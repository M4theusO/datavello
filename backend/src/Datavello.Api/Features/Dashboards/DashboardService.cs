using Datavello.Api.Domain;
using Datavello.Api.Features.Transactions;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Dashboards;

/// <summary>
/// Dashboard indicators, always aggregated in PostgreSQL from persisted transactions of the
/// active company (tenant query filter) and the requested filters.
/// </summary>
public sealed class DashboardService(AppDbContext db, ICurrentTenant tenant)
{
    public const int MaxMonths = 120;
    public const int RecentCount = 10;

    public async Task<DashboardSummary> GetSummaryAsync(DashboardQuery query, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var from = query.From!.Value;
        var to = query.To!.Value;
        TransactionRules.ValidateRange(from, to);
        if (PeriodMath.MonthIndex(to) - PeriodMath.MonthIndex(from) + 1 > MaxMonths)
        {
            throw AppException.BadRequest("O período máximo do dashboard é de 10 anos.");
        }

        var (prevFrom, prevTo) = PeriodMath.Previous(from, to);
        var all = db.Transactions.AsNoTracking();
        var current = TransactionService.Filter(all, from, to, query.CategoryId, query.Type);
        var previous = TransactionService.Filter(all, prevFrom, prevTo, query.CategoryId, query.Type);

        var currentKpis = await KpisAsync(current, ct);
        var previousKpis = await KpisAsync(previous, ct);

        return new DashboardSummary(
            new DatePeriod(from, to),
            new DatePeriod(prevFrom, prevTo),
            currentKpis,
            previousKpis.TransactionCount > 0 ? previousKpis : null,
            await MonthlyAsync(current, from, to, ct),
            await ExpensesByCategoryAsync(current, ct),
            await current
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Take(RecentCount)
                .Select(TransactionService.ToDto)
                .ToListAsync(ct));
    }

    public async Task<DashboardConfigDto> GetConfigAsync(CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var dashboard = await db.Dashboards.AsNoTracking()
            .Include(d => d.Widgets)
            .OrderByDescending(d => d.IsDefault).ThenBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct) ?? throw AppException.NotFound("Dashboard não encontrado.");

        var widgets = dashboard.Widgets
            .Where(w => WidgetCatalog.IsKnown(w.WidgetType))
            .OrderBy(w => w.Position)
            .Select(w => new WidgetConfigDto(w.WidgetType, w.Position, w.IsEnabled))
            .ToList();

        return new DashboardConfigDto(dashboard.Id, dashboard.Name, dashboard.DefaultPeriod, widgets);
    }

    private static async Task<Kpis> KpisAsync(IQueryable<Transaction> source, CancellationToken ct)
    {
        var totals = await source
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var income = totals.Where(x => x.Type == TransactionType.Income).Sum(x => x.Total);
        var expense = totals.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Total);
        return new Kpis(income, expense, income - expense, totals.Sum(x => x.Count));
    }

    private static async Task<IReadOnlyList<MonthlyPoint>> MonthlyAsync(
        IQueryable<Transaction> source, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await source
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        // Every month of the period appears, including months without transactions.
        var points = new List<MonthlyPoint>();
        var cumulative = 0m;
        foreach (var month in PeriodMath.MonthsIn(from, to))
        {
            var inMonth = rows.Where(r => r.Year == month.Year && r.Month == month.Month).ToList();
            var income = inMonth.Where(r => r.Type == TransactionType.Income).Sum(r => r.Total);
            var expense = inMonth.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Total);
            cumulative += income - expense;
            points.Add(new MonthlyPoint($"{month:yyyy-MM}", income, expense, income - expense, cumulative));
        }

        return points;
    }

    private async Task<IReadOnlyList<CategoryTotal>> ExpensesByCategoryAsync(IQueryable<Transaction> source, CancellationToken ct)
    {
        var totals = await source
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        if (totals.Count == 0)
        {
            return [];
        }

        var ids = totals.Select(x => x.CategoryId).ToList();
        var names = await db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var grandTotal = totals.Sum(x => x.Total);
        return totals
            .Select(x => new CategoryTotal(
                x.CategoryId,
                names.GetValueOrDefault(x.CategoryId, "Sem categoria"),
                x.Total,
                decimal.Round(x.Total / grandTotal, 4)))
            .ToList();
    }
}
