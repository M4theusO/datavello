using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Datavello.Api.Features.Auth;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed partial class PasswordResetTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [GeneratedRegex(@"http://datavello\.test/redefinir-senha\?[^\s""<]+")]
    private static partial Regex ResetLink();

    private static (Guid UserId, string Token) ParseLink(string body)
    {
        var match = ResetLink().Match(body);
        Assert.True(match.Success, "e-mail should contain the reset link");
        var query = HttpUtility.ParseQueryString(new Uri(match.Value).Query);
        return (Guid.Parse(query["uid"]!), query["token"]!);
    }

    private async Task<(Guid UserId, string Token)> RequestResetAsync(string email)
    {
        var response = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = await _factory.Emails.WaitForAsync(email, "Redefinição de senha");
        Assert.Equal("Redefinição de senha — Datavello", message.Subject);
        return ParseLink(message.TextBody);
    }

    [Fact]
    public async Task User_resets_the_password_through_the_e_mailed_link()
    {
        var (oldSession, me) = await _factory.RegisterAsync();
        var (userId, token) = await RequestResetAsync(me.User.Email);
        Assert.Equal(me.User.Id, userId);

        var reset = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "NovaSenha456" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // New password works, old one does not, and existing sessions are signed out.
        var client = _factory.CreateBrowserLikeClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostJsonAsync("/api/auth/login", new { email = me.User.Email, password = "SenhaForte123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostJsonAsync("/api/auth/login", new { email = me.User.Email, password = "NovaSenha456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/auth/me")).StatusCode);

        // Single use.
        var again = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "OutraSenha789" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("inválido ou expirou", await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_and_no_e_mail()
    {
        var email = $"ninguem-{Guid.NewGuid():N}@datavello.test";

        var response = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await Assert.ThrowsAsync<TimeoutException>(() => _factory.Emails.WaitForAsync(email, timeout: TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Tampered_or_foreign_tokens_are_rejected()
    {
        var (_, alice) = await _factory.RegisterAsync();
        var (_, bob) = await _factory.RegisterAsync();
        var (_, aliceToken) = await RequestResetAsync(alice.User.Email);
        var anonymous = _factory.CreateBrowserLikeClient();

        // Alice's token cannot reset Bob's password.
        var foreign = await anonymous.PostJsonAsync("/api/auth/reset-password", new { userId = bob.User.Id, token = aliceToken, newPassword = "Invasao123" });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);

        var garbage = await anonymous.PostJsonAsync("/api/auth/reset-password", new { userId = alice.User.Id, token = "%%%nao-base64%%%", newPassword = "Invasao123" });
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);

        var unknownUser = await anonymous.PostJsonAsync("/api/auth/reset-password", new { userId = Guid.NewGuid(), token = aliceToken, newPassword = "Invasao123" });
        Assert.Equal(HttpStatusCode.BadRequest, unknownUser.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await anonymous.PostJsonAsync("/api/auth/login", new { email = bob.User.Email, password = "SenhaForte123" })).StatusCode);
    }

    [Fact]
    public async Task Weak_new_password_is_rejected_and_the_link_stays_valid()
    {
        var (_, me) = await _factory.RegisterAsync();
        var (userId, token) = await RequestResetAsync(me.User.Email);
        var client = _factory.CreateBrowserLikeClient();

        var weak = await client.PostJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "curta" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.DoesNotContain("inválido ou expirou", await weak.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "SenhaBoa123" })).StatusCode);
    }

    [Fact]
    public async Task Reset_unlocks_an_account_locked_by_failed_logins()
    {
        var (_, me) = await _factory.RegisterAsync();
        var client = _factory.CreateBrowserLikeClient();
        for (var i = 0; i < 5; i++)
        {
            await client.PostJsonAsync("/api/auth/login", new LoginRequest { Email = me.User.Email, Password = "Errada000" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostJsonAsync("/api/auth/login", new { email = me.User.Email, password = "SenhaForte123" })).StatusCode);

        var (userId, token) = await RequestResetAsync(me.User.Email);
        await client.PostJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "NovaSenha456" });

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostJsonAsync("/api/auth/login", new { email = me.User.Email, password = "NovaSenha456" })).StatusCode);
    }

    [Fact]
    public async Task E_mail_html_escapes_the_user_name()
    {
        var email = $"html-{Guid.NewGuid():N}@datavello.test";
        await _factory.RegisterAsync(new
        {
            fullName = "<script>alert(1)</script>",
            email,
            password = "SenhaForte123",
            companyName = "Empresa"
        });

        await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/forgot-password", new { email });
        var message = await _factory.Emails.WaitForAsync(email, "Redefinição de senha");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    public void Dispose() => _factory.Dispose();
}
