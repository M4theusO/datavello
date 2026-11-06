using System.Net;
using Datavello.Api.Domain;
using Datavello.Api.Features.Auth;
using Datavello.Api.Features.Categories;
using Datavello.Api.Features.Companies;
using Datavello.Api.Infrastructure.Tenancy;
using Datavello.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class TeamApiTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    private static string NewEmail() => $"convidado-{Guid.NewGuid():N}@datavello.test";

    private static async Task<CreatedInvitationDto> InviteAsync(HttpClient owner, string email)
    {
        var response = await owner.PostJsonAsync("/api/company/invitations", new { email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<CreatedInvitationDto>();
    }

    private async Task<(HttpClient Client, MeResponse Me)> RegisterWithInviteAsync(string email, string token)
    {
        var client = _factory.CreateBrowserLikeClient();
        var me = await (await client.PostJsonAsync("/api/auth/register", new
        {
            fullName = "Pessoa Convidada",
            email,
            password = "SenhaForte123",
            inviteToken = token
        })).ReadAsync<MeResponse>();
        return (client, me);
    }

    [Fact]
    public async Task New_user_registers_through_an_invitation_and_joins_the_company_as_member()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync("Empresa que convida");
        var email = NewEmail();
        var created = await InviteAsync(owner, email);

        // Public preview, without a session.
        var preview = await (await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/invitations/preview", new { token = created.Token }))
            .ReadAsync<InvitationPreviewDto>();
        Assert.Equal("Empresa que convida", preview.CompanyName);
        Assert.Equal(email, preview.Email);

        var (_, me) = await RegisterWithInviteAsync(email, created.Token);

        Assert.Equal(ownerMe.ActiveCompany!.Id, me.ActiveCompany!.Id);
        Assert.Equal(CompanyRole.Member, me.ActiveCompany.Role);
        Assert.Single(me.Companies); // no company of their own was created

        var members = await owner.GetJsonAsync<List<MemberDto>>("/api/company/members");
        Assert.Equal([CompanyRole.Owner, CompanyRole.Member], members.Select(m => m.Role));
        Assert.Empty(await owner.GetJsonAsync<List<InvitationDto>>("/api/company/invitations"));

        // Single use.
        var reuse = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/invitations/preview", new { token = created.Token });
        Assert.Equal(HttpStatusCode.Gone, reuse.StatusCode);
    }

    [Fact]
    public async Task Existing_user_accepts_and_switches_between_companies_with_the_header()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync("Empresa A");
        var (guest, guestMe) = await _factory.RegisterAsync("Empresa B");
        var created = await InviteAsync(owner, guestMe.User.Email);

        var accepted = await (await guest.PostJsonAsync("/api/invitations/accept", new { token = created.Token })).ReadAsync<MeResponse>();

        Assert.Equal(ownerMe.ActiveCompany!.Id, accepted.ActiveCompany!.Id);
        Assert.Equal(2, accepted.Companies.Count);

        // Default stays the user's first company; the header selects the shared one.
        Assert.Equal(guestMe.ActiveCompany!.Id, (await guest.GetJsonAsync<MeResponse>("/api/auth/me")).ActiveCompany!.Id);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, ownerMe.ActiveCompany.Id.ToString());
        var categories = await (await guest.SendAsync(request)).ReadAsync<List<CategoryDto>>();
        Assert.Equal(
            (await owner.GetJsonAsync<List<CategoryDto>>("/api/categories")).Select(c => c.Id).Order(),
            categories.Select(c => c.Id).Order());
    }

    [Fact]
    public async Task Invitation_only_works_for_the_invited_email()
    {
        var (owner, _) = await _factory.RegisterAsync();
        var created = await InviteAsync(owner, NewEmail());
        var (other, _) = await _factory.RegisterAsync("Outra");

        var accept = await other.PostJsonAsync("/api/invitations/accept", new { token = created.Token });
        Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);

        var register = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/register", new
        {
            fullName = "Intrusa",
            email = NewEmail(),
            password = "SenhaForte123",
            inviteToken = created.Token
        });
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);

        // Still usable by the right person.
        Assert.Single(await owner.GetJsonAsync<List<InvitationDto>>("/api/company/invitations"));
    }

    [Fact]
    public async Task Unknown_expired_and_revoked_invitations_are_rejected()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync();
        var anonymous = _factory.CreateBrowserLikeClient();

        var unknown = await anonymous.PostJsonAsync("/api/invitations/preview", new { token = "nao-existe" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var expired = await InviteAsync(owner, NewEmail());
        await using (var db = database.CreateContext(ownerMe.ActiveCompany!.Id))
        {
            await db.CompanyInvitations.Where(i => i.Id == expired.Invitation.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        Assert.Equal(HttpStatusCode.Gone, (await anonymous.PostJsonAsync("/api/invitations/preview", new { token = expired.Token })).StatusCode);
        Assert.True((await owner.GetJsonAsync<List<InvitationDto>>("/api/company/invitations")).Single().IsExpired);

        var revoked = await InviteAsync(owner, NewEmail());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/company/invitations/{revoked.Invitation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostJsonAsync("/api/invitations/preview", new { token = revoked.Token })).StatusCode);
    }

    [Fact]
    public async Task Reinviting_the_same_email_replaces_the_previous_link_and_members_cannot_be_invited_again()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync();
        var email = NewEmail();

        var first = await InviteAsync(owner, email);
        var second = await InviteAsync(owner, email.ToUpperInvariant());

        Assert.Equal(second.Invitation.Id, (await owner.GetJsonAsync<List<InvitationDto>>("/api/company/invitations")).Single().Id);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostJsonAsync("/api/invitations/preview", new { token = first.Token })).StatusCode);

        var self = await owner.PostJsonAsync("/api/company/invitations", new { email = ownerMe.User.Email });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_manages_the_team_and_the_owner_cannot_be_removed()
    {
        var (owner, ownerMe) = await _factory.RegisterAsync();
        var email = NewEmail();
        var (member, memberMe) = await RegisterWithInviteAsync(email, (await InviteAsync(owner, email)).Token);

        Assert.Equal(2, (await member.GetJsonAsync<List<MemberDto>>("/api/company/members")).Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/company/invitations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostJsonAsync("/api/company/invitations", new { email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/company/members/{ownerMe.User.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/api/company/members/{ownerMe.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/company/leave", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/company/members/{memberMe.User.Id}")).StatusCode);

        // Access is gone immediately; the user is left without companies.
        var after = await member.GetJsonAsync<MeResponse>("/api/auth/me");
        Assert.Null(after.ActiveCompany);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/categories")).StatusCode);
    }

    [Fact]
    public async Task Member_can_leave_the_company()
    {
        var (owner, _) = await _factory.RegisterAsync();
        var (guest, guestMe) = await _factory.RegisterAsync("Empresa do convidado");
        var created = await InviteAsync(owner, guestMe.User.Email);
        var shared = (await (await guest.PostJsonAsync("/api/invitations/accept", new { token = created.Token })).ReadAsync<MeResponse>()).ActiveCompany!.Id;

        var leave = new HttpRequestMessage(HttpMethod.Post, "/api/company/leave");
        leave.Headers.Add(TenantResolutionMiddleware.HeaderName, shared.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await guest.SendAsync(leave)).StatusCode);

        var me = await guest.GetJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal([guestMe.ActiveCompany!.Id], me.Companies.Select(c => c.Id));
        Assert.Single(await owner.GetJsonAsync<List<MemberDto>>("/api/company/members"));
    }

    [Fact]
    public async Task Team_data_is_isolated_between_companies()
    {
        var (ownerA, _) = await _factory.RegisterAsync("Empresa A");
        var (ownerB, meB) = await _factory.RegisterAsync("Empresa B");
        var inviteA = await InviteAsync(ownerA, NewEmail());

        Assert.Empty(await ownerB.GetJsonAsync<List<InvitationDto>>("/api/company/invitations"));
        Assert.Single(await ownerB.GetJsonAsync<List<MemberDto>>("/api/company/members"));
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.DeleteAsync($"/api/company/invitations/{inviteA.Invitation.Id}")).StatusCode);

        var membersA = await ownerA.GetJsonAsync<List<MemberDto>>("/api/company/members");
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.DeleteAsync($"/api/company/members/{membersA.Single().UserId}")).StatusCode);
        Assert.Single(await ownerA.GetJsonAsync<List<InvitationDto>>("/api/company/invitations"));
        Assert.Single(await ownerB.GetJsonAsync<List<MemberDto>>("/api/company/members"));
        Assert.Equal(meB.User.Id, (await ownerB.GetJsonAsync<List<MemberDto>>("/api/company/members")).Single().UserId);
    }

    [Fact]
    public async Task Rejected_company_header_is_identifiable_by_the_client()
    {
        var (client, _) = await _factory.RegisterAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<ProblemDetails>(response.Content);
        Assert.Equal(TenantResolutionMiddleware.CompanyAccessDeniedType, problem!.Type);
    }

    [Fact]
    public async Task User_creates_another_company_and_becomes_its_owner()
    {
        var (client, me) = await _factory.RegisterAsync("Primeira");

        var response = await client.PostJsonAsync("/api/companies", new { name = "  Segunda  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<MeResponse>();

        Assert.Equal("Segunda", created.ActiveCompany!.Name);
        Assert.Equal(CompanyRole.Owner, created.ActiveCompany.Role);
        Assert.Equal(2, created.Companies.Count);

        // Provisioned with its own default categories, separate from the first company.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, created.ActiveCompany.Id.ToString());
        var categories = await (await client.SendAsync(request)).ReadAsync<List<CategoryDto>>();
        Assert.Equal(CompanyProvisioningService.DefaultCategories.Count, categories.Count);
        var firstCompanyCategories = await client.GetJsonAsync<List<CategoryDto>>("/api/categories");
        Assert.Empty(categories.Select(c => c.Id).Intersect(firstCompanyCategories.Select(c => c.Id)));
        Assert.Equal(me.ActiveCompany!.Id, (await client.GetJsonAsync<MeResponse>("/api/auth/me")).ActiveCompany!.Id);
    }

    [Fact]
    public async Task Registration_without_invite_still_requires_a_company_name()
    {
        var response = await _factory.CreateBrowserLikeClient().PostJsonAsync("/api/auth/register", new
        {
            fullName = "Sem Empresa",
            email = NewEmail(),
            password = "SenhaForte123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Informe o nome da empresa.", await response.Content.ReadAsStringAsync());
    }

    public void Dispose() => _factory.Dispose();
}
