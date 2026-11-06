using System.ComponentModel.DataAnnotations;
using System.Text;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Email;
using Datavello.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Features.Profile;

public sealed class RequestEmailChangeRequest
{
    /// <example>maria.nova@empresa.com.br</example>
    [Required(ErrorMessage = "Informe o novo e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256, ErrorMessage = "E-mail muito longo.")]
    public string NewEmail { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha atual.")]
    public string CurrentPassword { get; init; } = string.Empty;
}

public sealed class ConfirmEmailChangeRequest
{
    [Required(ErrorMessage = "Link inválido.")]
    public Guid UserId { get; init; }

    [Required(ErrorMessage = "Link inválido.")]
    [EmailAddress(ErrorMessage = "Link inválido.")]
    [StringLength(256, ErrorMessage = "Link inválido.")]
    public string NewEmail { get; init; } = string.Empty;

    [Required(ErrorMessage = "Link inválido.")]
    [StringLength(2000, ErrorMessage = "Link inválido.")]
    public string Token { get; init; } = string.Empty;
}

public sealed record EmailChangedDto(string Email);

/// <summary>
/// Changing the sign-in e-mail: the current password is required, a confirmation link goes to the
/// NEW address and nothing changes until it is used. Afterwards the old address is notified and all
/// sessions end (the security stamp rotates). The e-mail is also the user name used to sign in.
/// </summary>
public sealed class EmailChangeService(
    AppDbContext db,
    UserManager<AppUser> users,
    EmailQueue emails,
    IOptions<AppUrlSettings> appUrl,
    IOptions<DataProtectionTokenProviderOptions> tokenOptions,
    ILogger<EmailChangeService> logger)
{
    public async Task RequestAsync(AppUser user, RequestEmailChangeRequest request)
    {
        if (!await users.CheckPasswordAsync(user, request.CurrentPassword))
        {
            throw AppException.BadRequest("Senha atual incorreta.");
        }

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(users.NormalizeEmail(newEmail), user.NormalizedEmail, StringComparison.Ordinal))
        {
            throw AppException.BadRequest("Informe um e-mail diferente do atual.");
        }

        // Registration already tells whether an e-mail has an account, so this does not leak anything new.
        if (await users.FindByEmailAsync(newEmail) is not null)
        {
            throw AppException.Conflict("Este e-mail já está em uso por outra conta.");
        }

        var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
        var link = appUrl.Value.Link(
            $"/confirmar-email?uid={user.Id}&email={Uri.EscapeDataString(newEmail)}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}");

        emails.Enqueue(EmailTemplates.ConfirmEmailChange(newEmail, user.FullName, link, tokenOptions.Value.TokenLifespan));
        logger.LogInformation("E-mail change confirmation queued for user {UserId}.", user.Id);
    }

    public async Task<EmailChangedDto> ConfirmAsync(ConfirmEmailChangeRequest request)
    {
        var invalidLink = AppException.BadRequest("Este link é inválido ou expirou. Peça a troca de e-mail novamente no seu perfil.");

        var user = await users.FindByIdAsync(request.UserId.ToString()) ?? throw invalidLink;
        var newEmail = request.NewEmail.Trim();

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            throw invalidLink;
        }

        var oldEmail = user.Email!;

        // E-mail and user name (used to sign in) change together or not at all.
        await using var tx = await db.Database.BeginTransactionAsync();

        var changed = await users.ChangeEmailAsync(user, newEmail, token);
        if (!changed.Succeeded)
        {
            throw ToException(changed, invalidLink);
        }

        var renamed = await users.SetUserNameAsync(user, newEmail);
        if (!renamed.Succeeded)
        {
            throw ToException(renamed, invalidLink);
        }

        await tx.CommitAsync();

        emails.Enqueue(EmailTemplates.EmailChangedNotice(oldEmail, user.FullName, newEmail, appUrl.Value.Link("/login")));
        logger.LogInformation("E-mail changed for user {UserId}.", user.Id);
        return new EmailChangedDto(newEmail);
    }

    private static AppException ToException(IdentityResult result, AppException invalidLink)
    {
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return invalidLink;
        }

        return result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
            ? AppException.Conflict("Este e-mail já está em uso por outra conta.")
            : AppException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description).Distinct()));
    }
}
