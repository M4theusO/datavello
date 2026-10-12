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
