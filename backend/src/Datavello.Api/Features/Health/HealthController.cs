using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Health;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Verifica se a API está no ar.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => new HealthResponse("ok", DateTimeOffset.UtcNow);
}

public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);
