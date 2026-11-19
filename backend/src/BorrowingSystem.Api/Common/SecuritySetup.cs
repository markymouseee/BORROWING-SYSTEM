using System.Threading.RateLimiting;
using BorrowingSystem.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BorrowingSystem.Api.Common;

/// <summary>
/// Cross-cutting security: request rate limits and HTTP security headers.
/// Account lockout itself lives in <see cref="LoginThrottle"/>.
/// </summary>
public static class SecuritySetup
{
    public const string LoginRateLimit = "login";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services)
    {
        services.AddSingleton<LoginThrottle>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Second line of defence for sign-in: one machine may not hammer the endpoint,
            // whatever emails it cycles through (the per-account lockout cannot see that).
            options.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

            // Generous ceiling for the whole API, to blunt scripted abuse.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientIp(http),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));

            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                    ? (int)Math.Ceiling(wait.TotalSeconds)
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Too many requests from this computer. Please wait a moment and try again."
                };
                problem.Extensions["retryAfterSeconds"] = retryAfter;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, ct);
            };
        });

        return services;
    }

    /// <summary>Baseline OWASP response headers on every response.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            await next();
        });

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
