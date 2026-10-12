using System.Net;
using System.Text.RegularExpressions;
using Datavello.Tests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Datavello.Tests.Api;

/// <summary>Regression guard: every API endpoint requires authentication unless explicitly allowed here.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class EndpointSecurityTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    private static readonly HashSet<string> PublicEndpoints =
    [
        "GET api/health",
        "POST api/auth/register",
        "POST api/auth/login",
        "POST api/auth/logout"
    ];

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();

    [Fact]
    public async Task Every_non_public_endpoint_rejects_anonymous_requests()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (Method: method, Route: e.RoutePattern.RawText!)))
            .ToList();

        Assert.True(endpoints.Count > 20, $"expected to discover the API endpoints, found {endpoints.Count}");

        var client = _factory.CreateBrowserLikeClient(); // anonymous, but with the CSRF header
        var unprotected = new List<string>();
        foreach (var (method, route) in endpoints)
        {
            var key = $"{method} {route}";
            if (PublicEndpoints.Contains(key))
            {
                continue;
            }

            var url = "/" + RouteParameter().Replace(route, Guid.NewGuid().ToString());
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (method is "POST" or "PUT")
            {
                request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                unprotected.Add($"{key} -> {(int)response.StatusCode}");
            }
        }

        Assert.Empty(unprotected);
    }

    public void Dispose() => _factory.Dispose();
}
