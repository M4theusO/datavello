using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Datavello.Api.Features.Auth;
using Datavello.Api.Features.Companies;
using Datavello.Api.Features.Profile;
using Datavello.Tests.Infrastructure;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed partial class EmailChangeTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [GeneratedRegex(@"http://datavello\.test/confirmar-email\?[^\s""<]+")]
    private static partial Regex ConfirmLink();

    private static string NewEmail() => $"novo-{Guid.NewGuid():N}@datavello.test";

    private async Task<(Guid UserId, string NewEmail, string Token)> RequestChangeAsync(HttpClient client, string newEmail)
    {
        var response = await client.PostJsonAsync("/api/profile/email", new { newEmail, currentPassword = "SenhaForte123" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var message = await _factory.Emails.WaitForAsync(newEmail, "Confirme seu novo e-mail");
        Assert.Equal("Confirme seu novo e-mail — Datavello", message.Subject);
        var match = ConfirmLink().Match(message.TextBody);
        Assert.True(match.Success, "e-mail should contain the confirmation link");
        var query = HttpUtility.ParseQueryString(new Uri(match.Value).Query);
        return (Guid.Parse(query["uid"]!), query["email"]!, query["token"]!);
    }

    [Fact]
    public async Task Email_changes_only_after_confirmation_and_old_address_is_notified()
    {
        var (client, me) = await _factory.RegisterAsync();
        var newEmail = NewEmail();

        var (userId, emailInLink, token) = await RequestChangeAsync(client, newEmail);
        Assert.Equal(newEmail, emailInLink);

        // Nothing changed yet.
        Assert.Equal(me.User.Email, (await client.GetJsonAsync<MeResponse>("/api/auth/me")).User.Email);

        var confirmed = await (await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/confirm-email-change", new { userId, newEmail = emailInLink, token }))
            .ReadAsync<EmailChangedDto>();
        Assert.Equal(newEmail, confirmed.Email);

        // Sessions end; sign-in works with the new e-mail only.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var fresh = _factory.CreateBrowserLikeClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostJsonAsync("/api/auth/login", new { email = me.User.Email, password = "SenhaForte123" })).StatusCode);
        var login = await (await fresh.PostJsonAsync("/api/auth/login", new { email = newEmail, password = "SenhaForte123" })).ReadAsync<MeResponse>();
        Assert.Equal(newEmail, login.User.Email);
        Assert.Equal(me.ActiveCompany!.Id, login.ActiveCompany!.Id);

        var notice = await _factory.Emails.WaitForAsync(me.User.Email, "foi alterado");
        Assert.Equal("O e-mail da sua conta foi alterado — Datavello", notice.Subject);
        Assert.Contains(newEmail, notice.TextBody);

        // Single use.
        var again = await fresh.PostJsonAsync("/api/auth/confirm-email-change", new { userId, newEmail = emailInLink, token });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Request_requires_the_current_password_and_a_different_free_email()
    {
        var (client, me) = await _factory.RegisterAsync();
        var (_, other) = await _factory.RegisterAsync();

        var wrongPassword = await client.PostJsonAsync("/api/profile/email", new { newEmail = NewEmail(), currentPassword = "Errada123" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Contains("Senha atual incorreta", await wrongPassword.Content.ReadAsStringAsync());

        var same = await client.PostJsonAsync("/api/profile/email", new { newEmail = me.User.Email.ToUpperInvariant(), currentPassword = "SenhaForte123" });
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);

        var taken = await client.PostJsonAsync("/api/profile/email", new { newEmail = other.User.Email, currentPassword = "SenhaForte123" });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/profile/email", new { newEmail = NewEmail(), currentPassword = "x" })).StatusCode);
    }

    [Fact]
    public async Task Link_cannot_be_altered_or_used_for_another_account()
    {
        var (client, me) = await _factory.RegisterAsync();
        var (_, other) = await _factory.RegisterAsync();
        var (userId, newEmail, token) = await RequestChangeAsync(client, NewEmail());
        var anonymous = _factory.CreateBrowserLikeClient();

        // Token is bound to the requested address and to the user.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.PostJsonAsync("/api/auth/confirm-email-change", new { userId, newEmail = NewEmail(), token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.PostJsonAsync("/api/auth/confirm-email-change", new { userId = other.User.Id, newEmail, token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.PostJsonAsync("/api/auth/confirm-email-change", new { userId, newEmail, token = "%%%" })).StatusCode);

        Assert.Equal(me.User.Email, (await client.GetJsonAsync<MeResponse>("/api/auth/me")).User.Email);
    }

    [Fact]
    public async Task Address_taken_after_the_request_is_rejected_at_confirmation()
    {
        var (client, _) = await _factory.RegisterAsync();
        var target = NewEmail();
        var (userId, newEmail, token) = await RequestChangeAsync(client, target);

        // Someone registers with that address before the link is used.
        var registration = ApiClient.NewRegistration();
        var squatter = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/register", new
        {
            registration.FullName,
            email = target,
            registration.Password,
            registration.CompanyName
        });
        Assert.Equal(HttpStatusCode.Accepted, squatter.StatusCode); // even unconfirmed, the account holds the address

        var confirm = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/confirm-email-change", new { userId, newEmail, token });
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
    }

    [Fact]
    public async Task Invitation_is_e_mailed_with_the_same_link_the_owner_sees()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync("Empresa que convida");
        var email = NewEmail();

        var created = await (await owner.PostJsonAsync("/api/company/invitations", new { email })).ReadAsync<CreatedInvitationDto>();
        var message = await _factory.Emails.WaitForAsync(email, "convidou você");

        Assert.Equal($"{ownerMe.User.FullName} convidou você para Empresa que convida no Datavello", message.Subject);
        Assert.Contains($"{DatavelloApiFactory.PublicUrl}/convite/{created.Token}", message.TextBody);
        Assert.Contains(email, message.TextBody);
    }

    public void Dispose() => _factory.Dispose();
}
