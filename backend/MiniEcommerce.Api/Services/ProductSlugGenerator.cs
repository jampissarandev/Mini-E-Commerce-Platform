using System.Text.RegularExpressions;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Pure helper for product slug generation. Extracted from
/// <c>AdminProductsController</c> so the slug rules live in one place and are
/// unit-testable without an HTTP harness.
/// </summary>
/// <remarks>
/// Rules (matches what the live controller shipped pre-refactor):
///   <list type="bullet">
///     <item>Lowercase, ASCII-only (a-z, 0-9, space, hyphen).</item>
///     <item>Whitespace runs collapse to one hyphen.</item>
///     <item>Hyphen runs collapse to one hyphen.</item>
///     <item>Leading/trailing hyphens trimmed.</item>
///   </list>
/// If the caller already provided a non-empty <c>requestedSlug</c>, the
/// generator returns it untouched — the caller is responsible for any further
/// validation (length, character set).
/// </remarks>
public static class ProductSlugGenerator
{
    public static string GenerateFromName(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }
}
