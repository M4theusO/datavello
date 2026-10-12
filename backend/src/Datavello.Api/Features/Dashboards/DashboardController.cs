using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Dashboards;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>
    /// Indicadores, séries mensais, despesas por categoria e atividade recente do período,
    /// calculados a partir das transações cadastradas e dos filtros informados.
    /// </summary>
    /// <remarks>
    /// Exemplo: <c>GET /api/dashboard/summary?from=2026-01-01&amp;to=2026-06-30</c>.
    /// O período anterior (para comparação) tem a mesma duração e termina no dia anterior a <c>from</c>.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<DashboardSummary> Summary([FromQuery] DashboardQuery query, CancellationToken ct) =>
        dashboard.GetSummaryAsync(query, ct);

    /// <summary>Widgets do dashboard da empresa (ordem e ativação) e período padrão.</summary>
    [HttpGet("config")]
    public Task<DashboardConfigDto> Config(CancellationToken ct) => dashboard.GetConfigAsync(ct);

    /// <summary>Salva a ordem, os widgets ativos e o período padrão do dashboard.</summary>
    /// <remarks>Widgets não enviados são mantidos no final, desativados.</remarks>
    [HttpPut("config")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<DashboardConfigDto> UpdateConfig(UpdateDashboardConfigRequest request, CancellationToken ct) =>
        dashboard.UpdateConfigAsync(request, ct);

    /// <summary>Restaura o layout padrão (widgets e período) do modelo do dashboard.</summary>
    [HttpPost("config/reset")]
    public Task<DashboardConfigDto> ResetConfig(CancellationToken ct) => dashboard.ResetConfigAsync(ct);
}
