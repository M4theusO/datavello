using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Profile;

public sealed record ProfileDto(Guid Id, string FullName, string Email, DateTimeOffset CreatedAt);

public sealed class UpdateProfileRequest
{
    /// <example>Maria Silva</example>
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 150 caracteres.")]
    public string FullName { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>The authenticated user's own data.</summary>
[ApiController]
[Route("api/profile")]
[Produces("application/json")]
public sealed class ProfileController(UserManager<AppUser> users, SignInManager<AppUser> signIn) : ControllerBase
{
    /// <summary>Dados do usuário autenticado.</summary>
    [HttpGet]
    public async Task<ProfileDto> Get() => ToDto(await CurrentUserAsync());

    /// <summary>Atualiza o nome do usuário.</summary>
    [HttpPut]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ProfileDto> Update(UpdateProfileRequest request)
    {
        var user = await CurrentUserAsync();
        user.FullName = request.FullName.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw AppException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return ToDto(user);
    }

    /// <summary>Troca a senha. Outras sessões abertas deixam de valer; a sessão atual continua.</summary>
    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw AppException.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description).Distinct()));
        }

        // ChangePasswordAsync rotates the security stamp (invalidating other cookies); re-issue ours.
        await signIn.RefreshSignInAsync(user);
        return NoContent();
    }

    private async Task<AppUser> CurrentUserAsync() =>
        await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
        ?? throw new AppException(StatusCodes.Status401Unauthorized, "Sessão inválida", "Faça login novamente.");

    private static ProfileDto ToDto(AppUser user) => new(user.Id, user.FullName, user.Email!, user.CreatedAt);
}
