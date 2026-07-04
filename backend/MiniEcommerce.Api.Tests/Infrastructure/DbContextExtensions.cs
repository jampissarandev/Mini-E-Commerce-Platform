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

    /// <summary>
    /// Seeds the same categories and products used in production (see <see cref="Seed"/>)
    /// into the test in-memory database. Call after <see cref="ResetDatabaseAsync"/>
    /// when a test class needs catalog data.
    /// </summary>
    public static async Task SeedCatalogDataAsync(this ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await context.Categories.AnyAsync())
            return;

        var categories = new List<Category>
        {
            new() { Name = "Electronics", Slug = "electronics" },
            new() { Name = "Books", Slug = "books" },
            new() { Name = "Clothing", Slug = "clothing" },
            new() { Name = "Home", Slug = "home" },
            new() { Name = "Toys", Slug = "toys" }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { Name = "Wireless Headphones", Slug = "wireless-headphones", Description = "Premium noise-cancelling wireless headphones with 30-hour battery life.", Price = 79.99m, Stock = 50, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/wireless-headphones.jpg", SortOrder = 0 } } },
            new() { Name = "Bluetooth Speaker", Slug = "bluetooth-speaker", Description = "Portable waterproof speaker with deep bass and 12-hour playtime.", Price = 49.99m, Stock = 35, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/bluetooth-speaker.jpg", SortOrder = 0 } } },
            new() { Name = "USB-C Hub", Slug = "usb-c-hub", Description = "7-in-1 USB-C hub with HDMI, USB 3.0, and SD card reader.", Price = 34.99m, Stock = 80, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/usb-c-hub.jpg", SortOrder = 0 } } },
            new() { Name = "Mechanical Keyboard", Slug = "mechanical-keyboard", Description = "RGB mechanical keyboard with Cherry MX switches.", Price = 129.99m, Stock = 25, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/mechanical-keyboard.jpg", SortOrder = 0 } } },
            new() { Name = "The Clean Coder", Slug = "the-clean-coder", Description = "A code of conduct for professional programmers by Robert C. Martin.", Price = 29.99m, Stock = 100, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/clean-coder.jpg", SortOrder = 0 } } },
            new() { Name = "Design Patterns", Slug = "design-patterns", Description = "Elements of reusable object-oriented software.", Price = 39.99m, Stock = 60, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/design-patterns.jpg", SortOrder = 0 } } },
            new() { Name = "Refactoring", Slug = "refactoring", Description = "Improving the design of existing code by Martin Fowler.", Price = 34.99m, Stock = 45, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/refactoring.jpg", SortOrder = 0 } } },
            new() { Name = "Atomic Habits", Slug = "atomic-habits", Description = "An easy and proven way to build good habits by James Clear.", Price = 19.99m, Stock = 150, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/atomic-habits.jpg", SortOrder = 0 } } },
            new() { Name = "Classic T-Shirt", Slug = "classic-t-shirt", Description = "100% organic cotton classic fit t-shirt.", Price = 24.99m, Stock = 200, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/classic-tshirt.jpg", SortOrder = 0 } } },
            new() { Name = "Denim Jacket", Slug = "denim-jacket", Description = "Vintage wash denim jacket with button closure.", Price = 89.99m, Stock = 30, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/denim-jacket.jpg", SortOrder = 0 } } },
            new() { Name = "Running Shoes", Slug = "running-shoes", Description = "Lightweight running shoes with responsive cushioning.", Price = 109.99m, Stock = 60, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/running-shoes.jpg", SortOrder = 0 } } },
            new() { Name = "Wool Beanie", Slug = "wool-beanie", Description = "Soft merino wool beanie for cold weather.", Price = 19.99m, Stock = 90, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/wool-beanie.jpg", SortOrder = 0 } } },
            new() { Name = "Ceramic Vase", Slug = "ceramic-vase", Description = "Handcrafted ceramic vase with minimalist design.", Price = 34.99m, Stock = 40, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/ceramic-vase.jpg", SortOrder = 0 } } },
            new() { Name = "Scented Candle Set", Slug = "scented-candle-set", Description = "Set of 3 soy wax candles with lavender, vanilla, and cedar.", Price = 29.99m, Stock = 75, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/scented-candles.jpg", SortOrder = 0 } } },
            new() { Name = "Throw Blanket", Slug = "throw-blanket", Description = "Soft fleece throw blanket in assorted colors.", Price = 39.99m, Stock = 55, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/throw-blanket.jpg", SortOrder = 0 } } },
            new() { Name = "Coffee Mug Set", Slug = "coffee-mug-set", Description = "Set of 4 stoneware coffee mugs, 12oz each.", Price = 24.99m, Stock = 65, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/coffee-mugs.jpg", SortOrder = 0 } } },
            new() { Name = "Building Blocks Set", Slug = "building-blocks-set", Description = "500-piece building blocks set for creative play.", Price = 44.99m, Stock = 45, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/building-blocks.jpg", SortOrder = 0 } } },
            new() { Name = "Remote Control Car", Slug = "remote-control-car", Description = "High-speed RC car with rechargeable battery.", Price = 59.99m, Stock = 30, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/rc-car.jpg", SortOrder = 0 } } },
            new() { Name = "Puzzle 1000 Pieces", Slug = "puzzle-1000-pieces", Description = "1000-piece jigsaw puzzle with landscape artwork.", Price = 19.99m, Stock = 80, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/puzzle.jpg", SortOrder = 0 } } },
            new() { Name = "Plush Teddy Bear", Slug = "plush-teddy-bear", Description = "Soft plush teddy bear, 12 inches tall.", Price = 22.99m, Stock = 100, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/teddy-bear.jpg", SortOrder = 0 } } }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }
}
