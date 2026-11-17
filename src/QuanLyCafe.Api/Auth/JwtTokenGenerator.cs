using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuanLyCafe.Application.Staff;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Api.Auth;

// Claim theo mục 4.3.4: UserID (sub), họ tên (name), vai trò (role).
public class JwtTokenGenerator(IConfiguration config) : IJwtTokenGenerator
{
    public (string Token, DateTime ExpiresAt) Generate(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(config.GetValue("Jwt:ExpiresHours", 8));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.UserID.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim("role", user.Role.RoleName),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(config), SecurityAlgorithms.HmacSha256),
        });
        return (token, expiresAt);
    }

    public static SymmetricSecurityKey SigningKey(IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Thiếu Jwt:Key (>= 32 ký tự) — đặt bằng dotnet user-secrets.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
