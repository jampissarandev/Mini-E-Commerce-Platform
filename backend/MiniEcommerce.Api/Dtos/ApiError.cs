using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("Machine-readable error payload carried by the response envelope.")]
public record ApiError
{
    [SwaggerSchema("Machine-readable error code (e.g. PRODUCT_NOT_FOUND). Parse this, never Message.")]
    public string Code { get; init; } = string.Empty;

    [SwaggerSchema("Human-readable error message.")]
    public string Message { get; init; } = string.Empty;

    [SwaggerSchema("Optional per-field validation errors (field → messages).")]
    public Dictionary<string, string[]>? Details { get; init; }
}

[SwaggerSchema("Pagination metadata on list responses.")]
public record Meta
{
    [SwaggerSchema("Current page (1-based).")]
    public int Page { get; init; }

    [SwaggerSchema("Items per page.")]
    public int PageSize { get; init; }

    [SwaggerSchema("Total number of items across all pages.")]
    public long TotalCount { get; init; }

    [SwaggerSchema("Total number of pages.")]
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
