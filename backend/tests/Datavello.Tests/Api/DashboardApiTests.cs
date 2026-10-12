using System.Net;
using Datavello.Api.Domain;
using Datavello.Api.Features.Dashboards;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class DashboardApiTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    /// <summary>
    /// Fixture data (Empresa principal):
    ///   Dec/2025: income 500                                  (previous period of Jan–Mar)
    ///   Jan/2026: income 1000 (Vendas), expense 300 (Aluguel)
    ///   Feb/2026: no transactions
    ///   Mar/2026: income 200.50 (Serviços), expense 100.25 (Marketing), expense 50 (Aluguel)
    ///   Apr/2026: expense 999 (outside Jan–Mar)
    /// </summary>
    private async Task<HttpClient> SeedAsync()
    {
        var (client, _) = await _factory.RegisterAsync("Empresa principal");
        var vendas = await client.CategoryAsync("Vendas");
        var servicos = await client.CategoryAsync("Serviços");
        var aluguel = await client.CategoryAsync("Aluguel");
        var marketing = await client.CategoryAsync("Marketing");

        await client.AddTransactionAsync("2025-12-20", TransactionType.Income, 500m, vendas.Id);
        await client.AddTransactionAsync("2026-01-10", TransactionType.Income, 1000m, vendas.Id);
        await client.AddTransactionAsync("2026-01-15", TransactionType.Expense, 300m, aluguel.Id);
        await client.AddTransactionAsync("2026-03-02", TransactionType.Income, 200.50m, servicos.Id);
        await client.AddTransactionAsync("2026-03-20", TransactionType.Expense, 100.25m, marketing.Id, "Anúncios");
        await client.AddTransactionAsync("2026-03-31", TransactionType.Expense, 50m, aluguel.Id, "Último do período");
        await client.AddTransactionAsync("2026-04-01", TransactionType.Expense, 999m, aluguel.Id);
        return client;
    }

    [Fact]
    public async Task Kpis_are_calculated_from_persisted_transactions()
    {
        var client = await SeedAsync();

        var summary = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-01-01&to=2026-03-31");

        Assert.Equal(new Kpis(1200.50m, 450.25m, 750.25m, 5), summary.Current);
        Assert.Equal(new DatePeriod(new DateOnly(2025, 10, 1), new DateOnly(2025, 12, 31)), summary.PreviousPeriod);
        Assert.Equal(new Kpis(500m, 0m, 500m, 1), summary.Previous);
    }

    [Fact]
    public async Task Previous_kpis_are_null_when_previous_period_has_no_data()
    {
        var client = await SeedAsync();

        var summary = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-02-01&to=2026-02-28");

        Assert.Equal(0, summary.Current.TransactionCount);
        Assert.NotNull(summary.Previous); // January has data
        var early = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2025-01-01&to=2025-01-31");
        Assert.Null(early.Previous);
    }

    [Fact]
    public async Task Monthly_series_includes_empty_months_and_cumulative_balance()
    {
        var client = await SeedAsync();

        var summary = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-01-01&to=2026-03-31");

        Assert.Equal(
            [
                new MonthlyPoint("2026-01", 1000m, 300m, 700m, 700m),
                new MonthlyPoint("2026-02", 0m, 0m, 0m, 700m),
                new MonthlyPoint("2026-03", 200.50m, 150.25m, 50.25m, 750.25m)
            ],
            summary.Monthly);
    }

    [Fact]
    public async Task Expenses_by_category_are_sorted_with_shares()
    {
        var client = await SeedAsync();

        var summary = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-01-01&to=2026-03-31");

        Assert.Equal(["Aluguel", "Marketing"], summary.ExpensesByCategory.Select(c => c.CategoryName));
        Assert.Equal([350m, 100.25m], summary.ExpensesByCategory.Select(c => c.Total));
        Assert.Equal(0.7773m, summary.ExpensesByCategory[0].Share);
    }

    [Fact]
    public async Task Recent_activity_is_newest_first_within_the_period()
    {
        var client = await SeedAsync();

        var summary = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-01-01&to=2026-03-31");

        Assert.Equal(5, summary.Recent.Count);
        Assert.Equal("Último do período", summary.Recent[0].Description);
        Assert.DoesNotContain(summary.Recent, t => t.Date > new DateOnly(2026, 3, 31));
    }

    [Fact]
    public async Task Type_and_category_filters_apply_to_every_block()
    {
        var client = await SeedAsync();
        var aluguel = await client.CategoryAsync("Aluguel");

        var incomeOnly = await client.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2026-01-01&to=2026-03-31&type=Income");
        Assert.Equal(new Kpis(1200.50m, 0m, 1200.50m, 2), incomeOnly.Current);
        Assert.Empty(incomeOnly.ExpensesByCategory);
        Assert.All(incomeOnly.Recent, t => Assert.Equal(TransactionType.Income, t.Type));
        Assert.All(incomeOnly.Monthly, m => Assert.Equal(0m, m.Expense));

        var rentOnly = await client.GetJsonAsync<DashboardSummary>($"/api/dashboard/summary?from=2026-01-01&to=2026-03-31&categoryId={aluguel.Id}");
        Assert.Equal(new Kpis(0m, 350m, -350m, 2), rentOnly.Current);
        Assert.Equal(["Aluguel"], rentOnly.ExpensesByCategory.Select(c => c.CategoryName));
        Assert.All(rentOnly.Recent, t => Assert.Equal("Aluguel", t.CategoryName));
    }

    [Fact]
    public async Task Another_company_data_never_appears()
    {
        await SeedAsync();
        var (other, _) = await _factory.RegisterAsync("Outra empresa");

        var summary = await other.GetJsonAsync<DashboardSummary>("/api/dashboard/summary?from=2025-01-01&to=2026-12-31");

        Assert.Equal(new Kpis(0m, 0m, 0m, 0), summary.Current);
        Assert.Empty(summary.Recent);
        Assert.Empty(summary.ExpensesByCategory);
    }

    [Fact]
    public async Task Invalid_periods_are_rejected()
    {
        var (client, _) = await _factory.RegisterAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard/summary?from=2026-03-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard/summary?from=2000-01-01&to=2026-01-01")).StatusCode);
    }

    [Fact]
    public async Task Config_returns_default_widgets_in_order()
    {
        var (client, _) = await _factory.RegisterAsync();

        var config = await client.GetJsonAsync<DashboardConfigDto>("/api/dashboard/config");

        Assert.Equal(DashboardPeriod.Last6Months, config.DefaultPeriod);
        Assert.Equal(WidgetCatalog.All, config.Widgets.Select(w => w.Type));
        Assert.All(config.Widgets, w => Assert.True(w.IsEnabled));
    }

    public void Dispose() => _factory.Dispose();
}
