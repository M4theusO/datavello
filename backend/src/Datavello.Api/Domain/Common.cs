namespace Datavello.Api.Domain;

/// <summary>Business data that belongs to exactly one company (tenant).</summary>
public interface ITenantOwned
{
    Guid CompanyId { get; set; }
}

/// <summary>Entities whose CreatedAt/UpdatedAt are maintained by the DbContext.</summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
