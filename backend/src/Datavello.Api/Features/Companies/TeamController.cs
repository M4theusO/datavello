using System.Security.Claims;
using Datavello.Api.Features.Auth;
using Datavello.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Datavello.Api.Features.Companies;

/// <summary>Members of the active company and its pending invitations.</summary>
[ApiController]
[Route("api/company")]
[Produces("application/json")]
public sealed class TeamController(TeamService team) : ControllerBase
{
    /// <summary>Usuários com acesso à empresa ativa.</summary>
    [HttpGet("members")]
    public Task<List<MemberDto>> Members(CancellationToken ct) => team.ListMembersAsync(ct);

    /// <summary>Remove um membro. Apenas o proprietário; o proprietário não pode ser removido.</summary>
    [HttpDelete("members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken ct)
    {
        await team.RemoveMemberAsync(userId, ct);
        return NoContent();
    }

    /// <summary>O usuário atual deixa a empresa ativa (não disponível para o proprietário).</summary>
    [HttpPost("leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        await team.LeaveAsync(ct);
        return NoContent();
    }

    /// <summary>Convites ainda não aceitos (inclui expirados). Apenas o proprietário.</summary>
    [HttpGet("invitations")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<List<InvitationDto>> Invitations(CancellationToken ct) => team.ListInvitationsAsync(ct);

    /// <summary>
    /// Cria um convite (válido por 7 dias) e devolve o token do link — exibido somente nesta resposta.
    /// Um convite aberto para o mesmo e-mail é substituído.
    /// </summary>
    [HttpPost("invitations")]
    [ProducesResponseType<CreatedInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedInvitationDto>> CreateInvitation(CreateInvitationRequest request, CancellationToken ct)
    {
        var created = await team.CreateInvitationAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Cancela um convite pendente.</summary>
    [HttpDelete("invitations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken ct)
    {
        await team.RevokeInvitationAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Invitee side of an invitation. The token travels in the body so it stays out of URLs and access logs.</summary>
[ApiController]
[Route("api/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(TeamService team, AuthService auth) : ControllerBase
{
    /// <summary>Dados públicos de um convite válido (empresa, e-mail convidado, quem convidou).</summary>
    [HttpPost("preview")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimiting.Policy)]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public Task<InvitationPreviewDto> Preview(InvitationTokenRequest request, CancellationToken ct) =>
        team.PreviewAsync(request.Token, ct);

    /// <summary>Aceita o convite com a conta autenticada (o e-mail precisa ser o do convite).</summary>
    [HttpPost("accept")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone)]
    public async Task<MeResponse> Accept(InvitationTokenRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var companyId = await team.AcceptAsync(request.Token, userId, ct);
        return await auth.GetMeAsync(userId, companyId, ct);
    }
}

/// <summary>Companies of the signed-in user.</summary>
[ApiController]
[Route("api/companies")]
[Produces("application/json")]
public sealed class CompaniesController(AuthService auth) : ControllerBase
{
    /// <summary>Cria outra empresa (com categorias e dashboard padrão); o usuário é o proprietário.</summary>
    [HttpPost]
    [ProducesResponseType<MeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MeResponse>> Create(CreateCompanyRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var me = await auth.CreateCompanyAsync(userId, request.Name, ct);
        return StatusCode(StatusCodes.Status201Created, me);
    }
}
