using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Datavello.Tests.Infrastructure;

/// <summary>Runs the real API in memory against the test database.</summary>
/// <param name="authPermitPerMinute">Login/registration rate limit. Tests register many users from one
/// in-memory "IP", so it is high by default; rate-limit tests pass a small value.</param>
public sealed class DatavelloApiFactory(TestDatabase database, int authPermitPerMinute = 100_000) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", authPermitPerMinute.ToString());
        // Validate the security stamp on every request so session invalidation is observable in tests.
        builder.UseSetting("Auth:SecurityStampValidationSeconds", "0");
    }
}
