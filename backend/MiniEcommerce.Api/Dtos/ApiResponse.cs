namespace MiniEcommerce.Api.Dtos;

public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public Meta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, Meta? meta = null) =>
        new() { Success = true, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(ApiError error) =>
        new() { Success = false, Error = error };
}

public record ApiResponse
{
    public bool Success { get; init; }
    public ApiError? Error { get; init; }
    public Meta? Meta { get; init; }

    public static ApiResponse Ok(Meta? meta = null) =>
        new() { Success = true, Meta = meta };

    public static ApiResponse Fail(ApiError error) =>
        new() { Success = false, Error = error };
}
