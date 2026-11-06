using System.Collections.Concurrent;
using Datavello.Api.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Datavello.Tests.Infrastructure;

/// <summary>Runs the real API in memory against the test database.</summary>
/// <param name="authPermitPerMinute">Login/registration rate limit. Tests register many users from one
/// in-memory "IP", so it is high by default; rate-limit tests pass a small value.</param>
public sealed class DatavelloApiFactory(TestDatabase database, int authPermitPerMinute = 100_000) : WebApplicationFactory<Program>
{
    public const string PublicUrl = "http://datavello.test";

    /// <summary>E-mails "sent" by this API instance (captured instead of going to SMTP).</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", database.ConnectionString);
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", authPermitPerMinute.ToString());
        builder.UseSetting("App:PublicUrl", PublicUrl);
        // Validate the security stamp on every request so session invalidation is observable in tests.
        builder.UseSetting("Auth:SecurityStampValidationSeconds", "0");
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails)));
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Waits for the background dispatcher to deliver an e-mail to <paramref name="to"/>.</summary>
    public async Task<EmailMessage> WaitForAsync(string to, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            var found = _sent.LastOrDefault(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"No e-mail was sent to {to}.");
    }
}
