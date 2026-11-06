using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Datavello.Api.Domain;
using Datavello.Api.Features.Auth;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed partial class EmailConfirmationTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [GeneratedRegex(@"/redefinir-senha\?[^\s""<]+")]
    private static partial Regex ResetLink();

    private async Task<RegisterRequest> RegisterUnconfirmedAsync()
    {
        var registration = ApiClient.NewRegistration();
        var response = await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return registration;
    }

    private Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

    [Fact]
    public async Task Unconfirmed_account_cannot_sign_in_and_only_the_right_password_reveals_why()
    {
        var registration = await RegisterUnconfirmedAsync();
        var client = _factory.CreateBrowserLikeClient();

        var right = await LoginAsync(client, registration.Email, registration.Password);
        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        Assert.Contains("Confirme seu e-mail", await right.Content.ReadAsStringAsync());

        // Wrong password: same answer as an unknown e-mail.
        var wrong = await LoginAsync(client, registration.Email, "Errada0000");
        var unknown = await LoginAsync(client, $"ninguem-{Guid.NewGuid():N}@datavello.test", "Errada0000");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await Detail(unknown), await Detail(wrong));
    }

    [Fact]
    public async Task Failed_logins_on_an_unconfirmed_account_still_lock_it()
    {
        var registration = await RegisterUnconfirmedAsync();
        var client = _factory.CreateBrowserLikeClient();

        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(client, registration.Email, "Errada0000");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, registration.Email, registration.Password)).StatusCode);
    }

    [Fact]
    public async Task Confirmation_link_signs_in_once_and_then_only_says_already_confirmed()
    {
        var registration = await RegisterUnconfirmedAsync();
        var (userId, token) = await _factory.ConfirmationLinkAsync(registration.Email);

        var first = _factory.CreateBrowserLikeClient();
        var me = await (await first.PostJsonAsync("/api/auth/confirm-email", new { userId, token })).ReadAsync<MeResponse>();
        Assert.Equal(registration.Email, me.User.Email);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);

        // Reusing the link (e.g. if it leaked) does not create a session.
        var second = _factory.CreateBrowserLikeClient();
        var again = await second.PostJsonAsync("/api/auth/confirm-email", new { userId, token });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.False(again.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(second, registration.Email, registration.Password)).StatusCode);
    }

    [Fact]
    public async Task Tampered_or_foreign_confirmation_links_are_rejected()
    {
        var alice = await RegisterUnconfirmedAsync();
        var bob = await RegisterUnconfirmedAsync();
        var (aliceId, aliceToken) = await _factory.ConfirmationLinkAsync(alice.Email);
        var (bobId, _) = await _factory.ConfirmationLinkAsync(bob.Email);
        var client = _factory.CreateBrowserLikeClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/auth/confirm-email", new { userId = bobId, token = aliceToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/auth/confirm-email", new { userId = aliceId, token = "%%%" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/auth/confirm-email", new { userId = Guid.NewGuid(), token = aliceToken })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await LoginAsync(client, bob.Email, bob.Password)).StatusCode);
    }

    [Fact]
    public async Task Resend_sends_a_new_link_only_to_unconfirmed_accounts_and_answers_the_same_way()
    {
        var pending = await RegisterUnconfirmedAsync();
        var (_, confirmed) = await _factory.RegisterAsync();
        var unknown = $"ninguem-{Guid.NewGuid():N}@datavello.test";
        var client = _factory.CreateBrowserLikeClient();
        var before = _factory.Emails.Sent.Count(m => m.To == pending.Email);

        foreach (var email in new[] { pending.Email, confirmed.User.Email, unknown })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/auth/resend-confirmation", new { email })).StatusCode);
        }

        await _factory.Emails.WaitForAsync(pending.Email, ApiClient.ConfirmationSubject);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_factory.Emails.Sent.Count(m => m.To == pending.Email) == before && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(before + 1, _factory.Emails.Sent.Count(m => m.To == pending.Email));
        await Task.Delay(300);
        Assert.Single(_factory.Emails.Sent, m => m.To == confirmed.User.Email && m.Subject == ApiClient.ConfirmationSubject); // only the original
        Assert.DoesNotContain(_factory.Emails.Sent, m => m.To == unknown);

        // The new link works.
        var me = await _factory.ConfirmEmailAsync(_factory.CreateBrowserLikeClient(), pending.Email);
        Assert.Equal(pending.Email, me.User.Email);
    }

    [Fact]
    public async Task Password_reset_link_also_confirms_the_e_mail()
    {
        var registration = await RegisterUnconfirmedAsync();
        var client = _factory.CreateBrowserLikeClient();

        await client.PostJsonAsync("/api/auth/forgot-password", new { email = registration.Email });
        var mail = await _factory.Emails.WaitForAsync(registration.Email, "Redefinição de senha");
        var query = HttpUtility.ParseQueryString(new Uri("http://x" + ResetLink().Match(mail.TextBody).Value).Query);
        var reset = await client.PostJsonAsync("/api/auth/reset-password", new { userId = query["uid"], token = query["token"], newPassword = "NovaSenha456" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, registration.Email, "NovaSenha456")).StatusCode);
    }

    [Fact]
    public async Task Users_own_at_most_three_companies_but_can_join_others()
    {
        var (client, me) = await _factory.RegisterAsync("Primeira");
        Assert.True(me.CanCreateCompany);
        Assert.Equal(3, me.MaxOwnedCompanies);

        Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync("/api/companies", new { name = "Segunda" })).StatusCode);
        var third = await (await client.PostJsonAsync("/api/companies", new { name = "Terceira" })).ReadAsync<MeResponse>();
        Assert.False(third.CanCreateCompany);

        var fourth = await client.PostJsonAsync("/api/companies", new { name = "Quarta" });
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        Assert.Contains("limite de 3 empresas", await fourth.Content.ReadAsStringAsync());

        // Being invited to someone else's company is not limited.
        var (owner, _) = await _factory.RegisterAsync("Empresa de outra pessoa");
        var invitation = await (await owner.PostJsonAsync("/api/company/invitations", new { email = me.User.Email }))
            .ReadAsync<Datavello.Api.Features.Companies.CreatedInvitationDto>();
        var joined = await (await client.PostJsonAsync("/api/invitations/accept", new { token = invitation.Token })).ReadAsync<MeResponse>();
        Assert.Equal(4, joined.Companies.Count);
        Assert.Equal(3, joined.Companies.Count(c => c.Role == CompanyRole.Owner));
    }

    [Fact]
    public async Task New_accounts_start_unconfirmed()
    {
        // (Accounts that existed before this rule are confirmed once by the ConfirmExistingUserEmails migration.)
        var registration = await RegisterUnconfirmedAsync();
        await using var db = database.CreateContext();
        Assert.False(await db.Users.Where(u => u.Email == registration.Email).Select(u => u.EmailConfirmed).SingleAsync());
    }

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())?.Detail;

    public void Dispose() => _factory.Dispose();
}
