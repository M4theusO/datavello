using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;

namespace Datavello.Api.Features.Auth;

public sealed class RegisterRequest
{
    /// <example>Maria Silva</example>
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 150 caracteres.")]
    public string FullName { get; init; } = string.Empty;

    /// <example>maria@empresa.com.br</example>
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256, ErrorMessage = "E-mail muito longo.")]
    public string Email { get; init; } = string.Empty;

    /// <example>SenhaForte123</example>
    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    public string Password { get; init; } = string.Empty;

    /// <example>Padaria Pão Quente</example>
    [Required(ErrorMessage = "Informe o nome da empresa.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da empresa deve ter entre 2 e 150 caracteres.")]
    public string CompanyName { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    /// <example>maria@empresa.com.br</example>
    [Required(ErrorMessage = "Informe o e-mail.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; init; } = string.Empty;

    /// <summary>Keeps the session after the browser is closed.</summary>
    public bool RememberMe { get; init; }
}

public sealed record UserDto(Guid Id, string FullName, string Email);

public sealed record CompanySummaryDto(Guid Id, string Name, CompanyRole Role);

/// <summary>Authenticated user, the active company and every company the user belongs to.</summary>
public sealed record MeResponse(UserDto User, CompanySummaryDto? ActiveCompany, IReadOnlyList<CompanySummaryDto> Companies);
