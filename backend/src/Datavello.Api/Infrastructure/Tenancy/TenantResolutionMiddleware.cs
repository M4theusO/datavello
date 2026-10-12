using System.Security.Claims;
using Datavello.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Infrastructure.Tenancy;

/// <summary>
/// Resolves the active company for authenticated requests from the user's memberships.
/// The optional X-Company-Id header only selects among companies the user belongs to;
/// any other value is rejected with 403.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Company-Id";

    public async Task InvokeAsync(HttpContext context, CurrentTenant tenant, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var companyIds = await db.CompanyMembers.AsNoTracking()
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.CompanyId)
                .ToListAsync(context.RequestAborted);

            Guid? selected = companyIds.Count > 0 ? companyIds[0] : null;

            var requested = context.Request.Headers[HeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (!Guid.TryParse(requested, out var requestedId) || !companyIds.Contains(requestedId))
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Acesso negado",
                        detail: "Você não tem acesso a esta empresa.").ExecuteAsync(context);
                    return;
                }

                selected = requestedId;
            }

            if (selected is { } companyId)
            {
                tenant.Set(companyId, userId);
            }
        }

        await next(context);
    }
}
