using Datavello.Api.Infrastructure.Configuration;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Datavello.Api.Infrastructure.Data;

/// <summary>Used by `dotnet ef` (migrations). Reads the connection string from .env / environment.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.Load(Directory.GetCurrentDirectory());
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is not set. Copy .env.example to .env.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options, new CurrentTenant());
    }
}
