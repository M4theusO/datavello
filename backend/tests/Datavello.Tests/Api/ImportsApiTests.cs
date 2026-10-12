using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ClosedXML.Excel;
using Datavello.Api.Domain;
using Datavello.Api.Features.Categories;
using Datavello.Api.Features.Imports;
using Datavello.Api.Features.Transactions;
using Datavello.Api.Infrastructure.Http;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class ImportsApiTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    private const string SampleCsv =
        "Data;Descrição;Valor;Tipo;Categoria\n" +
        "05/01/2026;Venda balcão;1.500,00;Receita;Vendas\n" +
        "06/01/2026;Aluguel janeiro;900,00;Despesa;Aluguel\n" +
        "07/01/2026;Compra insumos;350,25;Despesa;\n" +
        "\n" +
        "32/01/2026;Data errada;10,00;Despesa;Aluguel\n" +
        "08/01/2026;Tipo errado;10,00;Transferência;Aluguel\n";

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/imports", form);
    }

    private static async Task<ImportPreviewDto> UploadCsvAsync(HttpClient client, string csv, string fileName = "lancamentos.csv")
    {
        var response = await UploadAsync(client, Encoding.UTF8.GetBytes(csv), fileName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<ImportPreviewDto>();
    }

    private static object Options(ImportPreviewDto preview, Guid? defaultExpense = null, bool skipDuplicates = true, bool createMissing = false) => new
    {
        mapping = new
        {
            date = preview.SuggestedMapping.Date,
            description = preview.SuggestedMapping.Description,
            amount = preview.SuggestedMapping.Amount,
            type = preview.SuggestedMapping.Type,
            category = preview.SuggestedMapping.Category
        },
        dateFormat = DateFormatOption.DayMonthYear,
        decimalSeparator = DecimalSeparatorOption.Comma,
        typeConvention = TypeConvention.Column,
        defaultExpenseCategoryId = defaultExpense,
        skipDuplicates,
        createMissingCategories = createMissing
    };

    [Fact]
    public async Task Upload_returns_preview_and_suggested_mapping()
    {
        var (client, _) = await _factory.RegisterAsync();

        var preview = await UploadCsvAsync(client, SampleCsv);

        Assert.Equal("csv", preview.FileType);
        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(1, preview.EmptyRows);
        Assert.Equal(new SuggestedMapping(0, 1, 2, 3, 4), preview.SuggestedMapping);
        Assert.Equal(TypeConvention.Column, preview.SuggestedTypeConvention);
        Assert.Equal(["05/01/2026", "Venda balcão", "1.500,00", "Receita", "Vendas"], preview.PreviewRows[0].Cells);
    }

    [Fact]
    public async Task Validate_reports_counts_and_row_errors_without_writing()
    {
        var (client, _) = await _factory.RegisterAsync();
        var outras = await client.CategoryAsync("Outras despesas");
        var preview = await UploadCsvAsync(client, SampleCsv);

        var validation = await (await client.PostJsonAsync($"/api/imports/{preview.Id}/validate", Options(preview, outras.Id)))
            .ReadAsync<ImportValidationDto>();

        Assert.Equal(3, validation.ValidRows);
        Assert.Equal(2, validation.InvalidRows);
        Assert.Equal(1, validation.EmptyRows);
        Assert.Equal(1500m, validation.IncomeTotal);
        Assert.Equal(1250.25m, validation.ExpenseTotal);
        Assert.Equal([6, 7], validation.Errors.Select(e => e.RowNumber));
        Assert.Equal("Outras despesas", validation.Sample[2].CategoryName);

        var list = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions");
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task Commit_imports_valid_rows_and_records_history()
    {
        var (client, me) = await _factory.RegisterAsync();
        var outras = await client.CategoryAsync("Outras despesas");
        var preview = await UploadCsvAsync(client, SampleCsv, "janeiro.csv");

        var result = await (await client.PostJsonAsync($"/api/imports/{preview.Id}/commit", Options(preview, outras.Id)))
            .ReadAsync<ImportJobDetailDto>();

        Assert.Equal(ImportStatus.CompletedWithErrors, result.Job.Status);
        Assert.Equal(3, result.Job.ImportedRows);
        Assert.Equal(2, result.Job.RejectedRows);
        Assert.Equal(me.User.FullName, result.Job.UserName);
        Assert.Equal([6, 7], result.Errors.Select(e => e.RowNumber));

        var transactions = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?sortDir=Asc");
        Assert.Equal(["Venda balcão", "Aluguel janeiro", "Compra insumos"], transactions.Items.Select(t => t.Description));
        Assert.All(transactions.Items, t => Assert.True(t.Imported));

        var history = await client.GetJsonAsync<PagedResult<ImportJobDto>>("/api/imports");
        Assert.Equal("janeiro.csv", history.Items.Single().FileName);

        var again = await client.PostJsonAsync($"/api/imports/{preview.Id}/commit", Options(preview, outras.Id));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_skips_duplicates_unless_asked()
    {
        var (client, _) = await _factory.RegisterAsync();
        const string csv = "Data;Descrição;Valor;Tipo;Categoria\n05/01/2026;Venda balcão;100,00;Receita;Vendas\n06/01/2026;Aluguel;900,00;Despesa;Aluguel\n";

        var first = await UploadCsvAsync(client, csv);
        await (await client.PostJsonAsync($"/api/imports/{first.Id}/commit", Options(first))).ReadAsync<ImportJobDetailDto>();

        var second = await UploadCsvAsync(client, csv);
        var skipped = await (await client.PostJsonAsync($"/api/imports/{second.Id}/commit", Options(second))).ReadAsync<ImportJobDetailDto>();
        Assert.Equal(ImportStatus.Failed, skipped.Job.Status);
        Assert.Equal(0, skipped.Job.ImportedRows);
        Assert.All(skipped.Errors, e => Assert.Equal("Duplicidade", e.Field));

        var third = await UploadCsvAsync(client, csv);
        var forced = await (await client.PostJsonAsync($"/api/imports/{third.Id}/commit", Options(third, skipDuplicates: false))).ReadAsync<ImportJobDetailDto>();
        Assert.Equal(ImportStatus.Completed, forced.Job.Status);
        Assert.Equal(4, (await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions")).TotalCount);
    }

    [Fact]
    public async Task Missing_categories_can_be_created_automatically()
    {
        var (client, _) = await _factory.RegisterAsync();
        var preview = await UploadCsvAsync(client,
            "Data;Descrição;Valor;Tipo;Categoria\n05/01/2026;Gasolina;200,00;Despesa;Combustível\n06/01/2026;Diesel;100,00;Despesa;combustivel\n");

        var result = await (await client.PostJsonAsync($"/api/imports/{preview.Id}/commit", Options(preview, createMissing: true)))
            .ReadAsync<ImportJobDetailDto>();

        Assert.Equal(ImportStatus.Completed, result.Job.Status);
        var created = await client.CategoryAsync("Combustível");
        Assert.Equal(CategoryKind.Expense, created.Kind);
        Assert.Equal(2, created.TransactionCount);
    }

    [Fact]
    public async Task Xlsx_files_are_imported_end_to_end()
    {
        var (client, _) = await _factory.RegisterAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Extrato");
        sheet.Cell(1, 1).Value = "Data";
        sheet.Cell(1, 2).Value = "Histórico";
        sheet.Cell(1, 3).Value = "Valor";
        sheet.Cell(2, 1).Value = new DateTime(2026, 2, 10);
        sheet.Cell(2, 2).Value = "Recebimento cliente";
        sheet.Cell(2, 3).Value = 2500.75;
        sheet.Cell(3, 1).Value = new DateTime(2026, 2, 11);
        sheet.Cell(3, 2).Value = "Pagamento fornecedor";
        sheet.Cell(3, 3).Value = -800;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var uploaded = await UploadAsync(client, stream.ToArray(), "extrato.xlsx");
        var preview = await uploaded.ReadAsync<ImportPreviewDto>();
        Assert.Equal(TypeConvention.Sign, preview.SuggestedTypeConvention);

        var vendas = await client.CategoryAsync("Vendas");
        var fornecedores = await client.CategoryAsync("Fornecedores");
        var result = await (await client.PostJsonAsync($"/api/imports/{preview.Id}/commit", new
        {
            mapping = new { date = 0, description = 1, amount = 2 },
            typeConvention = TypeConvention.Sign,
            defaultIncomeCategoryId = vendas.Id,
            defaultExpenseCategoryId = fornecedores.Id
        })).ReadAsync<ImportJobDetailDto>();

        Assert.Equal(ImportStatus.Completed, result.Job.Status);
        var items = (await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?sortDir=Asc")).Items;
        Assert.Equal([(TransactionType.Income, 2500.75m, "Vendas"), (TransactionType.Expense, 800m, "Fornecedores")],
            items.Select(t => (t.Type, t.Amount, t.CategoryName)));
    }

    [Theory]
    [InlineData("planilha.xls", "Formato não suportado")]
    [InlineData("macro.xlsm", "Formato não suportado")]
    [InlineData("vazio.csv", "vazio")]
    public async Task Invalid_files_are_rejected_with_a_message(string fileName, string message)
    {
        var (client, _) = await _factory.RegisterAsync();
        var content = fileName == "vazio.csv" ? [] : Encoding.UTF8.GetBytes("Data;Valor\n01/01/2026;1");

        var response = await UploadAsync(client, content, fileName);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Files_above_the_size_limit_are_rejected()
    {
        var (client, _) = await _factory.RegisterAsync();
        var big = new byte[6 * 1024 * 1024];
        Array.Fill(big, (byte)'a');

        var response = await UploadAsync(client, big, "grande.csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tamanho máximo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Another_company_cannot_use_or_see_an_import()
    {
        var (clientA, _) = await _factory.RegisterAsync("Empresa A");
        var (clientB, _) = await _factory.RegisterAsync("Empresa B");
        var preview = await UploadCsvAsync(clientA, SampleCsv);

        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostJsonAsync($"/api/imports/{preview.Id}/validate", Options(preview))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostJsonAsync($"/api/imports/{preview.Id}/commit", Options(preview))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/imports/{preview.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/imports/{preview.Id}")).StatusCode);

        // B's own category ids are not valid for A's import either.
        var categoryOfB = await clientB.CategoryAsync("Outras despesas");
        var crossDefault = await clientA.PostJsonAsync($"/api/imports/{preview.Id}/validate", Options(preview, categoryOfB.Id));
        Assert.Equal(HttpStatusCode.BadRequest, crossDefault.StatusCode);

        Assert.Equal(0, (await clientB.GetJsonAsync<PagedResult<ImportJobDto>>("/api/imports")).TotalCount);
    }

    [Fact]
    public async Task Pending_import_can_be_cancelled()
    {
        var (client, _) = await _factory.RegisterAsync();
        var preview = await UploadCsvAsync(client, SampleCsv);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/imports/{preview.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/imports/{preview.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_mapping_is_rejected()
    {
        var (client, _) = await _factory.RegisterAsync();
        var preview = await UploadCsvAsync(client, SampleCsv);

        var sameColumnTwice = await client.PostJsonAsync($"/api/imports/{preview.Id}/validate", new
        {
            mapping = new { date = 0, description = 0, amount = 2, type = 3 }
        });
        var outOfRange = await client.PostJsonAsync($"/api/imports/{preview.Id}/validate", new
        {
            mapping = new { date = 0, description = 1, amount = 99, type = 3 }
        });

        Assert.Equal(HttpStatusCode.BadRequest, sameColumnTwice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
