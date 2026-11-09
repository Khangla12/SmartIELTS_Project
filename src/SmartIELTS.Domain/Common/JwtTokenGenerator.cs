using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartIELTS.Domain.Entities;

namespace SmartIELTS.Domain.Common;

public class JwtOptions
{
    public string Issuer { get; set; } = "SmartIELTS";
    public string Audience { get; set; } = "SmartIELTS.Client";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 480;
}

/// <summary>Tạo JWT thật (HS256) thay cho token giả "mock-jwt-token-..." trước đây.</summary>
public static class JwtTokenGenerator
{
    public static string Generate(User user, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Key))
            throw new InvalidOperationException("Chưa cấu hình Jwt:Key trong appsettings.json");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
