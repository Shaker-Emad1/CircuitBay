using System;

namespace CircuitBay.Application.Common;

public abstract class AppException(string message, string code, Exception? innerException = null) 
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed class ValidationAppException(string message, Exception? inner = null) 
    : AppException(message, "validation_failed", inner);

public sealed class NotFoundAppException(string message, Exception? inner = null) 
    : AppException(message, "not_found", inner);

public sealed class ConflictAppException(string message, Exception? inner = null) 
    : AppException(message, "conflict", inner);

public sealed class UnauthorizedAppException(string message, Exception? inner = null) 
    : AppException(message, "auth_failed", inner);

public sealed class ConcurrencyAppException(string message, Exception? inner = null) 
    : AppException(message, "concurrency_error", inner);
