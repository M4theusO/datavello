using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;

namespace Datavello.Api.Features.Companies;

public sealed record MemberDto(Guid UserId, string FullName, string Email, CompanyRole Role, DateTimeOffset JoinedAt);

public sealed record InvitationDto(
    Guid Id,
    string Email,
    CompanyRole Role,
    string InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpired);

/// <summary>Returned only when the invitation is created: the token is not stored and cannot be read again.</summary>
public sealed record CreatedInvitationDto(InvitationDto Invitation, string Token);

/// <summary>What the invitee sees before accepting.</summary>
public sealed record InvitationPreviewDto(string CompanyName, string Email, string InvitedBy, DateTimeOffset ExpiresAt);

public sealed class CreateInvitationRequest
{
    /// <example>joao@empresa.com.br</example>
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(256, ErrorMessage = "E-mail muito longo.")]
    public string Email { get; init; } = string.Empty;
}

public sealed class InvitationTokenRequest
{
    [Required(ErrorMessage = "Convite inválido.")]
    [StringLength(100, ErrorMessage = "Convite inválido.")]
    public string Token { get; init; } = string.Empty;
}

public sealed class CreateCompanyRequest
{
    /// <example>Padaria Pão Quente — Filial</example>
    [Required(ErrorMessage = "Informe o nome da empresa.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da empresa deve ter entre 2 e 150 caracteres.")]
    public string Name { get; init; } = string.Empty;
}
