using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Datavello.Api.Features.Auth;

namespace Datavello.Tests.Infrastructure;

public static partial class ApiClient
{
    public const string ConfirmationSubject = "Confirme seu e-mail — Datavello";

    [GeneratedRegex(@"/confirmar-cadastro\?[^\s""<]+")]
    private static partial Regex ConfirmationLink();

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

    /// <summary>Registers a new user (+ company), confirms the e-mail and returns a signed-in client.</summary>
    public static async Task<(HttpClient Client, MeResponse Me)> RegisterAsync(this DatavelloApiFactory factory, string companyName = "Empresa Teste") =>
        await factory.RegisterAsync(NewRegistration(companyName));

    /// <summary>Registers <paramref name="registration"/>, confirms the e-mail and returns the signed-in client.</summary>
    public static async Task<(HttpClient Client, MeResponse Me)> RegisterAsync(this DatavelloApiFactory factory, object registration)
    {
        var client = factory.CreateBrowserLikeClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", registration, Json.Options);
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            throw new HttpRequestException($"Expected 202, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var accepted = (await response.Content.ReadFromJsonAsync<RegisterAcceptedResponse>(Json.Options))!;
        var me = await factory.ConfirmEmailAsync(client, accepted.Email);
        return (client, me);
    }

    /// <summary>Follows the confirmation link e-mailed to <paramref name="email"/> with <paramref name="client"/> (which becomes signed in).</summary>
    public static async Task<MeResponse> ConfirmEmailAsync(this DatavelloApiFactory factory, HttpClient client, string email)
    {
        var (userId, token) = await factory.ConfirmationLinkAsync(email);
        return await (await client.PostAsJsonAsync("/api/auth/confirm-email", new { userId, token }, Json.Options)).ReadAsync<MeResponse>();
    }

    public static async Task<(Guid UserId, string Token)> ConfirmationLinkAsync(this DatavelloApiFactory factory, string email)
    {
        var message = await factory.Emails.WaitForAsync(email, ConfirmationSubject);
        var match = ConfirmationLink().Match(message.TextBody);
        if (!match.Success)
        {
            throw new InvalidOperationException("Confirmation e-mail without a link.");
        }

        var query = HttpUtility.ParseQueryString(new Uri("http://x" + match.Value).Query);
        return (Guid.Parse(query["uid"]!), query["token"]!);
    }
}

public static class Json
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
