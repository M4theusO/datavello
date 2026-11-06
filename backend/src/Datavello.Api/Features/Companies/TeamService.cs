using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Email;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Features.Companies;

/// <summary>
/// Members of the active company and invitations to join it.
/// Invitations are e-mailed to the invitee and the link is also shown to the owner to share; the link token
/// is random, single-use, expires and is stored only as a hash. Only an account whose
/// e-mail matches the invitation can accept it.
/// </summary>
public sealed class TeamService(
    AppDbContext db,
    CurrentTenant tenant,
    ILookupNormalizer normalizer,
    EmailQueue emails,
    IOptions<AppUrlSettings> appUrl)
{
    /// <summary>Web app path of an invitation (the frontend builds the same link for the copy button).</summary>
    public static string InvitationPath(string token) => $"/convite/{Uri.EscapeDataString(token)}";

    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public const int MaxPendingInvitations = 50;

    // ---- Members ----

    public Task<List<MemberDto>> ListMembersAsync(CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        return db.CompanyMembers.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .OrderByDescending(m => m.Role == CompanyRole.Owner)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new MemberDto(m.UserId, m.User.FullName, m.User.Email!, m.Role, m.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid userId, CancellationToken ct)
    {
        var companyId = await RequireOwnerAsync(ct);
        var member = await db.CompanyMembers.SingleOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == userId, ct)
            ?? throw AppException.NotFound("Membro não encontrado.");

        if (member.Role == CompanyRole.Owner)
        {
            throw AppException.BadRequest("Não é possível remover o proprietário da empresa.");
        }

        db.CompanyMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The current user leaves the active company. The owner cannot leave.</summary>
    public async Task LeaveAsync(CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        var member = await db.CompanyMembers.SingleAsync(m => m.CompanyId == companyId && m.UserId == tenant.UserId, ct);
        if (member.Role == CompanyRole.Owner)
        {
            throw AppException.BadRequest("O proprietário não pode sair da empresa.");
        }

        db.CompanyMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    // ---- Invitations (owner side) ----

    public async Task<List<InvitationDto>> ListInvitationsAsync(CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return await db.CompanyInvitations.AsNoTracking()
            .Where(i => i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role, i.InvitedBy.FullName, i.CreatedAt, i.ExpiresAt, i.ExpiresAt <= now))
            .ToListAsync(ct);
    }

    /// <summary>Creates an invitation, replacing any open one for the same e-mail.</summary>
    public async Task<CreatedInvitationDto> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        var companyId = await RequireOwnerAsync(ct);
        var email = request.Email.Trim();
        var normalized = normalizer.NormalizeEmail(email);

        var alreadyMember = await db.CompanyMembers.AnyAsync(m => m.CompanyId == companyId && m.User.NormalizedEmail == normalized, ct);
        if (alreadyMember)
        {
            throw AppException.Conflict("Esta pessoa já faz parte da empresa.");
        }

        var now = DateTimeOffset.UtcNow;
        var open = await db.CompanyInvitations.Where(i => i.AcceptedAt == null).ToListAsync(ct);
        db.CompanyInvitations.RemoveRange(open.Where(i => i.NormalizedEmail == normalized));

        if (open.Count(i => i.NormalizedEmail != normalized && i.ExpiresAt > now) >= MaxPendingInvitations)
        {
            throw AppException.BadRequest($"Limite de {MaxPendingInvitations} convites pendentes atingido. Cancele convites antigos antes de criar novos.");
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var invitation = new CompanyInvitation
        {
            CompanyId = companyId,
            Email = email,
            NormalizedEmail = normalized,
            TokenHash = Hash(token),
            Role = CompanyRole.Member,
            InvitedByUserId = tenant.UserId!.Value,
            CreatedAt = now,
            ExpiresAt = now.Add(InvitationLifetime)
        };
        db.CompanyInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var invitedBy = await db.Users.Where(u => u.Id == invitation.InvitedByUserId).Select(u => u.FullName).SingleAsync(ct);
        var companyName = await db.Companies.Where(c => c.Id == companyId).Select(c => c.Name).SingleAsync(ct);
        emails.Enqueue(EmailTemplates.Invitation(email, companyName, invitedBy, appUrl.Value.Link(InvitationPath(token)), invitation.ExpiresAt));

        return new CreatedInvitationDto(
            new InvitationDto(invitation.Id, invitation.Email, invitation.Role, invitedBy, invitation.CreatedAt, invitation.ExpiresAt, false),
            token);
    }

    public async Task RevokeInvitationAsync(Guid id, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var invitation = await db.CompanyInvitations.SingleOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null, ct)
            ?? throw AppException.NotFound("Convite não encontrado.");

        db.CompanyInvitations.Remove(invitation);
        await db.SaveChangesAsync(ct);
    }

    // ---- Invitations (invitee side) ----

    public async Task<InvitationPreviewDto> PreviewAsync(string token, CancellationToken ct)
    {
        var invitation = await FindUsableAsync(token, ct);
        var companyName = await db.Companies.Where(c => c.Id == invitation.CompanyId).Select(c => c.Name).SingleAsync(ct);
        return new InvitationPreviewDto(companyName, invitation.Email, invitation.InvitedBy.FullName, invitation.ExpiresAt);
    }

    /// <summary>Adds the signed-in user to the inviting company and returns its id.</summary>
    public async Task<Guid> AcceptAsync(string token, Guid userId, CancellationToken ct)
    {
        var invitation = await FindUsableAsync(token, ct);
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (user.NormalizedEmail != invitation.NormalizedEmail)
        {
            throw AppException.Forbidden($"Este convite foi enviado para {invitation.Email}. Entre com essa conta para aceitá-lo.");
        }

        await JoinAsync(invitation, user, ct);
        await db.SaveChangesAsync(ct);
        return invitation.CompanyId;
    }

    /// <summary>
    /// Returns an invitation that can still be accepted, or throws 404 (unknown) / 410 (used or expired).
    /// Looks across companies: the token itself is the proof of access.
    /// </summary>
    public async Task<CompanyInvitation> FindUsableAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token.Trim());
        var invitation = await db.CompanyInvitations.IgnoreQueryFilters()
            .Include(i => i.InvitedBy)
            .SingleOrDefaultAsync(i => i.TokenHash == hash, ct)
            ?? throw AppException.NotFound("Convite não encontrado. Confira o link ou peça um novo convite.");

        if (invitation.AcceptedAt is not null)
        {
            throw AppException.Gone("Este convite já foi utilizado.");
        }

        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw AppException.Gone("Este convite expirou. Peça um novo convite ao proprietário da empresa.");
        }

        return invitation;
    }

    /// <summary>Marks the invitation as used and adds the membership. The caller saves.</summary>
    public async Task JoinAsync(CompanyInvitation invitation, AppUser user, CancellationToken ct)
    {
        // The validated invitation grants access to its company: write inside that tenant.
        tenant.Set(invitation.CompanyId, user.Id);

        var alreadyMember = await db.CompanyMembers.AnyAsync(m => m.CompanyId == invitation.CompanyId && m.UserId == user.Id, ct);
        if (!alreadyMember)
        {
            db.CompanyMembers.Add(new CompanyMember { CompanyId = invitation.CompanyId, UserId = user.Id, Role = invitation.Role });
        }

        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        invitation.AcceptedByUserId = user.Id;
    }

    private async Task<Guid> RequireOwnerAsync(CancellationToken ct)
    {
        var companyId = tenant.RequireCompanyId();
        var isOwner = await db.CompanyMembers.AnyAsync(
            m => m.CompanyId == companyId && m.UserId == tenant.UserId && m.Role == CompanyRole.Owner, ct);

        return isOwner ? companyId : throw AppException.Forbidden("Apenas o proprietário pode gerenciar a equipe da empresa.");
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
