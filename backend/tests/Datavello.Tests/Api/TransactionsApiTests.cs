using System.Net;
using Datavello.Api.Domain;
using Datavello.Api.Features.Transactions;
using Datavello.Api.Infrastructure.Http;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class TransactionsApiTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [Fact]
    public async Task Create_get_update_and_delete()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        var servicos = await client.CategoryAsync("Serviços");

        var created = await client.AddTransactionAsync("2026-03-15", TransactionType.Income, 1250.90m, vendas.Id, "  Venda balcão  ");
        Assert.Equal("Venda balcão", created.Description);
        Assert.Equal(1250.90m, created.Amount);
        Assert.Equal("Vendas", created.CategoryName);
        Assert.False(created.Imported);

        var update = await client.PutJsonAsync($"/api/transactions/{created.Id}", new
        {
            date = "2026-03-16",
            description = "Serviço de entrega",
            type = TransactionType.Income,
            amount = 80.5m,
            categoryId = servicos.Id,
            notes = "Cliente recorrente"
        });
        var updated = await update.ReadAsync<TransactionDto>();
        Assert.Equal(new DateOnly(2026, 3, 16), updated.Date);
        Assert.Equal("Serviços", updated.CategoryName);
        Assert.Equal("Cliente recorrente", updated.Notes);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transactions/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData(0, "maior que zero")]
    [InlineData(-10, "maior que zero")]
    [InlineData(10.555, "2 casas decimais")]
    public async Task Invalid_amounts_are_rejected(decimal amount, string message)
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");

        var response = await client.PostJsonAsync("/api/transactions", new
        {
            date = "2026-01-01", description = "Teste", type = TransactionType.Income, amount, categoryId = vendas.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_fields_return_validation_errors_in_portuguese()
    {
        var (client, _) = await _factory.RegisterAsync();

        var response = await client.PostJsonAsync("/api/transactions", new { description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Informe a data.", body);
        Assert.Contains("Informe o valor.", body);
        Assert.Contains("Informe a categoria.", body);
    }

    [Fact]
    public async Task Category_must_accept_the_transaction_type()
    {
        var (client, _) = await _factory.RegisterAsync();
        var aluguel = await client.CategoryAsync("Aluguel");

        var response = await client.PostJsonAsync("/api/transactions", new
        {
            date = "2026-01-01", description = "Receita errada", type = TransactionType.Income, amount = 10m, categoryId = aluguel.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("não aceita receitas", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_filters_by_period_type_category_and_search()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        var aluguel = await client.CategoryAsync("Aluguel");
        await client.AddTransactionAsync("2026-01-05", TransactionType.Income, 100m, vendas.Id, "Venda janeiro");
        await client.AddTransactionAsync("2026-02-05", TransactionType.Income, 200m, vendas.Id, "Venda fevereiro");
        await client.AddTransactionAsync("2026-02-10", TransactionType.Expense, 900m, aluguel.Id, "Aluguel fevereiro");
        await client.AddTransactionAsync("2026-03-05", TransactionType.Expense, 950m, aluguel.Id, "Aluguel março");

        var february = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?from=2026-02-01&to=2026-02-28");
        Assert.Equal(2, february.TotalCount);

        var expenses = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?type=Expense");
        Assert.All(expenses.Items, t => Assert.Equal(TransactionType.Expense, t.Type));
        Assert.Equal(2, expenses.TotalCount);

        var byCategory = await client.GetJsonAsync<PagedResult<TransactionDto>>($"/api/transactions?categoryId={vendas.Id}");
        Assert.Equal(2, byCategory.TotalCount);

        var search = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?search=FEVEREIRO");
        Assert.Equal(["Aluguel fevereiro", "Venda fevereiro"], search.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        await client.AddTransactionAsync("2026-01-05", TransactionType.Income, 10m, vendas.Id, "Desconto 50% cliente");
        await client.AddTransactionAsync("2026-01-06", TransactionType.Income, 10m, vendas.Id, "Desconto 500 cliente");

        var result = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?search=50%25");

        Assert.Equal(["Desconto 50% cliente"], result.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task List_sorts_and_paginates()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        decimal[] amounts = [30m, 10m, 50m, 20m, 40m];
        for (var i = 0; i < amounts.Length; i++)
        {
            await client.AddTransactionAsync($"2026-01-0{i + 1}", TransactionType.Income, amounts[i], vendas.Id);
        }

        var byDateDesc = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?pageSize=2");
        Assert.Equal(5, byDateDesc.TotalCount);
        Assert.Equal(3, byDateDesc.TotalPages);
        Assert.Equal([new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 4)], byDateDesc.Items.Select(t => t.Date));

        var byAmountAsc = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?sortBy=Amount&sortDir=Asc&page=2&pageSize=2");
        Assert.Equal([30m, 40m], byAmountAsc.Items.Select(t => t.Amount));

        var lastPage = await client.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions?page=3&pageSize=2");
        Assert.Single(lastPage.Items);
    }

    [Fact]
    public async Task Invalid_query_parameters_are_rejected()
    {
        var (client, _) = await _factory.RegisterAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/transactions?pageSize=500")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/transactions?from=2026-03-01&to=2026-01-01")).StatusCode);
    }

    [Fact]
    public async Task Another_company_cannot_see_change_delete_or_reference_data()
    {
        var (clientA, _) = await _factory.RegisterAsync("Empresa A");
        var (clientB, _) = await _factory.RegisterAsync("Empresa B");
        var vendasA = await clientA.CategoryAsync("Vendas");
        var vendasB = await clientB.CategoryAsync("Vendas");
        var transactionOfA = await clientA.AddTransactionAsync("2026-01-05", TransactionType.Income, 777m, vendasA.Id);

        // Read, update and delete by id
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/transactions/{transactionOfA.Id}")).StatusCode);
        var update = await clientB.PutJsonAsync($"/api/transactions/{transactionOfA.Id}", new
        {
            date = "2026-01-05", description = "Alterada", type = TransactionType.Income, amount = 1m, categoryId = vendasB.Id
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/transactions/{transactionOfA.Id}")).StatusCode);

        // Listing and filtering by the other company's category
        Assert.Equal(0, (await clientB.GetJsonAsync<PagedResult<TransactionDto>>("/api/transactions")).TotalCount);
        Assert.Equal(0, (await clientB.GetJsonAsync<PagedResult<TransactionDto>>($"/api/transactions?categoryId={vendasA.Id}")).TotalCount);

        // Creating a transaction that points to A's category
        var crossReference = await clientB.PostJsonAsync("/api/transactions", new
        {
            date = "2026-01-05", description = "Referência cruzada", type = TransactionType.Income, amount = 1m, categoryId = vendasA.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, crossReference.StatusCode);

        // A's data is intact
        var stillThere = await clientA.GetJsonAsync<TransactionDto>($"/api/transactions/{transactionOfA.Id}");
        Assert.Equal(777m, stillThere.Amount);
    }

    public void Dispose() => _factory.Dispose();
}
