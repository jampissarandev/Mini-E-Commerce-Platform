using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Helpers for working with the in-memory <see cref="ApplicationDbContext"/>
/// inside tests. All helpers open a fresh scope to avoid leaking the
/// context's internal service provider across requests.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Resolves a fresh <see cref="ApplicationDbContext"/> from the test
    /// host. Each test that needs direct DB access should call this and
    /// dispose the result; do not cache contexts across test methods.
    /// </summary>
    public static ApplicationDbContext CreateDbContext(this ApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Deletes all rows from every table in the in-memory database. Call this
    /// in a test class's constructor (after the host has started seeding) to
    /// give each test a clean slate without re-creating the factory.
    /// </summary>
    public static async Task ResetDatabaseAsync(this ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Order matters: children before parents.
        // NOTE: EF Core InMemory does not support ExecuteDeleteAsync (bulk SQL
        // DELETE), so we load + RemoveRange + SaveChanges instead. This is
        // acceptable for tests because the in-memory store is small.
        context.OrderItems.RemoveRange(context.OrderItems);
        context.Orders.RemoveRange(context.Orders);
        context.CartItems.RemoveRange(context.CartItems);
        context.Carts.RemoveRange(context.Carts);
        context.ProductImages.RemoveRange(context.ProductImages);
        context.Products.RemoveRange(context.Products);
        context.Categories.RemoveRange(context.Categories);
        // Identity tables
        context.UserRoles.RemoveRange(context.UserRoles);
        context.UserClaims.RemoveRange(context.UserClaims);
        context.UserLogins.RemoveRange(context.UserLogins);
        context.UserTokens.RemoveRange(context.UserTokens);
        context.Users.RemoveRange(context.Users);
        context.Roles.RemoveRange(context.Roles);
        await context.SaveChangesAsync();

        // Always re-seed the Identity roles after a reset. Production code
        // (e.g. AuthController.Register) calls AddToRoleAsync and assumes the
        // role already exists.
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
