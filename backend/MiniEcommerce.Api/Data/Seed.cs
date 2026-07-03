using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Data;

public static class Seed
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager);
        await SeedCategoriesAndProductsAsync(context);

        var categoryCount = await context.Categories.CountAsync();
        var productCount = await context.Products.CountAsync();
        Console.WriteLine($"Seed complete: {categoryCount} categories, {productCount} products, 2 users");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync("admin@example.com") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@example.com",
                Email = "admin@example.com",
                FullName = "Admin User",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        if (await userManager.FindByEmailAsync("customer@example.com") is null)
        {
            var customer = new ApplicationUser
            {
                UserName = "customer@example.com",
                Email = "customer@example.com",
                FullName = "Customer User",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(customer, "Customer123!");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(customer, "Customer");
        }
    }

    private static async Task SeedCategoriesAndProductsAsync(ApplicationDbContext context)
    {
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
            // Electronics
            new() { Name = "Wireless Headphones", Slug = "wireless-headphones", Description = "Premium noise-cancelling wireless headphones with 30-hour battery life.", Price = 79.99m, Stock = 50, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/wireless-headphones.jpg", SortOrder = 0 } } },
            new() { Name = "Bluetooth Speaker", Slug = "bluetooth-speaker", Description = "Portable waterproof speaker with deep bass and 12-hour playtime.", Price = 49.99m, Stock = 35, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/bluetooth-speaker.jpg", SortOrder = 0 } } },
            new() { Name = "USB-C Hub", Slug = "usb-c-hub", Description = "7-in-1 USB-C hub with HDMI, USB 3.0, and SD card reader.", Price = 34.99m, Stock = 80, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/usb-c-hub.jpg", SortOrder = 0 } } },
            new() { Name = "Mechanical Keyboard", Slug = "mechanical-keyboard", Description = "RGB mechanical keyboard with Cherry MX switches.", Price = 129.99m, Stock = 25, CategoryId = categories[0].Id, Images = { new ProductImage { Url = "/images/products/mechanical-keyboard.jpg", SortOrder = 0 } } },

            // Books
            new() { Name = "The Clean Coder", Slug = "the-clean-coder", Description = "A code of conduct for professional programmers by Robert C. Martin.", Price = 29.99m, Stock = 100, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/clean-coder.jpg", SortOrder = 0 } } },
            new() { Name = "Design Patterns", Slug = "design-patterns", Description = "Elements of reusable object-oriented software.", Price = 39.99m, Stock = 60, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/design-patterns.jpg", SortOrder = 0 } } },
            new() { Name = "Refactoring", Slug = "refactoring", Description = "Improving the design of existing code by Martin Fowler.", Price = 34.99m, Stock = 45, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/refactoring.jpg", SortOrder = 0 } } },
            new() { Name = "Atomic Habits", Slug = "atomic-habits", Description = "An easy and proven way to build good habits by James Clear.", Price = 19.99m, Stock = 150, CategoryId = categories[1].Id, Images = { new ProductImage { Url = "/images/products/atomic-habits.jpg", SortOrder = 0 } } },

            // Clothing
            new() { Name = "Classic T-Shirt", Slug = "classic-t-shirt", Description = "100% organic cotton classic fit t-shirt.", Price = 24.99m, Stock = 200, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/classic-tshirt.jpg", SortOrder = 0 } } },
            new() { Name = "Denim Jacket", Slug = "denim-jacket", Description = "Vintage wash denim jacket with button closure.", Price = 89.99m, Stock = 30, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/denim-jacket.jpg", SortOrder = 0 } } },
            new() { Name = "Running Shoes", Slug = "running-shoes", Description = "Lightweight running shoes with responsive cushioning.", Price = 109.99m, Stock = 60, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/running-shoes.jpg", SortOrder = 0 } } },
            new() { Name = "Wool Beanie", Slug = "wool-beanie", Description = "Soft merino wool beanie for cold weather.", Price = 19.99m, Stock = 90, CategoryId = categories[2].Id, Images = { new ProductImage { Url = "/images/products/wool-beanie.jpg", SortOrder = 0 } } },

            // Home
            new() { Name = "Ceramic Vase", Slug = "ceramic-vase", Description = "Handcrafted ceramic vase with minimalist design.", Price = 34.99m, Stock = 40, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/ceramic-vase.jpg", SortOrder = 0 } } },
            new() { Name = "Scented Candle Set", Slug = "scented-candle-set", Description = "Set of 3 soy wax candles with lavender, vanilla, and cedar.", Price = 29.99m, Stock = 75, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/scented-candles.jpg", SortOrder = 0 } } },
            new() { Name = "Throw Blanket", Slug = "throw-blanket", Description = "Soft fleece throw blanket in assorted colors.", Price = 39.99m, Stock = 55, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/throw-blanket.jpg", SortOrder = 0 } } },
            new() { Name = "Coffee Mug Set", Slug = "coffee-mug-set", Description = "Set of 4 stoneware coffee mugs, 12oz each.", Price = 24.99m, Stock = 65, CategoryId = categories[3].Id, Images = { new ProductImage { Url = "/images/products/coffee-mugs.jpg", SortOrder = 0 } } },

            // Toys
            new() { Name = "Building Blocks Set", Slug = "building-blocks-set", Description = "500-piece building blocks set for creative play.", Price = 44.99m, Stock = 45, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/building-blocks.jpg", SortOrder = 0 } } },
            new() { Name = "Remote Control Car", Slug = "remote-control-car", Description = "High-speed RC car with rechargeable battery.", Price = 59.99m, Stock = 30, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/rc-car.jpg", SortOrder = 0 } } },
            new() { Name = "Puzzle 1000 Pieces", Slug = "puzzle-1000-pieces", Description = "1000-piece jigsaw puzzle with landscape artwork.", Price = 19.99m, Stock = 80, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/puzzle.jpg", SortOrder = 0 } } },
            new() { Name = "Plush Teddy Bear", Slug = "plush-teddy-bear", Description = "Soft plush teddy bear, 12 inches tall.", Price = 22.99m, Stock = 100, CategoryId = categories[4].Id, Images = { new ProductImage { Url = "/images/products/teddy-bear.jpg", SortOrder = 0 } } }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }
}
