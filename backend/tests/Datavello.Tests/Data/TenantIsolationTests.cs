using Datavello.Api.Domain;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Tests.Data;

[Collection(DatabaseCollection.Name)]
public sealed class TenantIsolationTests(TestDatabase database)
{
    [Fact]
    public async Task Queries_only_return_rows_of_the_active_company()
    {
        var (companyA, categoryA) = await database.CreateCompanyAsync("Empresa A");
        var (companyB, categoryB) = await database.CreateCompanyAsync("Empresa B");

        await using var dbA = database.CreateContext(companyA.Id);
        var visibleToA = await dbA.Categories.Select(c => c.Id).ToListAsync();

        Assert.Contains(categoryA.Id, visibleToA);
        Assert.DoesNotContain(categoryB.Id, visibleToA);
        Assert.Null(await dbA.Categories.FirstOrDefaultAsync(c => c.Id == categoryB.Id));
        Assert.Null(await dbA.Categories.FindAsync(categoryB.Id));

        await using var dbB = database.CreateContext(companyB.Id);
        Assert.DoesNotContain(categoryA.Id, await dbB.Categories.Select(c => c.Id).ToListAsync());
    }

    [Fact]
    public async Task Without_active_company_tenant_data_is_invisible()
    {
        await database.CreateCompanyAsync("Empresa sem contexto");

        await using var db = database.CreateContext(companyId: null);

        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task New_entities_receive_the_active_company_and_timestamps()
    {
        var (company, category) = await database.CreateCompanyAsync("Empresa C");

        await using var db = database.CreateContext(company.Id);
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 1, 15),
            Description = "Venda balcão",
            Type = TransactionType.Income,
            Amount = 150.75m,
            CategoryId = category.Id
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        Assert.Equal(company.Id, transaction.CompanyId);
        Assert.NotEqual(default, transaction.CreatedAt);
        Assert.Equal(transaction.CreatedAt, transaction.UpdatedAt);
    }

    [Fact]
    public async Task Creating_data_for_another_company_is_blocked()
    {
        var (companyA, _) = await database.CreateCompanyAsync("Empresa D");
        var (companyB, _) = await database.CreateCompanyAsync("Empresa E");

        await using var dbA = database.CreateContext(companyA.Id);
        dbA.Categories.Add(new Category { CompanyId = companyB.Id, Name = "Intrusa", Kind = CategoryKind.Expense });

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task Changing_or_deleting_another_company_row_is_blocked()
    {
        var (companyA, _) = await database.CreateCompanyAsync("Empresa F");
        var (_, categoryB) = await database.CreateCompanyAsync("Empresa G");

        await using (var dbA = database.CreateContext(companyA.Id))
        {
            var foreign = await dbA.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == categoryB.Id);
            foreign.Name = "Alterada indevidamente";
            await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
        }

        await using (var dbA = database.CreateContext(companyA.Id))
        {
            var foreign = await dbA.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == categoryB.Id);
            dbA.Categories.Remove(foreign);
            await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
        }

        await using var system = database.CreateContext();
        var unchanged = await system.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == categoryB.Id);
        Assert.Equal("Vendas", unchanged.Name);
    }

    [Fact]
    public async Task Moving_a_row_to_another_company_is_blocked()
    {
        var (companyA, categoryA) = await database.CreateCompanyAsync("Empresa H");
        var (companyB, _) = await database.CreateCompanyAsync("Empresa I");

        await using var dbA = database.CreateContext(companyA.Id);
        var own = await dbA.Categories.SingleAsync(c => c.Id == categoryA.Id);
        own.CompanyId = companyB.Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbA.SaveChangesAsync());
    }
}
