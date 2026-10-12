using System.Text.Json.Serialization;
using Datavello.Api.Domain;
using Datavello.Api.Features.Auth;
using Datavello.Api.Features.Categories;
using Datavello.Api.Features.Companies;
using Datavello.Api.Features.Dashboards;
using Datavello.Api.Features.Transactions;
using Datavello.Api.Infrastructure.Configuration;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Identity;
using Datavello.Api.Infrastructure.Security;
using Datavello.Api.Infrastructure.Seed;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    DotEnvLoader.Load(Directory.GetCurrentDirectory());
}

var builder = WebApplication.CreateBuilder(args);

// --- Persistence and tenancy ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured. Copy .env.example to .env.");

builder.Services.AddScoped<CurrentTenant>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

// --- Identity and cookie authentication ---
builder.Services
    .AddIdentityCore<AppUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "datavello.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    // An API answers with status codes instead of redirecting to a login page.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// --- Application services ---
builder.Services.AddScoped<CompanyProvisioningService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<DemoSeeder>();

// --- API ---
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, "Datavello.Api.xml");
    if (File.Exists(xml))
    {
        o.IncludeXmlComments(xml);
    }

    o.OperationFilter<ClientHeaderOperationFilter>();
});

const string CorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Development-only command: dotnet run --project src/Datavello.Api -- seed-demo [--reset]
if (args.Contains("seed-demo"))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("seed-demo is only available in the Development environment.");
        Environment.ExitCode = 1;
        return;
    }

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().RunAsync(reset: args.Contains("--reset"));
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "Datavello API");
}

app.UseCors(CorsPolicy);
app.UseMiddleware<RequireClientHeaderMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
