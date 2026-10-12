using Microsoft.AspNetCore.Identity;

namespace Datavello.Api.Domain;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string FullName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CompanyMember> Memberships { get; set; } = [];
}
