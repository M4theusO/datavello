using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Configuration;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Datavello.Tests.Infrastructure;

/// <summary>
/// Recreates the test database (ConnectionStrings__Test) once per test run and applies
/// the real migrations. Tests that touch the database share it through the "Database" collection.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public string ConnectionString { get; }

    public TestDatabase()
    {
        DotEnvLoader.Load(AppContext.BaseDirectory);
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Test")
            ?? throw new InvalidOperationException("ConnectionStrings__Test is not set. See .env.example.");

        // The schema is wiped on every run: never allow it to point at the application database.
        var main = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (main is not null
            && string.Equals(new NpgsqlConnectionStringBuilder(main).Database, new NpgsqlConnectionStringBuilder(ConnectionString).Database, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ConnectionStrings__Test must point to a different database than ConnectionStrings__Default.");
        }
    }

    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync();

        // Reset the schema instead of dropping the database, so idle sessions
        // (e.g. pgAdmin browsing datavello_test) do not break the test run.
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
        await db.Database.MigrateAsync();
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var target = new NpgsqlConnectionStringBuilder(ConnectionString);
        var maintenance = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();

        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        exists.Parameters.AddWithValue("name", target.Database!);
        if (await exists.ExecuteScalarAsync() is null)
        {
            var quoted = "\"" + target.Database!.Replace("\"", "\"\"") + "\"";
            await using var create = new NpgsqlCommand($"CREATE DATABASE {quoted}", connection);
            await create.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Context scoped to <paramref name="companyId"/>, or a system context when null.</summary>
    public AppDbContext CreateContext(Guid? companyId = null)
    {
        var tenant = new CurrentTenant();
        if (companyId is { } id)
        {
            tenant.Set(id);
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        return new AppDbContext(options, tenant);
    }

    /// <summary>Creates a company with one category, using a system (tenant-less) context.</summary>
    public async Task<(Company Company, Category Category)> CreateCompanyAsync(string name)
    {
        await using var db = CreateContext();
        var company = new Company { Name = name };
        var category = new Category { CompanyId = company.Id, Name = "Vendas", Kind = CategoryKind.Income };
        db.AddRange(company, category);
        await db.SaveChangesAsync();
        return (company, category);
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<TestDatabase>
{
    public const string Name = "Database";
}
