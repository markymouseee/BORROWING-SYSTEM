using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BorrowingSystem.Api.Features.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; init; } = "lcc-borrowing";
    public string Audience { get; init; } = "lcc-borrowing";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpiresInMinutes { get; init; } = 480;

    public SymmetricSecurityKey GetSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (options.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        services.AddSingleton(options);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = options.GetSecurityKey(),
                    NameClaimType = AppClaims.Name,
                    RoleClaimType = AppClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();
        return services;
    }
}

public static class AppClaims
{
    public const string UserId = "sub";
    public const string Name = "name";
    public const string Role = "role";
}
