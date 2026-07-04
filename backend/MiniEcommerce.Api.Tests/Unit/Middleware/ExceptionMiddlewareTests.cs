using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;
using MiniEcommerce.Api.Middleware;

namespace MiniEcommerce.Api.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for <see cref="ExceptionMiddleware"/>. Each test wires a fake
/// <c>RequestDelegate</c> that throws a specific exception type, invokes the
/// middleware, and asserts the response status code + JSON envelope.
/// </summary>
public class ExceptionMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<(int StatusCode, ApiResponse Body)> InvokeAsync(Exception thrown)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionMiddleware(
            next: _ => throw thrown,
            logger: NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var json = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        var body = JsonSerializer.Deserialize<ApiResponse>(json, JsonOptions);

        return ((int)context.Response.StatusCode, body!);
    }

    [Fact]
    public async Task NotFoundException_MapsTo_404_NotFound()
    {
        var (status, body) = await InvokeAsync(new NotFoundException("Entity not found"));

        status.Should().Be((int)HttpStatusCode.NotFound);
        body.Success.Should().BeFalse();
        body.Error.Should().NotBeNull();
        body.Error!.Code.Should().Be("NOT_FOUND");
        body.Error.Message.Should().Be("Entity not found");
    }

    [Fact]
    public async Task ValidationException_MapsTo_400_WithErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = new[] { "Email is required" },
            ["Password"] = new[] { "Password too short" }
        };

        var (status, body) = await InvokeAsync(new ValidationException(errors));

        status.Should().Be((int)HttpStatusCode.BadRequest);
        body.Error!.Code.Should().Be("VALIDATION_ERROR");
        body.Error.Details.Should().NotBeNull();
        body.Error.Details!["Email"].Should().ContainSingle().Which.Should().Be("Email is required");
    }

    [Fact]
    public async Task BusinessRuleException_MapsTo_409_WithCustomCode()
    {
        var (status, body) = await InvokeAsync(
            new BusinessRuleException("INSUFFICIENT_STOCK", "Only 3 items left in stock"));

        status.Should().Be((int)HttpStatusCode.Conflict);
        body.Error!.Code.Should().Be("INSUFFICIENT_STOCK");
        body.Error.Message.Should().Be("Only 3 items left in stock");
    }

    [Fact]
    public async Task UnauthorizedAccessException_MapsTo_401()
    {
        var (status, body) = await InvokeAsync(new UnauthorizedAccessException("nope"));

        status.Should().Be((int)HttpStatusCode.Unauthorized);
        body.Error!.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task UnhandledException_MapsTo_500_WithGenericMessage()
    {
        var (status, body) = await InvokeAsync(new InvalidOperationException("internal secret"));

        status.Should().Be((int)HttpStatusCode.InternalServerError);
        body.Error!.Code.Should().Be("INTERNAL_ERROR");
        // Critically: must NOT leak the original message to the client.
        body.Error.Message.Should().NotContain("internal secret");
        body.Error.Message.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task NoException_PassesThrough_WithoutModifyingResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        var middleware = new ExceptionMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200); // DefaultHttpContext default
    }
}
