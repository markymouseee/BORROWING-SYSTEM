using System.Security.Claims;
using BorrowingSystem.Api.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BorrowingSystem.Api.Features.Auth;

public sealed class TokenService(JwtOptions options, TimeProvider time)
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(options.ExpiresInMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(AppClaims.Name, user.FullName),
                new Claim(AppClaims.Role, user.Role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(options.GetSecurityKey(), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
