using Datavello.Api.Domain;
using Datavello.Api.Features.Companies;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    CompanyProvisioningService provisioning)
{
    /// <summary>Creates the user and their company atomically, then signs the user in.</summary>
    public async Task<MeResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = new AppUser { UserName = email, Email = email, FullName = request.FullName.Trim() };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw ToException(result);
        }

        var company = await provisioning.ProvisionAsync(user, request.CompanyName, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await signInManager.SignInAsync(user, isPersistent: false);
        return await GetMeAsync(user.Id, company.Id, ct);
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
        return new MeResponse(new UserDto(user.Id, user.FullName, user.Email!), active, companies);
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
