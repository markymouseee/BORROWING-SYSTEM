namespace BorrowingSystem.Api.Common;

/// <summary>A business rule was violated (maps to HTTP 400).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>The request clashes with existing state, e.g. a double booking (maps to HTTP 409).</summary>
public sealed class ConflictException(string message) : DomainException(message);

/// <summary>The requested record does not exist (maps to HTTP 404).</summary>
public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For<T>(int id) => new($"{typeof(T).Name} #{id} was not found.");
}

/// <summary>The caller is signed in but may not touch this record (maps to HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);
