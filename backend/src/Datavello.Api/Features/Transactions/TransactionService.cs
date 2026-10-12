using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Http;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Transactions;

/// <summary>Transactions of the active company. Tenant isolation comes from the DbContext query filter.</summary>
public sealed class TransactionService(AppDbContext db, ICurrentTenant tenant)
{
    public async Task<PagedResult<TransactionDto>> ListAsync(TransactionQuery query, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        TransactionRules.ValidateRange(query.From, query.To);

        var source = Filter(db.Transactions.AsNoTracking(), query.From, query.To, query.CategoryId, query.Type, query.Search);

        // Secondary keys keep the order stable across pages.
        source = (query.SortBy, query.SortDir) switch
        {
            (TransactionSortField.Amount, SortDirection.Asc) => source.OrderBy(t => t.Amount).ThenByDescending(t => t.Date).ThenByDescending(t => t.Id),
            (TransactionSortField.Amount, SortDirection.Desc) => source.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Date).ThenByDescending(t => t.Id),
            (_, SortDirection.Asc) => source.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id),
            _ => source.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
        };

        return await source.Select(ToDto).ToPagedResultAsync(query, ct);
    }

    /// <summary>Applies the filters shared by the transaction list and the dashboard.</summary>
    public static IQueryable<Transaction> Filter(
        IQueryable<Transaction> source, DateOnly? from, DateOnly? to, Guid? categoryId, TransactionType? type, string? search = null)
    {
        if (from is { } f) source = source.Where(t => t.Date >= f);
        if (to is { } t2) source = source.Where(t => t.Date <= t2);
        if (categoryId is { } c) source = source.Where(t => t.CategoryId == c);
        if (type is { } ty) source = source.Where(t => t.Type == ty);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            source = source.Where(t => EF.Functions.ILike(t.Description, pattern, @"\"));
        }

        return source;
    }

    public async Task<TransactionDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        return await db.Transactions.AsNoTracking().Where(t => t.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("Transação não encontrada.");
    }

    public async Task<TransactionDto> CreateAsync(TransactionRequest request, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var transaction = new Transaction();
        await ApplyAsync(transaction, request, ct);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return await GetAsync(transaction.Id, ct);
    }

    public async Task<TransactionDto> UpdateAsync(Guid id, TransactionRequest request, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("Transação não encontrada.");

        await ApplyAsync(transaction, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("Transação não encontrada.");

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Transaction transaction, TransactionRequest request, CancellationToken ct)
    {
        var date = request.Date!.Value;
        var type = request.Type!.Value;
        var amount = request.Amount!.Value;

        var error = TransactionRules.ValidateDate(date) ?? TransactionRules.ValidateAmount(amount);
        if (error is not null)
        {
            throw AppException.BadRequest(error);
        }

        // Looked up through the tenant filter: a category from another company is simply "not found".
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            ?? throw AppException.BadRequest("Categoria não encontrada.");

        if (!category.Accepts(type))
        {
            var label = type == TransactionType.Income ? "receitas" : "despesas";
            throw AppException.BadRequest($"A categoria \"{category.Name}\" não aceita {label}.");
        }

        transaction.Date = date;
        transaction.Description = request.Description.Trim();
        transaction.Type = type;
        transaction.Amount = amount;
        transaction.CategoryId = category.Id;
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    /// <summary>Projection used by every read of transactions (list, detail, dashboard).</summary>
    public static readonly System.Linq.Expressions.Expression<Func<Transaction, TransactionDto>> ToDto = t =>
        new TransactionDto(
            t.Id, t.Date, t.Description, t.Type, t.Amount, t.CategoryId, t.Category.Name, t.Notes,
            t.ImportJobId != null, t.CreatedAt, t.UpdatedAt);
}
