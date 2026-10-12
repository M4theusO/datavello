namespace Datavello.Api.Domain;

public enum DashboardPeriod
{
    Last30Days,
    ThisMonth,
    LastMonth,
    Last3Months,
    Last6Months,
    Last12Months,
    ThisYear
}

/// <summary>Global (not tenant-owned) starting layout used to create and reset dashboards.</summary>
public class DashboardTemplate
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DashboardPeriod DefaultPeriod { get; set; }

    /// <summary>Ordered widget types enabled by default.</summary>
    public string[] Widgets { get; set; } = [];
}

public class Dashboard : ITenantOwned, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public DashboardTemplate? Template { get; set; }
    public DashboardPeriod DefaultPeriod { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<WidgetConfig> Widgets { get; set; } = [];
}

public class WidgetConfig : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid DashboardId { get; set; }

    /// <summary>Key from <see cref="WidgetCatalog"/>.</summary>
    public string WidgetType { get; set; } = string.Empty;

    public int Position { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Reserved for per-widget options (jsonb).</summary>
    public string? Settings { get; set; }
}

/// <summary>Fixed set of ready-made widgets available in the MVP.</summary>
public static class WidgetCatalog
{
    public const string KpiIncome = "kpi-income";
    public const string KpiExpense = "kpi-expense";
    public const string KpiBalance = "kpi-balance";
    public const string KpiCount = "kpi-count";
    public const string ChartMonthly = "chart-monthly";
    public const string ChartExpenseByCategory = "chart-expense-by-category";
    public const string ChartIncomeVsExpense = "chart-income-vs-expense";
    public const string ChartBalanceHistory = "chart-balance-history";
    public const string TableRecent = "table-recent";

    public static readonly IReadOnlyList<string> All =
    [
        KpiIncome, KpiExpense, KpiBalance, KpiCount,
        ChartMonthly, ChartExpenseByCategory, ChartIncomeVsExpense, ChartBalanceHistory,
        TableRecent
    ];

    public static bool IsKnown(string type) => All.Contains(type);
}
