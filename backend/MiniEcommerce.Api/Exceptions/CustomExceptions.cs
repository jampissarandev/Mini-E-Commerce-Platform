namespace MiniEcommerce.Api.Exceptions;

public class NotFoundException : Exception
{
    /// <summary>
    /// Machine-readable error code surfaced to API clients. Defaults to
    /// <c>"NOT_FOUND"</c> for the generic case; domain-specific callers
    /// (e.g. <c>AdminOrdersController</c>) pass a code like
    /// <c>"ORDER_NOT_FOUND"</c> so the client can branch on it.
    /// </summary>
    public string Code { get; }

    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(message)
    {
        Code = code;
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
        Code = "NOT_FOUND";
    }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Machine-readable error code surfaced to API clients. Defaults to
    /// <c>"VALIDATION_ERROR"</c>; callers can pass a domain-specific code
    /// (e.g. <c>"INVALID_STATUS"</c>) so the client can branch on it.
    /// </summary>
    public string Code { get; }

    public ValidationException(Dictionary<string, string[]> errors, string code = "VALIDATION_ERROR")
        : base("Validation failed.")
    {
        Errors = errors;
        Code = code;
    }

    public ValidationException(string message, string code = "VALIDATION_ERROR")
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { { string.Empty, [message] } };
        Code = code;
    }
}

public class BusinessRuleException : Exception
{
    public string Code { get; }

    public BusinessRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
