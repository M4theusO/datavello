using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Datavello.Tests.Data;

[Collection(DatabaseCollection.Name)]
public sealed class PersistenceTests(TestDatabase database)
{
    [Fact]
    public async Task Money_sums_are_exact_decimal_values()
    {
        var (company, category) = await database.CreateCompanyAsync("Empresa Precisão");

        await using (var db = database.CreateContext(company.Id))
        {
            for (var i = 0; i < 10; i++)
            {
                db.Transactions.Add(new Transaction
                {
                    Date = new DateOnly(2026, 2, 1),
                    Description = $"Centavos {i}",
                    Type = TransactionType.Income,
                    Amount = 0.10m,
                    CategoryId = category.Id
                });
            }

            db.Transactions.Add(new Transaction
            {
                Date = new DateOnly(2026, 2, 2),
                Description = "Valor alto",
                Type = TransactionType.Income,
                Amount = 1_234_567.89m,
                CategoryId = category.Id
            });
            await db.SaveChangesAsync();
        }

        await using var read = database.CreateContext(company.Id);
        var total = await read.Transactions.SumAsync(t => t.Amount);

        Assert.Equal(1_234_568.89m, total);
    }

    [Fact]
    public async Task Non_positive_amount_is_rejected_by_the_database()
    {
        var (company, category) = await database.CreateCompanyAsync("Empresa Valor Zero");

        await using var db = database.CreateContext(company.Id);
        db.Transactions.Add(new Transaction
        {
            Date = new DateOnly(2026, 3, 1),
            Description = "Inválida",
            Type = TransactionType.Expense,
            Amount = 0m,
            CategoryId = category.Id
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<PostgresException>(ex.InnerException);
    }

    [Fact]
    public async Task Category_in_use_cannot_be_deleted()
    {
        var (company, category) = await database.CreateCompanyAsync("Empresa Vínculo");

        await using (var db = database.CreateContext(company.Id))
        {
            db.Transactions.Add(new Transaction
            {
                Date = new DateOnly(2026, 3, 1),
                Description = "Venda",
                Type = TransactionType.Income,
                Amount = 10m,
                CategoryId = category.Id
            });
            await db.SaveChangesAsync();
        }

        await using var del = database.CreateContext(company.Id);
        del.Categories.Remove(await del.Categories.SingleAsync(c => c.Id == category.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => del.SaveChangesAsync());
    }

    [Fact]
    public async Task Default_dashboard_template_is_seeded_with_all_widgets()
    {
        await using var db = database.CreateContext();

        var template = await db.DashboardTemplates.SingleAsync(t => t.Key == DashboardTemplateConfiguration.DefaultFinancialTemplateKey);

        Assert.Equal(WidgetCatalog.All, template.Widgets);
        Assert.Equal(DashboardPeriod.Last6Months, template.DefaultPeriod);
    }
}
