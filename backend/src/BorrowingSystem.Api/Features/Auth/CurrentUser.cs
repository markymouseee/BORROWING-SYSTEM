using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Auth;

/// <summary>Who is making the current request. Services depend on this instead of HttpContext.</summary>
public interface ICurrentUser
{
    int Id { get; }
    bool IsAdmin { get; }
    Task<User> GetAsync(CancellationToken ct = default);
}

public sealed class CurrentUser(IHttpContextAccessor accessor, AppDbContext db) : ICurrentUser
{
    private User? _cached;

    public int Id =>
        int.TryParse(accessor.HttpContext?.User.FindFirst(AppClaims.UserId)?.Value, out var id)
            ? id
            : throw new ForbiddenException("You are not signed in.");

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole(nameof(UserRole.Admin)) ?? false;

    public async Task<User> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == Id, ct);
        if (user is not { IsActive: true })
            throw new ForbiddenException("Your account is no longer active.");

        return _cached = user;
    }
}
