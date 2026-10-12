using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Seed;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Datavello.Tests.Data;

[Collection(DatabaseCollection.Name)]
public sealed class DemoSeederTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    private async Task SeedAsync(bool reset = false)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(reset);
    }

    private async Task<(int Users, int Companies, int Transactions)> DemoCountsAsync()
    {
        await using var db = database.CreateContext();
        var companyIds = db.Companies.Where(c => c.Name == DemoSeeder.DemoCompanyName).Select(c => c.Id);
        return (
            await db.Users.CountAsync(u => u.Email == DemoSeeder.DemoEmail),
            await companyIds.CountAsync(),
            await db.Transactions.IgnoreQueryFilters().CountAsync(t => companyIds.Contains(t.CompanyId)));
    }

    [Fact]
    public async Task Seed_is_idempotent_reset_recreates_and_other_companies_are_untouched()
    {
        var (otherClient, other) = await _factory.RegisterAsync("Empresa real do usuário");
        var vendas = await otherClient.CategoryAsync("Vendas");
        await otherClient.AddTransactionAsync("2026-01-10", TransactionType.Income, 123m, vendas.Id);

        await SeedAsync();
        var first = await DemoCountsAsync();
        Assert.Equal(1, first.Users);
        Assert.Equal(1, first.Companies);
        Assert.True(first.Transactions > 200, $"expected a full demo dataset, got {first.Transactions}");

        await SeedAsync();
        Assert.Equal(first, await DemoCountsAsync());

        await SeedAsync(reset: true);
        Assert.Equal(first, await DemoCountsAsync());

        await using var db = database.CreateContext(other.ActiveCompany!.Id);
        Assert.Equal(1, await db.Transactions.CountAsync());
        Assert.Equal(10, await db.Categories.CountAsync());
    }

    public void Dispose() => _factory.Dispose();
}
