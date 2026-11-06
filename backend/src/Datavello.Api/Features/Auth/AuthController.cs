using System.Security.Claims;
using Datavello.Api.Features.Profile;
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
    /// <summary>
    /// Cria a conta e a empresa (com categorias e dashboard padrão) — ou entra na empresa do convite — e
    /// envia o e-mail de confirmação. A sessão só começa depois da confirmação.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<RegisterAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterAcceptedResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var accepted = await auth.RegisterAsync(request, ct);
        return Accepted(accepted);
    }

    /// <summary>Confirma o e-mail pelo link recebido e inicia a sessão. Link de e-mail já confirmado responde 409.</summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<MeResponse> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct) => auth.ConfirmEmailAsync(request, ct);

    /// <summary>Reenvia o e-mail de confirmação. Sempre 204, para não revelar quais e-mails têm conta.</summary>
    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        await auth.ResendConfirmationAsync(request);
        return NoContent();
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

    /// <summary>
    /// Envia um link de redefinição de senha, se o e-mail tiver conta. A resposta é sempre 204,
    /// para não revelar quais e-mails estão cadastrados.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, [FromServices] PasswordResetService passwords)
    {
        await passwords.RequestAsync(request);
        return NoContent();
    }

    /// <summary>Define a nova senha a partir do link recebido por e-mail. Outras sessões abertas deixam de valer.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, [FromServices] PasswordResetService passwords)
    {
        await passwords.ResetAsync(request);
        return NoContent();
    }

    /// <summary>
    /// Confirma a troca de e-mail pelo link enviado ao novo endereço (funciona sem login, inclusive em
    /// outro dispositivo). Todas as sessões são encerradas; o próximo login usa o novo e-mail.
    /// </summary>
    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<EmailChangedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<EmailChangedDto> ConfirmEmailChange(ConfirmEmailChangeRequest request, [FromServices] EmailChangeService emailChange)
    {
        var result = await emailChange.ConfirmAsync(request);
        // The current browser's session belongs to the old identity: end it here as well.
        await auth.LogoutAsync();
        return result;
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
