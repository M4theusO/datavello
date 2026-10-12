using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Companies;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? Document,
    string Currency,
    CompanyRole Role,
    int MemberCount,
    DateTimeOffset CreatedAt);

public sealed class UpdateCompanyRequest
{
    /// <example>Padaria Pão Quente</example>
    [Required(ErrorMessage = "Informe o nome da empresa.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da empresa deve ter entre 2 e 150 caracteres.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>CPF ou CNPJ (opcional), com ou sem pontuação.</summary>
    /// <example>12.345.678/0001-95</example>
    [StringLength(30, ErrorMessage = "Documento inválido.")]
    public string? Document { get; init; }
}

/// <summary>The active company (resolved from the authenticated membership).</summary>
[ApiController]
[Route("api/company")]
[Produces("application/json")]
public sealed class CompanyController(AppDbContext db, ICurrentTenant tenant) : ControllerBase
{
    /// <summary>Dados da empresa ativa.</summary>
    [HttpGet]
    public Task<CompanyDto> Get(CancellationToken ct) => LoadAsync(ct);

    /// <summary>Atualiza nome e documento. Apenas o proprietário pode alterar.</summary>
    [HttpPut]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<CompanyDto> Update(UpdateCompanyRequest request, CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        var role = await RoleAsync(companyId, ct);
        if (role != CompanyRole.Owner)
        {
            throw AppException.Forbidden("Apenas o proprietário pode alterar os dados da empresa.");
        }

        string? document = null;
        if (!string.IsNullOrWhiteSpace(request.Document))
        {
            var digits = BrazilianDocument.Digits(request.Document);
            if (!BrazilianDocument.IsValid(digits))
            {
                throw AppException.BadRequest("CPF ou CNPJ inválido. Confira os números digitados.");
            }

            document = BrazilianDocument.Format(digits);
        }

        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        company.Name = request.Name.Trim();
        company.Document = document;
        await db.SaveChangesAsync(ct);

        return await LoadAsync(ct);
    }

    private async Task<CompanyDto> LoadAsync(CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        var role = await RoleAsync(companyId, ct);
        return await db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyDto(c.Id, c.Name, c.Document, c.Currency, role, c.Members.Count, c.CreatedAt))
            .SingleAsync(ct);
    }

    private Task<CompanyRole> RoleAsync(Guid companyId, CancellationToken ct) =>
        db.CompanyMembers.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.UserId == tenant.UserId)
            .Select(m => m.Role)
            .SingleAsync(ct);
}
