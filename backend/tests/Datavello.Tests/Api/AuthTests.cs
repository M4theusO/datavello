using System.Net;
using System.Net.Http.Json;
using Datavello.Api.Domain;
using Datavello.Api.Features.Auth;
using Datavello.Api.Features.Companies;
using Datavello.Api.Infrastructure.Data;
using Datavello.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Datavello.Tests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class AuthTests(TestDatabase database) : IDisposable
{
    private readonly DatavelloApiFactory _factory = new(database);

    [Fact]
    public async Task Register_creates_user_company_categories_and_dashboard_and_signs_in()
    {
        var client = _factory.CreateBrowserLikeClient();
        var registration = ApiClient.NewRegistration("Padaria Teste");

        var response = await client.PostAsJsonAsync("/api/auth/register", registration);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setCookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("datavello.auth", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        var me = (await response.Content.ReadFromJsonAsync<MeResponse>(Json.Options))!;
        Assert.Equal(registration.Email, me.User.Email);
        Assert.Equal("Padaria Teste", me.ActiveCompany!.Name);
        Assert.Equal(CompanyRole.Owner, me.ActiveCompany.Role);

        await using var db = database.CreateContext(me.ActiveCompany.Id);
        Assert.Equal(CompanyProvisioningService.DefaultCategories.Count, await db.Categories.CountAsync());
        var dashboard = await db.Dashboards.Include(d => d.Widgets).SingleAsync();
        Assert.Equal(WidgetCatalog.All, dashboard.Widgets.OrderBy(w => w.Position).Select(w => w.WidgetType));

        var meAgain = await client.GetFromJsonAsync<MeResponse>("/api/auth/me", Json.Options);
        Assert.Equal(me.User.Id, meAgain!.User.Id);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_conflict()
    {
        var registration = ApiClient.NewRegistration();
        var client = _factory.CreateBrowserLikeClient();
        (await client.PostAsJsonAsync("/api/auth/register", registration)).EnsureSuccessStatusCode();

        var other = _factory.CreateBrowserLikeClient();
        var response = await other.PostAsJsonAsync("/api/auth/register", registration);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_fails_and_creates_nothing()
    {
        var registration = ApiClient.NewRegistration();
        var weak = new RegisterRequest
        {
            FullName = registration.FullName,
            Email = registration.Email,
            Password = "curta",
            CompanyName = "Não deve existir"
        };

        var response = await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register", weak);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = database.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == weak.Email));
        Assert.False(await db.Companies.AnyAsync(c => c.Name == "Não deve existir"));
    }

    [Fact]
    public async Task Register_with_invalid_payload_returns_validation_errors()
    {
        var response = await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = "", email = "nao-e-email", password = "", companyName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("E-mail inválido.", body);
    }

    [Fact]
    public async Task Login_and_logout_flow()
    {
        var registration = ApiClient.NewRegistration();
        (await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register", registration)).EnsureSuccessStatusCode();

        var client = _factory.CreateBrowserLikeClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = registration.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_failure_does_not_reveal_whether_the_email_exists()
    {
        var registration = ApiClient.NewRegistration();
        (await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register", registration)).EnsureSuccessStatusCode();
        var client = _factory.CreateBrowserLikeClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = "SenhaErrada999" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "ninguem@datavello.test", Password = "SenhaErrada999" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await Detail(wrongPassword), await Detail(unknownEmail));
    }

    [Fact]
    public async Task Account_is_locked_after_repeated_failures()
    {
        var registration = ApiClient.NewRegistration();
        (await _factory.CreateBrowserLikeClient().PostAsJsonAsync("/api/auth/register", registration)).EnsureSuccessStatusCode();
        var client = _factory.CreateBrowserLikeClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = "SenhaErrada999" });
        }

        var correct = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = registration.Email, Password = registration.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_without_client_header_are_rejected()
    {
        var client = _factory.CreateClient(); // no X-Requested-With

        var response = await client.PostAsJsonAsync("/api/auth/register", ApiClient.NewRegistration());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("X-Requested-With", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Users_only_see_their_own_company()
    {
        var (_, meA) = await _factory.RegisterAsync("Empresa Alfa");
        var (clientB, meB) = await _factory.RegisterAsync("Empresa Beta");

        Assert.NotEqual(meA.ActiveCompany!.Id, meB.ActiveCompany!.Id);
        Assert.Single(meB.Companies);
        Assert.DoesNotContain(meB.Companies, c => c.Id == meA.ActiveCompany.Id);

        // Asking for another company's id is refused, not silently ignored.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("X-Company-Id", meA.ActiveCompany.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await clientB.SendAsync(request)).StatusCode);

        var own = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        own.Headers.Add("X-Company-Id", meB.ActiveCompany.Id.ToString());
        Assert.Equal(HttpStatusCode.OK, (await clientB.SendAsync(own)).StatusCode);
    }

    [Fact]
    public void Runtime_model_matches_the_migrations()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>())?.Detail;

    public void Dispose() => _factory.Dispose();
}
