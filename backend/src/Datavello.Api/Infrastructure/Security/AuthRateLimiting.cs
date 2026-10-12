using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Datavello.Api.Infrastructure.Security;

/// <summary>
/// Per-IP limit for login and registration (brute force / mass sign-up protection),
/// on top of the per-account lockout. Configured by "RateLimiting:AuthPermitPerMinute".
/// </summary>
public static class AuthRateLimiting
{
    public const string Policy = "auth";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas tentativas",
                    Detail = "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente."
                }, options: null, contentType: "application/problem+json", cancellationToken: ct);
            };
        });
    }
}
