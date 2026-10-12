using System.Text.Json;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Http;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Features.Imports;

/// <summary>Spreadsheet import workflow for the active company. Rules: docs/data-import.md.</summary>
public sealed class ImportService(
    AppDbContext db,
    ICurrentTenant tenant,
    IOptions<ImportSettings> settings,
    ILogger<ImportService> logger)
{
    private const int MaxErrorsInResponse = 200;
    private const int SampleSize = 10;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ImportSettings Limits => settings.Value;

    // ---------------- 1. Upload ----------------

    public async Task<ImportPreviewDto> UploadAsync(IFormFile? file, CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        if (file is null)
        {
            throw AppException.BadRequest("Selecione um arquivo .csv ou .xlsx.");
        }

        if (file.Length == 0)
        {
            throw AppException.BadRequest("O arquivo está vazio.");
        }

        var fileName = Path.GetFileName(file.FileName);
        var fileType = SpreadsheetReader.FileTypeFor(fileName)
            ?? throw AppException.BadRequest("Formato não suportado. Envie um arquivo .csv ou .xlsx (arquivos .xls e .xlsm não são aceitos).");

        if (file.Length > Limits.MaxFileSizeBytes)
        {
            throw AppException.BadRequest($"O arquivo excede o tamanho máximo de {Limits.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        using (var buffer = new MemoryStream((int)file.Length))
        {
            await stream.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        ParsedSheet sheet;
        try
        {
            sheet = SpreadsheetReader.Read(content, fileType, Limits.MaxRows);
        }
        catch (SpreadsheetException ex)
        {
            throw AppException.BadRequest(ex.Message);
        }

        await DiscardExpiredAsync(ct);

        var job = new ImportJob
        {
            CompanyId = companyId,
            UserId = tenant.UserId ?? throw AppException.Forbidden(),
            FileName = fileName.Length > 255 ? fileName[..255] : fileName,
            FileType = fileType,
            Status = ImportStatus.Pending,
            TotalRows = sheet.Rows.Count,
            StagingData = JsonSerializer.Serialize(new Staging(sheet.Headers, sheet.Rows, sheet.EmptyRows), Json)
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Import {ImportId} staged: {FileType}, {Rows} rows", job.Id, fileType, sheet.Rows.Count);

        var mapping = ImportParsing.SuggestMapping(sheet.Headers);
        return new ImportPreviewDto(
            job.Id,
            job.FileName,
            fileType,
            sheet.Headers,
            sheet.Rows.Take(Limits.PreviewRows)
                .Select(r => new PreviewRowDto(r.Number, r.Cells.Select(SpreadsheetReader.StripMarker).ToList()))
                .ToList(),
            sheet.Rows.Count,
            sheet.EmptyRows,
            mapping,
            ImportParsing.SuggestTypeConvention(mapping, sheet.Rows));
    }

    // ---------------- 2. Validate (no writes) ----------------

    public async Task<ImportValidationDto> ValidateAsync(Guid id, ImportOptions options, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var job = await FindPendingAsync(id, track: false, ct);
        var outcome = await EvaluateAsync(job, options, ct);

        return new ImportValidationDto(
            outcome.TotalRows,
            outcome.Valid.Count,
            outcome.RejectedRows.Count,
            outcome.DuplicateRows,
            outcome.EmptyRows,
            outcome.Valid.Where(v => v.Type == TransactionType.Income).Sum(v => v.Amount),
            outcome.Valid.Where(v => v.Type == TransactionType.Expense).Sum(v => v.Amount),
            outcome.NewCategories.Values.Select(v => v.Name).OrderBy(n => n).ToList(),
            outcome.Valid.Take(SampleSize)
                .Select(v => new ParsedRowDto(v.RowNumber, v.Date, v.Description, v.Type, v.Amount, v.CategoryName, v.NewCategory, v.PossibleDuplicate))
                .ToList(),
            outcome.Errors.Take(MaxErrorsInResponse).ToList(),
            outcome.Errors.Count);
    }

    // ---------------- 3. Commit (single transaction) ----------------

    public async Task<ImportJobDetailDto> CommitAsync(Guid id, ImportOptions options, CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Row lock: a second concurrent commit waits here and then sees the job is no longer pending.
        var job = await db.ImportJobs
            .FromSql($"""SELECT * FROM "ImportJobs" WHERE "Id" = {id} FOR UPDATE""")
            .SingleOrDefaultAsync(ct) ?? throw AppException.NotFound("Importação não encontrada.");
        if (job.Status != ImportStatus.Pending)
        {
            throw AppException.Conflict("Esta importação já foi concluída.");
        }

        var outcome = await EvaluateAsync(job, options, ct);

        // Categories created on demand (kind follows the rows that use them).
        var createdIds = new Dictionary<string, Guid>();
        foreach (var (key, (name, types)) in outcome.NewCategories)
        {
            var category = new Category
            {
                Name = name,
                Kind = types.Count > 1 ? CategoryKind.Both : types.Contains(TransactionType.Income) ? CategoryKind.Income : CategoryKind.Expense
            };
            db.Categories.Add(category);
            createdIds[key] = category.Id;
        }

        foreach (var row in outcome.Valid)
        {
            db.Transactions.Add(new Transaction
            {
                Date = row.Date,
                Description = row.Description,
                Type = row.Type,
                Amount = row.Amount,
                CategoryId = row.CategoryId ?? createdIds[TextNormalizer.Normalize(row.CategoryName)],
                ImportJobId = job.Id
            });
        }

        db.ImportRowErrors.AddRange(outcome.Errors.Select(e => new ImportRowError
        {
            ImportJobId = job.Id,
            RowNumber = e.RowNumber,
            Field = e.Field,
            Message = e.Message.Length > 500 ? e.Message[..500] : e.Message,
            RawValue = e.RawValue
        }));

        job.ImportedRows = outcome.Valid.Count;
        job.RejectedRows = outcome.RejectedRows.Count;
        job.Status = outcome.Valid.Count == 0
            ? ImportStatus.Failed
            : outcome.RejectedRows.Count == 0 ? ImportStatus.Completed : ImportStatus.CompletedWithErrors;
        job.Message = Summary(job, outcome);
        job.Options = JsonSerializer.Serialize(options, Json);
        job.StagingData = null;
        job.CompletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Import {ImportId} committed: {Status}, {Imported} imported, {Rejected} rejected",
            job.Id, job.Status, job.ImportedRows, job.RejectedRows);

        return await GetAsync(job.Id, ct);
    }

    // ---------------- History ----------------

    public async Task<PagedResult<ImportJobDto>> ListAsync(PageQuery page, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        return await db.ImportJobs.AsNoTracking()
            .Where(j => j.Status != ImportStatus.Pending)
            .OrderByDescending(j => j.CreatedAt)
            .Select(ToDto)
            .ToPagedResultAsync(page, ct);
    }

    public async Task<ImportJobDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var job = await db.ImportJobs.AsNoTracking().Where(j => j.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("Importação não encontrada.");

        var errors = db.ImportRowErrors.AsNoTracking().Where(e => e.ImportJobId == id);
        var list = await errors
            .OrderBy(e => e.RowNumber).ThenBy(e => e.Id)
            .Take(500)
            .Select(e => new RowErrorDto(e.RowNumber, e.Field, e.Message, e.RawValue))
            .ToListAsync(ct);

        return new ImportJobDetailDto(job, list, await errors.CountAsync(ct));
    }

    /// <summary>Discards an import that was not confirmed yet.</summary>
    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var job = await FindPendingAsync(id, track: true, ct);
        db.ImportJobs.Remove(job);
        await db.SaveChangesAsync(ct);
    }

    // ---------------- helpers ----------------

    private sealed record Staging(string[] Headers, IReadOnlyList<SheetRow> Rows, int EmptyRows);

    private async Task<ImportJob> FindPendingAsync(Guid id, bool track, CancellationToken ct)
    {
        var query = track ? db.ImportJobs : db.ImportJobs.AsNoTracking();
        var job = await query.SingleOrDefaultAsync(j => j.Id == id, ct)
            ?? throw AppException.NotFound("Importação não encontrada.");
        if (job.Status != ImportStatus.Pending || job.StagingData is null)
        {
            throw AppException.Conflict("Esta importação já foi concluída.");
        }

        return job;
    }

    private async Task<ImportOutcome> EvaluateAsync(ImportJob job, ImportOptions options, CancellationToken ct)
    {
        var staging = JsonSerializer.Deserialize<Staging>(job.StagingData!, Json)!;
        var sheet = new ParsedSheet(staging.Headers, staging.Rows, staging.EmptyRows);
        ValidateMapping(options.Mapping!, options.TypeConvention, sheet.Headers.Length);

        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        foreach (var defaultId in new[] { options.DefaultIncomeCategoryId, options.DefaultExpenseCategoryId })
        {
            if (defaultId is { } catId && categories.All(c => c.Id != catId))
            {
                throw AppException.BadRequest("A categoria padrão escolhida não foi encontrada.");
            }
        }

        var outcome = ImportValidator.Validate(sheet, options, categories);

        // Duplicates: existing transactions of this company in the file's date range.
        var existingKeys = new HashSet<string>();
        if (outcome.Valid.Count > 0)
        {
            var from = outcome.Valid.Min(v => v.Date);
            var to = outcome.Valid.Max(v => v.Date);
            var existing = await db.Transactions.AsNoTracking()
                .Where(t => t.Date >= from && t.Date <= to)
                .Select(t => new { t.Date, t.Type, t.Amount, t.Description })
                .ToListAsync(ct);
            existingKeys = existing.Select(t => ImportParsing.DuplicateKey(t.Date, t.Type, t.Amount, t.Description)).ToHashSet();
        }

        ImportValidator.ApplyDuplicates(outcome, existingKeys, options.SkipDuplicates);
        return outcome;
    }

    private static void ValidateMapping(ColumnMapping mapping, TypeConvention convention, int columnCount)
    {
        int?[] columns = [mapping.Date, mapping.Description, mapping.Amount, mapping.Type, mapping.Category];
        if (columns.Any(c => c is < 0 || c >= columnCount))
        {
            throw AppException.BadRequest("O mapeamento aponta para uma coluna que não existe no arquivo.");
        }

        var used = columns.Where(c => c is not null).ToList();
        if (used.Count != used.Distinct().Count())
        {
            throw AppException.BadRequest("Uma mesma coluna foi associada a mais de um campo.");
        }

        if (convention == TypeConvention.Column && mapping.Type is null)
        {
            throw AppException.BadRequest("Selecione a coluna de tipo ou escolha outra convenção para o tipo.");
        }
    }

    private async Task DiscardExpiredAsync(CancellationToken ct)
    {
        var limit = DateTimeOffset.UtcNow - Limits.PendingExpiration;
        await db.ImportJobs.Where(j => j.Status == ImportStatus.Pending && j.CreatedAt < limit).ExecuteDeleteAsync(ct);
    }

    private static string Summary(ImportJob job, ImportOutcome outcome)
    {
        var parts = new List<string> { $"{job.ImportedRows} importada(s)", $"{job.RejectedRows} rejeitada(s)" };
        if (outcome.DuplicateRows > 0) parts.Add($"{outcome.DuplicateRows} possível(is) duplicidade(s)");
        if (outcome.EmptyRows > 0) parts.Add($"{outcome.EmptyRows} linha(s) vazia(s) ignorada(s)");
        if (outcome.NewCategories.Count > 0) parts.Add($"{outcome.NewCategories.Count} categoria(s) criada(s)");
        return string.Join(", ", parts) + ".";
    }

    private static readonly System.Linq.Expressions.Expression<Func<ImportJob, ImportJobDto>> ToDto = j =>
        new ImportJobDto(j.Id, j.FileName, j.FileType, j.Status, j.User.FullName, j.TotalRows, j.ImportedRows, j.RejectedRows,
            j.Message, j.CreatedAt, j.CompletedAt);
}
