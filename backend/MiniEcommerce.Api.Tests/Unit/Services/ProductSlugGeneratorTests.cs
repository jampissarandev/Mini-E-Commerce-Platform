using FluentAssertions;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Pin-tests for the product slug generator. These pin the slug format that
/// every existing Product's <c>Slug</c> column was populated with (so the
/// generator can't quietly drift into a different format and break every
/// product URL in the catalog).
/// </summary>
public class ProductSlugGeneratorTests
{
    [Theory]
    [InlineData("Laptop Pro", "laptop-pro")]
    [InlineData("  Laptop Pro  ", "laptop-pro")]
    [InlineData("Laptop    Pro", "laptop-pro")]
    [InlineData("Laptop---Pro", "laptop-pro")]
    [InlineData("Laptop Pro 15\"", "laptop-pro-15")]
    [InlineData("Men's T-Shirt", "mens-t-shirt")]
    [InlineData("USB-C Hub", "usb-c-hub")]
    [InlineData("Throw Blanket!", "throw-blanket")]
    public void GenerateFromName_ProducesKebabCaseLowerAscii(string input, string expected)
    {
        ProductSlugGenerator.GenerateFromName(input).Should().Be(expected);
    }

    [Fact]
    public void GenerateFromName_EmptyAfterStrip_ReturnsEmpty()
    {
        ProductSlugGenerator.GenerateFromName("!!!").Should().BeEmpty();
    }

    [Fact]
    public void GenerateFromName_AllWhitespace_ReturnsEmpty()
    {
        ProductSlugGenerator.GenerateFromName("   ").Should().BeEmpty();
    }
}
