using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

/// <summary>
/// The response envelope every endpoint returns. <c>Success</c> is true on
/// 2xx; on failure <c>Error</c> carries a machine-readable <c>Code</c> and a
/// human-readable <c>Message</c>. The frontend parses <c>Code</c>, never
/// <c>Message</c> (CONTEXT.md rule #5).
/// </summary>
[SwaggerSchema("The response envelope every endpoint returns.")]
public record ApiResponse<T>
{
    [SwaggerSchema("True on 2xx responses; false on errors.")]
    public bool Success { get; init; }

    [SwaggerSchema("The payload, present when Success is true.")]
    public T? Data { get; init; }

    [SwaggerSchema("Error details, present when Success is false.")]
    public ApiError? Error { get; init; }

    [SwaggerSchema("Pagination metadata for list endpoints.")]
    public Meta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, Meta? meta = null) =>
        new() { Success = true, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(ApiError error) =>
        new() { Success = false, Error = error };
}

[SwaggerSchema("The response envelope for endpoints with no payload.")]
public record ApiResponse
{
    [SwaggerSchema("True on 2xx responses; false on errors.")]
    public bool Success { get; init; }

    [SwaggerSchema("Error details, present when Success is false.")]
    public ApiError? Error { get; init; }

    [SwaggerSchema("Pagination metadata for list endpoints.")]
    public Meta? Meta { get; init; }

    public static ApiResponse Ok(Meta? meta = null) =>
        new() { Success = true, Meta = meta };

    public static ApiResponse Fail(ApiError error) =>
        new() { Success = false, Error = error };
}
