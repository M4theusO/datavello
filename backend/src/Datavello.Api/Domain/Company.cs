namespace Datavello.Api.Domain;

public class Company : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional CNPJ/CPF, stored as typed by the user.</summary>
    public string? Document { get; set; }

    public string Currency { get; set; } = "BRL";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CompanyMember> Members { get; set; } = [];
}

public enum CompanyRole
{
    Owner,
    Member
}

public class CompanyMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public CompanyRole Role { get; set; } = CompanyRole.Member;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Invitation for someone to join a company as a member. Delivered as a link the owner
/// shares; only the SHA-256 hash of the link token is stored.
/// </summary>
public class CompanyInvitation : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }

    /// <summary>E-mail the invitation is addressed to; only an account with this e-mail can accept it.</summary>
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>Hex SHA-256 of the token in the link.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public CompanyRole Role { get; set; } = CompanyRole.Member;
    public Guid InvitedByUserId { get; set; }
    public AppUser InvitedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
}
