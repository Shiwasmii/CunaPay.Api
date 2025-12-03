using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CunaPay.Api.Services;

public class JwtService
{
    private readonly string _secret;
    private readonly string _expiresIn;

    public JwtService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ?? "dev_jwt_secret_key_min_32_chars";
        _expiresIn = configuration["Jwt:ExpiresIn"] ?? "12h";
    }

    public string GenerateToken(string userId, string email, string? role = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secret);
        
        var claims = new List<Claim>
        {
            new Claim("uid", userId),
            new Claim("email", email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Agregar rol si se proporciona
        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim("role", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expires = _expiresIn switch
        {
            "5min" => Helpers.DateTimeHelper.UtcNow.AddMinutes(5),
            "12h" => Helpers.DateTimeHelper.UtcNow.AddHours(12),
            "24h" => Helpers.DateTimeHelper.UtcNow.AddHours(24),
            "7d" => Helpers.DateTimeHelper.UtcNow.AddDays(7),
            _ => Helpers.DateTimeHelper.UtcNow.AddMinutes(5) // Por defecto 5 minutos
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secret);
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }
}
