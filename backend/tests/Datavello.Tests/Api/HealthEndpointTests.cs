using System.Net;
using System.Net.Http.Json;
using Datavello.Api.Features.Health;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class HealthEndpointTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task Unknown_route_requires_authentication_and_returns_problem_details()
    {
        var anonymous = await _factory.CreateClient().GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);

        var (client, _) = await _factory.RegisterAsync();
        var authenticated = await client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, authenticated.StatusCode);
        Assert.Equal("application/problem+json", authenticated.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose() => _factory.Dispose();
}
