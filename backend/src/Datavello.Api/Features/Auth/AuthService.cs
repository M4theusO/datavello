using System.Text;
using Datavello.Api.Domain;
using Datavello.Api.Features.Companies;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Email;
using Datavello.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    CompanyProvisioningService provisioning,
    TeamService team,
    EmailQueue emails,
    IOptions<AppUrlSettings> appUrl,
    IOptions<DataProtectionTokenProviderOptions> tokenOptions,
    ILogger<AuthService> logger)
{
    /// <summary>Companies a user may own (the one created at registration included).</summary>
    public const int MaxOwnedCompanies = 3;

    /// <summary>
    /// Creates the user and, atomically, either a new company (owner) or — with an invite
    /// token — the membership in the inviting company. The account can only sign in after
    /// confirming the e-mail through the link that is sent here.
    /// </summary>
    public async Task<RegisterAcceptedResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = new AppUser { UserName = email, Email = email, FullName = request.FullName.Trim() };

        CompanyInvitation? invitation = null;
        if (!string.IsNullOrWhiteSpace(request.InviteToken))
        {
            invitation = await team.FindUsableAsync(request.InviteToken, ct);
            if (userManager.NormalizeEmail(email) != invitation.NormalizedEmail)
            {
                throw AppException.BadRequest($"Este convite foi enviado para {invitation.Email}. Use esse e-mail no cadastro.");
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw ToException(result);
        }

        if (invitation is not null)
        {
            await team.JoinAsync(invitation, user, ct);
        }
        else
        {
            await provisioning.ProvisionAsync(user, request.CompanyName!, ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await SendConfirmationAsync(user);
        return new RegisterAcceptedResponse(user.Email!);
    }

    /// <summary>
    /// Confirms the e-mail from the link and signs the user in. A link for an address that is
    /// already confirmed does not sign anyone in (409), so a leaked link is not a login link.
    /// </summary>
    public async Task<MeResponse> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var invalidLink = AppException.BadRequest("Este link de confirmação é inválido ou expirou. Peça um novo para continuar.");

        var user = await userManager.FindByIdAsync(request.UserId.ToString()) ?? throw invalidLink;
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            throw invalidLink;
        }

        if (user.EmailConfirmed)
        {
            var valid = await userManager.VerifyUserTokenAsync(
                user, userManager.Options.Tokens.EmailConfirmationTokenProvider, UserManager<AppUser>.ConfirmEmailTokenPurpose, token);
            throw valid ? AppException.Conflict("Este e-mail já foi confirmado. Entre com seu e-mail e senha.") : invalidLink;
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            throw invalidLink;
        }

        logger.LogInformation("E-mail confirmed for user {UserId}.", user.Id);
        await signInManager.SignInAsync(user, isPersistent: false);
        return await GetMeAsync(user.Id, activeCompanyId: null, ct);
    }

    /// <summary>Sends a new confirmation link if the e-mail has an unconfirmed account. Never reveals which is the case.</summary>
    public async Task ResendConfirmationAsync(ResendConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false })
        {
            await SendConfirmationAsync(user);
        }
    }

    /// <summary>Creates another company owned by the user (it becomes the active one in the response).</summary>
    public async Task<MeResponse> CreateCompanyAsync(Guid userId, string name, CancellationToken ct)
    {
        if (await OwnedCompaniesAsync(userId, ct) >= MaxOwnedCompanies)
        {
            throw AppException.BadRequest($"Você atingiu o limite de {MaxOwnedCompanies} empresas próprias por usuário.");
        }

        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var company = await provisioning.ProvisionAsync(user, name, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetMeAsync(userId, company.Id, ct);
    }

    public async Task<MeResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var result = await signInManager.PasswordSignInAsync(email, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new AppException(StatusCodes.Status429TooManyRequests, "Acesso bloqueado temporariamente",
                "Muitas tentativas de login. Aguarde alguns minutos e tente novamente.");
        }

        if (result.IsNotAllowed)
        {
            // Identity refuses unconfirmed accounts before checking the password. Check it here, so
            // only someone who knows the password learns that the account exists and is unconfirmed.
            var pending = await userManager.FindByNameAsync(email);
            if (pending is not null && await userManager.IsLockedOutAsync(pending))
            {
                throw new AppException(StatusCodes.Status429TooManyRequests, "Acesso bloqueado temporariamente",
                    "Muitas tentativas de login. Aguarde alguns minutos e tente novamente.");
            }

            if (pending is not null && await userManager.CheckPasswordAsync(pending, request.Password))
            {
                throw new AppException(StatusCodes.Status403Forbidden, "E-mail não confirmado",
                    "Confirme seu e-mail para entrar. Enviamos um link de confirmação no cadastro; se não o encontrar, peça um novo.");
            }

            if (pending is not null)
            {
                await userManager.AccessFailedAsync(pending);
            }
        }

        if (!result.Succeeded)
        {
            // Same message for unknown e-mail and wrong password: does not reveal which accounts exist.
            throw new AppException(StatusCodes.Status401Unauthorized, "Falha no login", "E-mail ou senha incorretos.");
        }

        var user = await userManager.FindByNameAsync(email) ?? throw AppException.NotFound();
        return await GetMeAsync(user.Id, activeCompanyId: null, ct);
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<MeResponse> GetMeAsync(Guid userId, Guid? activeCompanyId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AppException(StatusCodes.Status401Unauthorized, "Sessão inválida", "Faça login novamente.");

        var companies = await db.CompanyMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new CompanySummaryDto(m.CompanyId, m.Company.Name, m.Role))
            .ToListAsync(ct);

        var active = companies.FirstOrDefault(c => c.Id == activeCompanyId) ?? companies.FirstOrDefault();
        var canCreateCompany = companies.Count(c => c.Role == CompanyRole.Owner) < MaxOwnedCompanies;
        return new MeResponse(new UserDto(user.Id, user.FullName, user.Email!), active, companies, canCreateCompany, MaxOwnedCompanies);
    }

    private Task<int> OwnedCompaniesAsync(Guid userId, CancellationToken ct) =>
        db.CompanyMembers.CountAsync(m => m.UserId == userId && m.Role == CompanyRole.Owner, ct);

    private async Task SendConfirmationAsync(AppUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = appUrl.Value.Link($"/confirmar-cadastro?uid={user.Id}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}");
        emails.Enqueue(EmailTemplates.ConfirmRegistration(user.Email!, user.FullName, link, tokenOptions.Value.TokenLifespan));
        logger.LogInformation("Registration confirmation e-mail queued for user {UserId}.", user.Id);
    }

    private static AppException ToException(IdentityResult result)
    {
        var errors = result.Errors.ToList();
        var message = string.Join(" ", errors.Select(e => e.Description).Distinct());

        return errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
            ? AppException.Conflict(message)
            : AppException.BadRequest(message);
    }
}
