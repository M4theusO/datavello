using Datavello.Api.Infrastructure.Errors;

namespace Datavello.Api.Infrastructure.Tenancy;

/// <summary>
/// The company the current request operates on. Resolved server-side from the
/// authenticated user's validated membership — never taken directly from the client.
/// </summary>
public interface ICurrentTenant
{
    Guid? CompanyId { get; }
    Guid? UserId { get; }

    /// <summary>Returns the active company or throws 403 when there is none.</summary>
    Guid RequireCompanyId();
}

/// <summary>Scoped per request. Only the tenant resolution step (or trusted system code) sets it.</summary>
public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? CompanyId { get; private set; }
    public Guid? UserId { get; private set; }

    public void Set(Guid companyId, Guid? userId = null)
    {
        CompanyId = companyId;
        UserId = userId;
    }

    public Guid RequireCompanyId() =>
        CompanyId ?? throw AppException.Forbidden("Nenhuma empresa ativa para este usuário.");
}
