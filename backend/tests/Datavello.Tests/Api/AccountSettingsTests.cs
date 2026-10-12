using System.Net;
using System.Net.Http.Json;
using Datavello.Api.Domain;
using Datavello.Api.Features.Auth;
using Datavello.Api.Features.Companies;
using Datavello.Api.Features.Profile;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class AccountSettingsTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [Fact]
    public async Task Owner_updates_company_name_and_document()
    {
        var (client, me) = await _factory.RegisterAsync("Nome antigo");

        var company = await (await client.PutJsonAsync("/api/company", new { name = "  Padaria Nova  ", document = "11222333000181" }))
            .ReadAsync<CompanyDto>();

        Assert.Equal(me.ActiveCompany!.Id, company.Id);
        Assert.Equal("Padaria Nova", company.Name);
        Assert.Equal("11.222.333/0001-81", company.Document);
        Assert.Equal(CompanyRole.Owner, company.Role);
        Assert.Equal("Padaria Nova", (await client.GetJsonAsync<MeResponse>("/api/auth/me")).ActiveCompany!.Name);

        var cleared = await (await client.PutJsonAsync("/api/company", new { name = "Padaria Nova", document = "" })).ReadAsync<CompanyDto>();
        Assert.Null(cleared.Document);
    }

    [Theory]
    [InlineData("11222333000182")] // wrong check digit
    [InlineData("11111111111")]    // repeated digits
    [InlineData("123")]            // wrong length
    public async Task Invalid_documents_are_rejected(string document)
    {
        var (client, _) = await _factory.RegisterAsync();

        var response = await client.PutJsonAsync("/api/company", new { name = "Empresa", document });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CPF ou CNPJ inválido", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Members_can_read_but_not_change_company_data()
    {
        var (_, owner) = await _factory.RegisterAsync("Empresa do dono");
        var (memberClient, member) = await _factory.RegisterAsync("Empresa do membro");

        await using (var db = database.CreateContext())
        {
            db.CompanyMembers.Add(new CompanyMember { CompanyId = owner.ActiveCompany!.Id, UserId = member.User.Id, Role = CompanyRole.Member });
            await db.SaveChangesAsync();
        }

        memberClient.DefaultRequestHeaders.Add("X-Company-Id", owner.ActiveCompany!.Id.ToString());
        var company = await memberClient.GetJsonAsync<CompanyDto>("/api/company");
        Assert.Equal(CompanyRole.Member, company.Role);
        Assert.Equal(2, company.MemberCount);

        var update = await memberClient.PutJsonAsync("/api/company", new { name = "Tomada", document = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task Profile_name_can_be_updated()
    {
        var (client, _) = await _factory.RegisterAsync();

        var profile = await (await client.PutJsonAsync("/api/profile", new { fullName = "  Nome Atualizado " })).ReadAsync<ProfileDto>();

        Assert.Equal("Nome Atualizado", profile.FullName);
        Assert.Equal("Nome Atualizado", (await client.GetJsonAsync<MeResponse>("/api/auth/me")).User.FullName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutJsonAsync("/api/profile", new { fullName = "" })).StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        var registration = ApiClient.NewRegistration();
        var current = _factory.CreateBrowserLikeClient();
        (await current.PostAsJsonAsync("/api/auth/register", registration)).EnsureSuccessStatusCode();

        var otherDevice = _factory.CreateBrowserLikeClient();
        (await otherDevice.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = registration.Password }))
            .EnsureSuccessStatusCode();

        var wrong = await current.PostJsonAsync("/api/profile/password", new { currentPassword = "errada123", newPassword = "NovaSenha456" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("Senha atual incorreta", await wrong.Content.ReadAsStringAsync());

        var weak = await current.PostJsonAsync("/api/profile/password", new { currentPassword = registration.Password, newPassword = "curta" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var changed = await current.PostJsonAsync("/api/profile/password", new { currentPassword = registration.Password, newPassword = "NovaSenha456" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/auth/me")).StatusCode);

        var fresh = _factory.CreateBrowserLikeClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = registration.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await fresh.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = "NovaSenha456" })).StatusCode);
    }

    [Fact]
    public async Task Session_of_a_deleted_user_stops_working()
    {
        var (client, me) = await _factory.RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/categories")).StatusCode);

        await using (var db = database.CreateContext())
        {
            await db.Companies.Where(c => c.Id == me.ActiveCompany!.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == me.User.Id).ExecuteDeleteAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/categories")).StatusCode);
    }

    [Fact]
    public async Task Removing_a_membership_revokes_access_to_that_company_immediately()
    {
        var (_, owner) = await _factory.RegisterAsync("Empresa compartilhada");
        var (memberClient, member) = await _factory.RegisterAsync("Empresa própria do membro");
        var shared = owner.ActiveCompany!.Id;

        await using (var db = database.CreateContext())
        {
            db.CompanyMembers.Add(new CompanyMember { CompanyId = shared, UserId = member.User.Id, Role = CompanyRole.Member });
            await db.SaveChangesAsync();
        }

        memberClient.DefaultRequestHeaders.Add("X-Company-Id", shared.ToString());
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/transactions")).StatusCode);

        await using (var db = database.CreateContext())
        {
            await db.CompanyMembers.Where(m => m.CompanyId == shared && m.UserId == member.User.Id).ExecuteDeleteAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/transactions")).StatusCode);
    }

    [Fact]
    public async Task Api_responses_carry_security_headers()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Login_attempts_are_rate_limited_per_client()
    {
        using var limited = new DatavelloApiFactory(database, authPermitPerMinute: 3);
        var client = limited.CreateBrowserLikeClient();
        var attempt = new LoginRequest { Email = "ninguem@datavello.test", Password = "Qualquer123" };

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", attempt)).StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Contains("Muitas tentativas", await blocked.Content.ReadAsStringAsync());
    }

    public void Dispose() => _factory.Dispose();
}
