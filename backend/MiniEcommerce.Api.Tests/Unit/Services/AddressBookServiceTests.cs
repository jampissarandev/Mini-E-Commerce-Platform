using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests at the <see cref="MiniEcommerce.Api.Interfaces.IAddressBookService"/> seam.
/// Drives the ADR 0004 single-default invariant for the three rules that
/// mutate <c>IsDefault</c>: create, set-default, delete-with-promote.
///
/// Uses the EF Core in-memory provider (single-threaded, no transaction
/// support) — the Postgres race-window tests are deferred until the
/// Testcontainers-based concurrent test from Task 24 lands.
/// </summary>
public class AddressBookServiceTests
{
    private const string CustomerA = "customer-a";
    private const string CustomerB = "customer-b";

    private static AddressBookService NewSut(out ApplicationDbContext context)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AddressBookServiceTests_{Guid.NewGuid():N}")
            .Options;
        context = new ApplicationDbContext(options);
        return new AddressBookService(context);
    }

    private static Address Seed(
        ApplicationDbContext ctx,
        string customerId,
        bool isDefault = false,
        DateTime? createdAt = null,
        string fullName = "Test")
    {
        var a = new Address
        {
            CustomerId = customerId,
            FullName = fullName,
            Street = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "US",
            Phone = "+1-555-0000",
            IsDefault = isDefault,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
        ctx.Addresses.Add(a);
        ctx.SaveChanges();
        return a;
    }

    // ─────────────── CreateForCustomerAsync ───────────────

    [Fact]
    public async Task CreateForCustomerAsync_FirstAddress_IsDefault()
    {
        var sut = NewSut(out var ctx);

        var address = await sut.CreateForCustomerAsync(
            CustomerA, "First", "1 St", "City", "00000", "US", "+1-555-0000");

        address.IsDefault.Should().BeTrue("the first address is auto-default per ADR 0004");
    }

    [Fact]
    public async Task CreateForCustomerAsync_SecondAddress_NotDefault()
    {
        var sut = NewSut(out var ctx);
        Seed(ctx, CustomerA, isDefault: true);

        var address = await sut.CreateForCustomerAsync(
            CustomerA, "Second", "1 St", "City", "00000", "US", "+1-555-0000");

        address.IsDefault.Should().BeFalse("only one address per customer may be default");
    }

    [Fact]
    public async Task CreateForCustomerAsync_DoesNotAffectOtherCustomersDefault()
    {
        var sut = NewSut(out var ctx);
        Seed(ctx, CustomerA, isDefault: true);
        Seed(ctx, CustomerB, isDefault: true);

        await sut.CreateForCustomerAsync(
            CustomerB, "Second", "1 St", "City", "00000", "US", "+1-555-0000");

        // Customer A's default should be untouched
        var customerADefault = ctx.Addresses.Single(a => a.CustomerId == CustomerA && a.IsDefault);
        customerADefault.FullName.Should().Be("Test");
        // Customer B now has exactly one default
        var customerBDefaults = ctx.Addresses.Where(a => a.CustomerId == CustomerB && a.IsDefault).ToList();
        customerBDefaults.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateForCustomerAsync_PersistsAllFields()
    {
        var sut = NewSut(out var ctx);

        var address = await sut.CreateForCustomerAsync(
            CustomerA,
            fullName: "Jane Doe",
            street: "123 Main St",
            city: "Springfield",
            postalCode: "62704",
            country: "US",
            phone: "+1-555-0100");

        address.Id.Should().BeGreaterThan(0);
        address.CustomerId.Should().Be(CustomerA);
        address.FullName.Should().Be("Jane Doe");
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Springfield");
        address.PostalCode.Should().Be("62704");
        address.Country.Should().Be("US");
        address.Phone.Should().Be("+1-555-0100");
        address.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ─────────────── SetDefaultAsync ───────────────

    [Fact]
    public async Task SetDefaultAsync_AddressExists_ReturnsTrue()
    {
        var sut = NewSut(out var ctx);
        var target = Seed(ctx, CustomerA, isDefault: false);

        var ok = await sut.SetDefaultAsync(CustomerA, target.Id);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_AddressExists_SetsTargetAsDefault()
    {
        var sut = NewSut(out var ctx);
        var target = Seed(ctx, CustomerA, isDefault: false);

        await sut.SetDefaultAsync(CustomerA, target.Id);

        var reloaded = ctx.Addresses.Single(a => a.Id == target.Id);
        reloaded.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_UnsetsPreviousDefault()
    {
        var sut = NewSut(out var ctx);
        var previous = Seed(ctx, CustomerA, isDefault: true);
        var target = Seed(ctx, CustomerA, isDefault: false);

        await sut.SetDefaultAsync(CustomerA, target.Id);

        var previousReloaded = ctx.Addresses.Single(a => a.Id == previous.Id);
        previousReloaded.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_LeavesExactlyOneDefault()
    {
        var sut = NewSut(out var ctx);
        Seed(ctx, CustomerA, isDefault: true);
        var target = Seed(ctx, CustomerA, isDefault: false);
        Seed(ctx, CustomerA, isDefault: false, fullName: "Third");

        await sut.SetDefaultAsync(CustomerA, target.Id);

        var defaults = ctx.Addresses.Where(a => a.CustomerId == CustomerA && a.IsDefault).ToList();
        defaults.Should().HaveCount(1);
        defaults[0].Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task SetDefaultAsync_AddressDoesNotExist_ReturnsFalse()
    {
        var sut = NewSut(out var ctx);

        var ok = await sut.SetDefaultAsync(CustomerA, addressId: 99999);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_AddressBelongsToAnotherCustomer_ReturnsFalse()
    {
        var sut = NewSut(out var ctx);
        var victim = Seed(ctx, CustomerB, isDefault: true);

        var ok = await sut.SetDefaultAsync(CustomerA, victim.Id);

        ok.Should().BeFalse();
        // Victim's default flag should be unchanged
        ctx.Addresses.Single(a => a.Id == victim.Id).IsDefault.Should().BeTrue();
    }

    // ─────────────── DeleteAsync ───────────────

    [Fact]
    public async Task DeleteAsync_AddressExists_ReturnsTrue()
    {
        var sut = NewSut(out var ctx);
        var address = Seed(ctx, CustomerA, isDefault: true);

        var ok = await sut.DeleteAsync(CustomerA, address.Id);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_AddressExists_RemovesIt()
    {
        var sut = NewSut(out var ctx);
        var address = Seed(ctx, CustomerA, isDefault: true);

        await sut.DeleteAsync(CustomerA, address.Id);

        ctx.Addresses.Any(a => a.Id == address.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_AddressDoesNotExist_ReturnsFalse()
    {
        var sut = NewSut(out var ctx);

        var ok = await sut.DeleteAsync(CustomerA, addressId: 99999);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_AddressBelongsToAnotherCustomer_ReturnsFalse()
    {
        var sut = NewSut(out var ctx);
        var victim = Seed(ctx, CustomerB, isDefault: true);

        var ok = await sut.DeleteAsync(CustomerA, victim.Id);

        ok.Should().BeFalse();
        ctx.Addresses.Any(a => a.Id == victim.Id).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_DeletedWasDefault_PromotesMostRecentRemaining()
    {
        var sut = NewSut(out var ctx);
        var oldest = Seed(ctx, CustomerA, isDefault: true, createdAt: DateTime.UtcNow.AddMinutes(-10));
        Seed(ctx, CustomerA, isDefault: false, createdAt: DateTime.UtcNow.AddMinutes(-5));
        var newest = Seed(ctx, CustomerA, isDefault: false, createdAt: DateTime.UtcNow);

        await sut.DeleteAsync(CustomerA, oldest.Id);

        var defaults = ctx.Addresses.Where(a => a.CustomerId == CustomerA && a.IsDefault).ToList();
        defaults.Should().HaveCount(1);
        defaults[0].Id.Should().Be(newest.Id);
    }

    [Fact]
    public async Task DeleteAsync_DeletedWasNonDefault_LeavesDefaultIntact()
    {
        var sut = NewSut(out var ctx);
        var oldest = Seed(ctx, CustomerA, isDefault: true, createdAt: DateTime.UtcNow.AddMinutes(-10));
        var middle = Seed(ctx, CustomerA, isDefault: false, createdAt: DateTime.UtcNow.AddMinutes(-5));
        Seed(ctx, CustomerA, isDefault: false, createdAt: DateTime.UtcNow);

        await sut.DeleteAsync(CustomerA, middle.Id);

        var defaults = ctx.Addresses.Where(a => a.CustomerId == CustomerA && a.IsDefault).ToList();
        defaults.Should().HaveCount(1);
        defaults[0].Id.Should().Be(oldest.Id);
    }

    [Fact]
    public async Task DeleteAsync_DeletedOnlyAddress_LeavesZeroAddresses()
    {
        var sut = NewSut(out var ctx);
        var only = Seed(ctx, CustomerA, isDefault: true);

        await sut.DeleteAsync(CustomerA, only.Id);

        ctx.Addresses.Where(a => a.CustomerId == CustomerA).Should().BeEmpty();
    }
}
