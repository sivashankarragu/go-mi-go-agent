namespace GoMigo.Application.Common;

/// <summary>Request input is invalid. Mapped to HTTP 400 with per-field errors.</summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Requested resource does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Request conflicts with the current state of a resource. Mapped to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
