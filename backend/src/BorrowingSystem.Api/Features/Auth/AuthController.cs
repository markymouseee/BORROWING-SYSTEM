using System.ComponentModel.DataAnnotations;
using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Features.Auth;

public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    TokenService tokens,
    LoginThrottle throttle,
    ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(SecuritySetup.LoginRateLimit)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = Domain.User.NormalizeEmail(request.Email);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Locked out: refuse before even looking at the password, so a correct guess during
        // the lockout window teaches the attacker nothing.
        var status = throttle.Check(email, ip);
        if (status.IsLocked) return LockedOut(status);

        var user = await db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Email == email, ct);

        // Always run one hash verification, even for unknown emails, so response time does not
        // reveal which emails have accounts.
        var verification = hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyHash, request.Password);

        if (user is not { IsActive: true } || verification == PasswordVerificationResult.Failed)
        {
            status = throttle.RegisterFailure(email, ip);
            if (status.IsLocked) return LockedOut(status);

            // Same message whether the email or the password is wrong, so accounts can't be probed.
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Sign-in failed",
                Detail = "Incorrect email or password."
            };
            problem.Extensions["attemptsRemaining"] = status.AttemptsRemaining;
            return Unauthorized(problem);
        }

        throttle.RegisterSuccess(email, ip);

        // Upgrade old hashes transparently when the framework's algorithm settings change.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(hasher.HashPassword(user, request.Password));
            await db.SaveChangesAsync(ct);
        }

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    private ObjectResult LockedOut(ThrottleStatus status)
    {
        var seconds = (int)Math.Ceiling(status.RetryAfter.TotalSeconds);
        Response.Headers.RetryAfter = seconds.ToString();

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many attempts",
            Detail = "Too many failed sign-in attempts. Please wait before trying again."
        };
        problem.Extensions["retryAfterSeconds"] = seconds;
        return StatusCode(StatusCodes.Status429TooManyRequests, problem);
    }

    // A fixed hash to verify against when the email is unknown (see the timing note in Login).
    private static readonly User DummyUser = new("x", "x@x", UserRole.Admin, null);
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, Guid.NewGuid().ToString());

    [Authorize]
    [HttpGet("me")]
    public async Task<UserDto> Me(CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Department).FirstAsync(u => u.Id == currentUser.Id, ct);
        return user.ToDto();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await currentUser.GetAsync(ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new DomainException("Your current password is incorrect.");

        user.SetPasswordHash(hasher.HashPassword(user, request.NewPassword));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
