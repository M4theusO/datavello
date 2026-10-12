using System.Net.Http.Json;
using Datavello.Api.Features.Auth;

namespace Datavello.Tests.Infrastructure;

public static class ApiClient
{
    /// <summary>HTTP client with its own cookie jar and the CSRF header the frontend always sends.</summary>
    public static HttpClient CreateBrowserLikeClient(this DatavelloApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "datavello");
        return client;
    }

    public static RegisterRequest NewRegistration(string companyName = "Empresa Teste") => new()
    {
        FullName = "Usuária Teste",
        Email = $"teste-{Guid.NewGuid():N}@datavello.test",
        Password = "SenhaForte123",
        CompanyName = companyName
    };

    /// <summary>Registers a new user (+ company) and returns a signed-in client.</summary>
    public static async Task<(HttpClient Client, MeResponse Me)> RegisterAsync(this DatavelloApiFactory factory, string companyName = "Empresa Teste")
    {
        var client = factory.CreateBrowserLikeClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(companyName));
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(Json.Options);
        return (client, me!);
    }
}

public static class Json
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
