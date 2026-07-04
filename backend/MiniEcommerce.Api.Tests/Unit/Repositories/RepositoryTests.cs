using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Repositories;

namespace MiniEcommerce.Api.Tests.Unit.Repositories;

/// <summary>
/// Characterization tests for <see cref="Repository{T}"/>. These were written
/// first (red) and confirm the existing implementation behaves as documented
/// in <c>IRepository&lt;T&gt;</c>:
///   - GetByIdAsync returns null for missing ids
///   - ListAsync / ListAsync(predicate) filter correctly
///   - AddAsync stages inserts but does not call SaveChanges
///   - UpdateAsync marks the entity Modified
///   - RemoveAsync marks the entity Deleted
///   - Query returns an IQueryable for further composition
/// </summary>
public class RepositoryTests
{
    // Each test class gets a unique DB name so in-memory stores don't share state.
    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Repo_{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        using var ctx = NewContext();
        var category = new Category { Id = 1, Name = "Electronics", Slug = "electronics" };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();
        var sut = new Repository<Category>(ctx);

        // Act
        var result = await sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Electronics");
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityDoesNotExist_ReturnsNull()
    {
        using var ctx = NewContext();
        var sut = new Repository<Category>(ctx);

        var result = await sut.GetByIdAsync(42);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WhenNoEntities_ReturnsEmpty()
    {
        using var ctx = NewContext();
        var sut = new Repository<Category>(ctx);

        var result = await sut.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllEntities()
    {
        using var ctx = NewContext();
        ctx.Categories.AddRange(
            new Category { Id = 1, Name = "Electronics", Slug = "electronics" },
            new Category { Id = 2, Name = "Books", Slug = "books" });
        await ctx.SaveChangesAsync();
        var sut = new Repository<Category>(ctx);

        var result = await sut.ListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithPredicate_ReturnsMatchingEntities()
    {
        using var ctx = NewContext();
        ctx.Categories.AddRange(
            new Category { Id = 1, Name = "Electronics", Slug = "electronics" },
            new Category { Id = 2, Name = "Books", Slug = "books" },
            new Category { Id = 3, Name = "Clothing", Slug = "clothing" });
        await ctx.SaveChangesAsync();
        var sut = new Repository<Category>(ctx);

        var result = await sut.ListAsync(c => c.Name.StartsWith("B"));

        result.Should().ContainSingle().Which.Name.Should().Be("Books");
    }

    [Fact]
    public async Task AddAsync_StagesInsert_ButDoesNotCallSaveChanges()
    {
        using var ctx = NewContext();
        var sut = new Repository<Category>(ctx);

        var added = await sut.AddAsync(new Category { Name = "Toys", Slug = "toys" });

        added.Should().NotBeNull();
        // Repository contract: AddAsync stages the entity; the caller is
        // responsible for SaveChanges. The entity should be in Added state
        // but not yet persisted.
        ctx.Categories.Local.Should().ContainSingle();
        (await ctx.Categories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_FollowedBySaveChanges_PersistsEntity()
    {
        using var ctx = NewContext();
        var sut = new Repository<Category>(ctx);

        await sut.AddAsync(new Category { Name = "Toys", Slug = "toys" });
        await ctx.SaveChangesAsync();

        (await ctx.Categories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_MarksEntityAsModified()
    {
        using var ctx = NewContext();
        var category = new Category { Id = 1, Name = "Electronics", Slug = "electronics" };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();
        // Detach so we can re-attach via Update.
        ctx.Entry(category).State = EntityState.Detached;
        var sut = new Repository<Category>(ctx);

        var updated = new Category { Id = 1, Name = "Electronics & Gadgets", Slug = "electronics" };
        await sut.UpdateAsync(updated);
        await ctx.SaveChangesAsync();

        var stored = await ctx.Categories.FindAsync(1);
        stored!.Name.Should().Be("Electronics & Gadgets");
    }

    [Fact]
    public async Task RemoveAsync_DeletesEntity()
    {
        using var ctx = NewContext();
        var category = new Category { Id = 1, Name = "Electronics", Slug = "electronics" };
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();
        var sut = new Repository<Category>(ctx);

        await sut.RemoveAsync(category);
        await ctx.SaveChangesAsync();

        (await ctx.Categories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Query_ReturnsIQueryableForFurtherComposition()
    {
        using var ctx = NewContext();
        ctx.Categories.AddRange(
            new Category { Id = 1, Name = "Electronics", Slug = "electronics" },
            new Category { Id = 2, Name = "Books", Slug = "books" });
        await ctx.SaveChangesAsync();
        var sut = new Repository<Category>(ctx);

        // Compose a LINQ query on top of Query() — should be executable.
        // We assert the contract: Query() exposes IQueryable so the caller can
        // append Where/OrderBy/Select. (We avoid string.Length here because the
        // EF InMemory provider has known LINQ translation quirks with it.)
        var result = sut.Query()
            .OrderBy(c => c.Name)
            .Select(c => c.Slug)
            .ToList();

        result.Should().Equal(new[] { "books", "electronics" });
    }
}
