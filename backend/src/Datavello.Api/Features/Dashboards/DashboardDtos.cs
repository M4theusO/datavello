using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;
using Datavello.Api.Features.Transactions;

namespace Datavello.Api.Features.Dashboards;

public sealed class DashboardQuery
{
    /// <summary>Data inicial (inclusive), formato yyyy-MM-dd.</summary>
    [Required(ErrorMessage = "Informe a data inicial.")]
    public DateOnly? From { get; init; }

    /// <summary>Data final (inclusive), formato yyyy-MM-dd.</summary>
    [Required(ErrorMessage = "Informe a data final.")]
    public DateOnly? To { get; init; }

    public Guid? CategoryId { get; init; }
    public TransactionType? Type { get; init; }
}

public sealed record DatePeriod(DateOnly From, DateOnly To);

/// <summary>"Saldo do período" = receitas − despesas cadastradas no período. Não é lucro contábil nem fluxo de caixa completo.</summary>
public sealed record Kpis(decimal Income, decimal Expense, decimal Balance, int TransactionCount);

/// <param name="Month">Mês no formato yyyy-MM.</param>
/// <param name="CumulativeBalance">Saldo acumulado desde o início do período selecionado.</param>
public sealed record MonthlyPoint(string Month, decimal Income, decimal Expense, decimal Balance, decimal CumulativeBalance);

/// <param name="Share">Participação no total de despesas do período (0 a 1).</param>
public sealed record CategoryTotal(Guid CategoryId, string CategoryName, decimal Total, decimal Share);

/// <param name="Previous">Indicadores do período anterior; nulo quando não há transações nele.</param>
public sealed record DashboardSummary(
    DatePeriod Period,
    DatePeriod PreviousPeriod,
    Kpis Current,
    Kpis? Previous,
    IReadOnlyList<MonthlyPoint> Monthly,
    IReadOnlyList<CategoryTotal> ExpensesByCategory,
    IReadOnlyList<TransactionDto> Recent);

public sealed record WidgetConfigDto(string Type, int Position, bool IsEnabled);

public sealed class WidgetSetting
{
    /// <example>chart-monthly</example>
    [Required(ErrorMessage = "Informe o widget.")]
    public string Type { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}

/// <summary>Widgets in the desired order; any widget left out is kept at the end, disabled.</summary>
public sealed class UpdateDashboardConfigRequest
{
    [Required(ErrorMessage = "Informe o período padrão.")]
    [EnumDataType(typeof(DashboardPeriod), ErrorMessage = "Período padrão inválido.")]
    public DashboardPeriod? DefaultPeriod { get; init; }

    [Required(ErrorMessage = "Informe os widgets.")]
    public List<WidgetSetting> Widgets { get; init; } = [];
}

public sealed record DashboardConfigDto(Guid Id, string Name, DashboardPeriod DefaultPeriod, IReadOnlyList<WidgetConfigDto> Widgets);
