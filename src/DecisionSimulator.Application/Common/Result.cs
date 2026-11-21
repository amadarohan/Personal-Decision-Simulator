namespace DecisionSimulator.Application.Common;

/// <summary>
/// The outcome of an application operation. Failures carry a machine-readable
/// code plus a message that is safe to show to the user, so the web layer never
/// has to interpret an exception to decide what to display.
/// </summary>
public sealed class Result
{
    private Result(bool succeeded, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public bool Failed => !Succeeded;

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}

/// <summary>An operation outcome that carries a value on success.</summary>
public sealed class Result<T>
{
    private Result(bool succeeded, T? value, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public bool Failed => !Succeeded;

    // CA1000 warns about static members on generic types. For a result type the
    // factory methods are the whole point: they are what makes the success and
    // failure states unambiguous at every call site. Suppressed here rather than
    // project-wide so the rule still applies everywhere else.
#pragma warning disable CA1000
    public static Result<T> Success(T value) => new(true, value, null, null);

    public static Result<T> Failure(string errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);
#pragma warning restore CA1000
}

/// <summary>Stable error codes shared between the application and web layers.</summary>
public static class ErrorCodes
{
    public const string NotFound = "not_found";
    public const string ValidationFailed = "validation_failed";
    public const string InvalidState = "invalid_state";
    public const string Conflict = "conflict";
    public const string MissingAssumptions = "missing_assumptions";
    public const string Unsupported = "unsupported";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string Forbidden = "forbidden";
}
