using System.Security.Claims;
using Datavello.Api.Infrastructure.Security;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Datavello.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, ICurrentTenant tenant) : ControllerBase
{
    /// <summary>Cria a conta, a empresa (com categorias e dashboard padrão) e inicia a sessão.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MeResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var me = await auth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), me);
    }

    /// <summary>Inicia a sessão (cookie HttpOnly).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public Task<MeResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Encerra a sessão.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync();
        return NoContent();
    }

    /// <summary>Usuário autenticado e empresa ativa.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<MeResponse> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return auth.GetMeAsync(userId, tenant.CompanyId, ct);
    }
}
