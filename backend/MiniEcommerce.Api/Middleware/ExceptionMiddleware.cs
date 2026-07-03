using System.Net;
using System.Text.Json;
using MiniEcommerce.Api.Dtos;

namespace MiniEcommerce.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, error) = exception switch
        {
            Exceptions.NotFoundException ex =>
                (HttpStatusCode.NotFound,
                 new ApiError { Code = "NOT_FOUND", Message = ex.Message }),

            Exceptions.ValidationException ex =>
                (HttpStatusCode.BadRequest,
                 new ApiError { Code = "VALIDATION_ERROR", Message = ex.Message, Details = ex.Errors }),

            Exceptions.BusinessRuleException ex =>
                (HttpStatusCode.Conflict,
                 new ApiError { Code = ex.Code, Message = ex.Message }),

            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized,
                 new ApiError { Code = "UNAUTHORIZED", Message = "You are not authorized." }),

            _ =>
                (HttpStatusCode.InternalServerError,
                 new ApiError { Code = "INTERNAL_ERROR", Message = "An unexpected error occurred." })
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.Fail(error);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
