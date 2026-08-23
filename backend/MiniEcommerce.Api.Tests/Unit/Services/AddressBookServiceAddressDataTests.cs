using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Pin-tests for the <see cref="AddressData"/> bundle that
/// <see cref="MiniEcommerce.Api.Services.AddressBookService"/> consumes.
/// These don't replace the existing tests — they pin the new seam so a future
/// "let's break AddressData back into 7 string params" refactor is caught.
/// </summary>
public class AddressBookServiceAddressDataTests
{
    private const string CustomerA = "customer-a";

    private static AddressBookService NewSut(out ApplicationDbContext context)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AddressDataTests_{Guid.NewGuid():N}")
            .Options;
        context = new ApplicationDbContext(options);
        return new AddressBookService(context);
    }

    private static AddressData NewData(string fullName = "Jane Doe") =>
        new(
            FullName: fullName,
            Street: "123 Main St",
            City: "Springfield",
            PostalCode: "62704",
            Country: "US",
            Phone: "+1-555-0100");

    [Fact]
    public async Task CreateForCustomerAsync_AcceptsAddressData_AndPersistsAllFields()
    {
        var sut = NewSut(out var ctx);

        var address = await sut.CreateForCustomerAsync(CustomerA, NewData("Jane Doe"));

        address.CustomerId.Should().Be(CustomerA);
        address.FullName.Should().Be("Jane Doe");
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Springfield");
        address.PostalCode.Should().Be("62704");
        address.Country.Should().Be("US");
        address.Phone.Should().Be("+1-555-0100");
        address.IsDefault.Should().BeTrue("first address is auto-default per ADR 0004");
    }

    [Fact]
    public async Task UpdateAsync_AcceptsAddressData_AndPersistsAllFields()
    {
        var sut = NewSut(out var ctx);
        var existing = new Address
        {
            CustomerId = CustomerA,
            FullName = "Old",
            Street = "Old St",
            City = "Old City",
            PostalCode = "00000",
            Country = "US",
            Phone = "+1-555-0000",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Addresses.Add(existing);
        ctx.SaveChanges();

        var ok = await sut.UpdateAsync(CustomerA, existing.Id, NewData("New"));

        ok.Should().BeTrue();
        var reloaded = ctx.Addresses.Single(a => a.Id == existing.Id);
        reloaded.FullName.Should().Be("New");
        reloaded.Street.Should().Be("123 Main St");
        reloaded.City.Should().Be("Springfield");
        reloaded.PostalCode.Should().Be("62704");
        reloaded.Country.Should().Be("US");
        reloaded.Phone.Should().Be("+1-555-0100");
        reloaded.IsDefault.Should().BeTrue("Update must not touch IsDefault — that's SetDefault's job");
    }

    [Fact]
    public async Task UpdateAsync_AddressNotFound_ReturnsFalse()
    {
        var sut = NewSut(out _);

        var ok = await sut.UpdateAsync(CustomerA, addressId: 99999, NewData());

        ok.Should().BeFalse();
    }
}
