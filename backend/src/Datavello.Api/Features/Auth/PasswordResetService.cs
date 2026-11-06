using System.ComponentModel.DataAnnotations;
using System.Text;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Email;
using Datavello.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Features.Auth;

public sealed class ForgotPasswordRequest
{
    /// <example>maria@empresa.com.br</example>
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256, ErrorMessage = "E-mail muito longo.")]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "Link inválido.")]
    public Guid UserId { get; init; }

    [Required(ErrorMessage = "Link inválido.")]
    [StringLength(2000, ErrorMessage = "Link inválido.")]
    public string Token { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// "Esqueci minha senha": a single-use, time-limited Identity token sent by e-mail as a link
/// to the web app. The answer never reveals whether the e-mail has an account.
/// </summary>
public sealed class PasswordResetService(
    UserManager<AppUser> users,
    EmailQueue emails,
    IOptions<AppUrlSettings> appUrl,
    IOptions<DataProtectionTokenProviderOptions> tokenOptions,
    ILogger<PasswordResetService> logger)
{
    public async Task RequestAsync(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            logger.LogInformation("Password reset requested for an unknown e-mail.");
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = appUrl.Value.Link($"/redefinir-senha?uid={user.Id}&token={encoded}");

        emails.Enqueue(EmailTemplates.PasswordReset(user.Email!, user.FullName, link, tokenOptions.Value.TokenLifespan));
        logger.LogInformation("Password reset e-mail queued for user {UserId}.", user.Id);
    }

    /// <summary>Sets the new password. Other sessions stop working (security stamp rotates) and a lockout is cleared.</summary>
    public async Task ResetAsync(ResetPasswordRequest request)
    {
        var invalidLink = AppException.BadRequest("Este link é inválido ou expirou. Peça uma nova redefinição de senha.");

        var user = await users.FindByIdAsync(request.UserId.ToString()) ?? throw invalidLink;

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            throw invalidLink;
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                throw invalidLink;
            }

            throw AppException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description).Distinct()));
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }
}
