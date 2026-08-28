using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;
using Testcontainers.PostgreSql;

namespace MiniEcommerce.Api.Tests.Integration.Postgres;

/// <summary>
/// Shared Postgres container for the <c>Postgres</c> xUnit collection.
/// One container per test run (≈ 5 s startup) — all tests in the collection
/// share the same DB and run sequentially (xUnit collection semantics).
/// Tagged <c>Integration-DB</c> per <c>tasks/test-spec.md §6</c>: opt-in
/// via <c>dotnet test --filter "Category=Integration-DB"</c> so CI can run
/// the fast InMemory suite by default and only pull the container on demand.
/// </summary>
[Trait("Category", "Integration-DB")]
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithEnvironment("POSTGRES_DB", "mini_ecommerce_test")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DbContextOptions<ApplicationDbContext> NewOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var dbOptions = NewOptions();
        await using var ctx = new ApplicationDbContext(dbOptions);
        await ctx.Database.MigrateAsync();

        if (!await ctx.Categories.AnyAsync())
        {
            ctx.Categories.Add(new Category { Name = "Test", Slug = "test" });
            await ctx.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[Trait("Category", "Integration-DB")]
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
