namespace HireLens.SharedKernel;

/// <summary>
/// Thrown only when a domain invariant is violated. Callers must not use this
/// for ordinary validation that the UI can recover from — those use Result.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
