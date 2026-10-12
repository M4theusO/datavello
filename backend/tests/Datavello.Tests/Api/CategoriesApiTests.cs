using System.Net;
using Datavello.Api.Domain;
using Datavello.Api.Features.Categories;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class CategoriesApiTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [Fact]
    public async Task Lists_default_categories_with_transaction_counts()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        await client.AddTransactionAsync("2026-01-10", TransactionType.Income, 100m, vendas.Id);
        await client.AddTransactionAsync("2026-01-11", TransactionType.Income, 50m, vendas.Id);

        var all = await client.GetJsonAsync<List<CategoryDto>>("/api/categories");

        Assert.Equal(10, all.Count);
        Assert.Equal(2, all.Single(c => c.Name == "Vendas").TransactionCount);
        Assert.All(all.Where(c => c.Name != "Vendas"), c => Assert.Equal(0, c.TransactionCount));
    }

    [Fact]
    public async Task Filter_by_kind_includes_categories_valid_for_both()
    {
        var (client, _) = await _factory.RegisterAsync();
        (await client.PostJsonAsync("/api/categories", new { name = "Ajustes", kind = CategoryKind.Both })).EnsureSuccessStatusCode();

        var income = await client.GetJsonAsync<List<CategoryDto>>("/api/categories?kind=Income");

        Assert.Contains(income, c => c.Name == "Ajustes");
        Assert.All(income, c => Assert.NotEqual(CategoryKind.Expense, c.Kind));
    }

    [Fact]
    public async Task Create_rejects_duplicate_names_ignoring_case()
    {
        var (client, _) = await _factory.RegisterAsync();

        var created = await client.PostJsonAsync("/api/categories", new { name = "Combustível", kind = CategoryKind.Expense });
        var duplicate = await client.PostJsonAsync("/api/categories", new { name = "  COMBUSTÍVEL ", kind = CategoryKind.Expense });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Same_name_is_allowed_in_different_companies()
    {
        var (clientA, _) = await _factory.RegisterAsync();
        var (clientB, _) = await _factory.RegisterAsync();

        var a = await clientA.PostJsonAsync("/api/categories", new { name = "Exclusiva", kind = CategoryKind.Expense });
        var b = await clientB.PostJsonAsync("/api/categories", new { name = "Exclusiva", kind = CategoryKind.Expense });

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
    }

    [Fact]
    public async Task Invalid_payload_returns_validation_errors()
    {
        var (client, _) = await _factory.RegisterAsync();

        var response = await client.PostJsonAsync("/api/categories", new { name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("O nome deve ter entre 2 e 80 caracteres.", body);
        Assert.Contains("Informe o tipo da categoria.", body);
    }

    [Fact]
    public async Task Category_in_use_cannot_be_deleted_but_unused_can()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        var marketing = await client.CategoryAsync("Marketing");
        await client.AddTransactionAsync("2026-02-01", TransactionType.Income, 10m, vendas.Id);

        var inUse = await client.DeleteAsync($"/api/categories/{vendas.Id}");
        var unused = await client.DeleteAsync($"/api/categories/{marketing.Id}");

        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        Assert.Contains("Reclassifique", await inUse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, unused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/categories/{marketing.Id}")).StatusCode);
    }

    [Fact]
    public async Task Kind_cannot_be_narrowed_when_incompatible_transactions_exist()
    {
        var (client, _) = await _factory.RegisterAsync();
        var vendas = await client.CategoryAsync("Vendas");
        await client.AddTransactionAsync("2026-02-01", TransactionType.Income, 10m, vendas.Id);

        var toExpense = await client.PutJsonAsync($"/api/categories/{vendas.Id}", new { name = "Vendas", kind = CategoryKind.Expense });
        var toBoth = await client.PutJsonAsync($"/api/categories/{vendas.Id}", new { name = "Vendas e outros", kind = CategoryKind.Both });

        Assert.Equal(HttpStatusCode.Conflict, toExpense.StatusCode);
        var updated = await toBoth.ReadAsync<CategoryDto>();
        Assert.Equal("Vendas e outros", updated.Name);
        Assert.Equal(CategoryKind.Both, updated.Kind);
    }

    [Fact]
    public async Task Another_company_cannot_read_change_or_delete_a_category()
    {
        var (clientA, _) = await _factory.RegisterAsync("Empresa A");
        var (clientB, _) = await _factory.RegisterAsync("Empresa B");
        var categoryOfA = await clientA.CategoryAsync("Vendas");

        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/categories/{categoryOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientB.PutJsonAsync($"/api/categories/{categoryOfA.Id}", new { name = "Invadida", kind = CategoryKind.Income })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/categories/{categoryOfA.Id}")).StatusCode);
        Assert.DoesNotContain(await clientB.GetJsonAsync<List<CategoryDto>>("/api/categories"), c => c.Id == categoryOfA.Id);

        Assert.Equal("Vendas", (await clientA.CategoryAsync("Vendas")).Name);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var anonymous = _factory.CreateBrowserLikeClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/categories")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
