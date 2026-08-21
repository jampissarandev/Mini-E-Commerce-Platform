using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Category
        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.ParentCategory)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Product
        builder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.Price).HasPrecision(18, 2);
        });

        // ProductImage
        builder.Entity<ProductImage>(e =>
        {
            e.HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductVariant — ADR 0003 (Task 27)
        builder.Entity<ProductVariant>(e =>
        {
            e.HasIndex(pv => pv.Sku).IsUnique();
            e.HasOne(pv => pv.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(pv => pv.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Cart
        builder.Entity<Cart>(e =>
        {
            e.HasIndex(c => c.CustomerId).IsUnique();
            e.HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CartItem — ADR 0003: FK switches from Product to ProductVariant
        builder.Entity<CartItem>(e =>
        {
            e.HasIndex(ci => new { ci.CartId, ci.ProductVariantId }).IsUnique();
            e.HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ci => ci.ProductVariant)
                .WithMany(pv => pv.CartItems)
                .HasForeignKey(ci => ci.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(ci => ci.UnitPrice).HasPrecision(18, 2);
        });

        // Order
        builder.Entity<Order>(e =>
        {
            e.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(o => o.Subtotal).HasPrecision(18, 2);
            e.Property(o => o.ShippingFee).HasPrecision(18, 2);
            e.Property(o => o.Total).HasPrecision(18, 2);
        });

        // OrderItem — ADR 0003: FK switches from Product to ProductVariant
        builder.Entity<OrderItem>(e =>
        {
            e.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(oi => oi.ProductVariant)
                .WithMany(pv => pv.OrderItems)
                .HasForeignKey(oi => oi.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
        });

        // RefreshToken — ADR 0005 (Task 25)
        builder.Entity<RefreshToken>(e =>
        {
            e.HasOne(rt => rt.Customer)
                .WithMany()
                .HasForeignKey(rt => rt.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rt => rt.TokenHash).IsUnique();
            e.HasIndex(rt => rt.CustomerId);
            e.HasOne(rt => rt.ReplacedBy)
                .WithMany()
                .HasForeignKey(rt => rt.ReplacedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Address — ADR 0004 (Task 26)
        builder.Entity<Address>(e =>
        {
            e.HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.CustomerId, a.IsDefault })
                .HasFilter(null); // no filter needed for InMemory compat
            // ADR 0004 invariant: at-most-one IsDefault=true per customer.
            // Backstops the concurrent-first-INSERT race at the DB level
            // (two callers can both observe AnyAsync=false before either
            // commits; the second INSERT with IsDefault=true fails the
            // unique constraint and AddressBookService retries as
            // non-default). The InMemory provider used by tests ignores
            // indexes, so the constraint is checked in app code there
            // (single-threaded by construction).
            e.HasIndex(a => a.CustomerId)
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_Addresses_OneDefaultPerCustomer");
        });
    }
}
