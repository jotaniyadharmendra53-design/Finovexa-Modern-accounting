using InvoiceSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace InvoiceSaaS.Infrastructure.Identity
{
   // ═══════════════════════════════════════════════════════════
//  JWT Settings
// ═══════════════════════════════════════════════════════════===
public class JwtSettings
{
    public string SecretKey          { get; set; } = default!;
    public string Issuer             { get; set; } = default!;
    public string Audience           { get; set; } = default!;
    public int    AccessTokenExpiry  { get; set; } = 60;
    public int    RefreshTokenExpiry { get; set; } = 7;
}

// ═══════════════════════════════════════════════════════════
//  JWT Service
// ═══════════════════════════════════════════════════════════
public class JwtService : IJwtService
{
    private readonly JwtSettings             _settings;
    private readonly IRefreshTokenRepository _refreshRepo;

    public JwtService(IOptions<JwtSettings> settings, IRefreshTokenRepository refreshRepo)
    {
        _settings    = settings.Value;
        _refreshRepo = refreshRepo;
    }

    public string GenerateAccessToken(Domain.Interfaces.JwtPayload payload)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   payload.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, payload.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("fullName", payload.FullName),
            new("roleName", payload.RoleName),
        };

        if (payload.CompanyId.HasValue)
            claims.Add(new Claim("companyId", payload.CompanyId.Value.ToString()));

        foreach (var perm in payload.Permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
                      .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public Domain.Interfaces.JwtPayload? ValidateAccessToken(string token)
    {
        try
        {
            var key       = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
            var handler   = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = _settings.Issuer,
                ValidateAudience         = true,
                ValidAudience            = _settings.Audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.FromSeconds(30)
            }, out _);

            // FindFirstValue is an extension method in Microsoft.AspNetCore.Authentication
            var userId    = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email     = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            var fullName  = principal.FindFirstValue("fullName");
            var roleName  = principal.FindFirstValue("roleName");
            var companyId = principal.FindFirstValue("companyId");
            var perms     = principal.FindAll("permission").Select(c => c.Value);

            if (userId is null || email is null) return null;

            return new Domain.Interfaces.JwtPayload(
                UserId:      Guid.Parse(userId),
                Email:       email,
                FullName:    fullName  ?? string.Empty,
                RoleName:    roleName  ?? string.Empty,
                CompanyId:   companyId is not null ? Guid.Parse(companyId) : null,
                Permissions: perms);
        }
        catch { return null; }
    }

    public async Task<TokenPair> RefreshTokensAsync(string refreshToken, string ipAddress, CancellationToken ct = default)
        => throw new NotImplementedException("Use AuthService.RefreshTokenAsync instead.");
}

// ═══════════════════════════════════════════════════════════
//  CurrentUserService
// ═══════════════════════════════════════════════════════════
public class CurrentUserService : ICurrentUserService
{
    private readonly ClaimsPrincipal? _principal;

    // IHttpContextAccessor is in Microsoft.AspNetCore.Http
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _principal = httpContextAccessor.HttpContext?.User;
    }

    public Guid? UserId
    {
        get
        {
            var val = _principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? _principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return val is not null ? Guid.Parse(val) : null;
        }
    }

    public Guid? CompanyId
    {
        get
        {
            var val = _principal?.FindFirstValue("companyId");
            return val is not null ? Guid.Parse(val) : null;
        }
    }

    public string? Email    => _principal?.FindFirstValue(JwtRegisteredClaimNames.Email)
                            ?? _principal?.FindFirstValue(ClaimTypes.Email);
    public string? FullName => _principal?.FindFirstValue("fullName");
    public string? RoleName => _principal?.FindFirstValue("roleName")
                            ?? _principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsSuperAdmin    => RoleName?.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> Permissions
        => _principal?.FindAll("permission").Select(c => c.Value)
           ?? Enumerable.Empty<string>();

    public bool HasPermission(string permissionCode)
        => IsSuperAdmin || Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}

// ═══════════════════════════════════════════════════════════
//  HasPermission Attribute
// ═══════════════════════════════════════════════════════════
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : Attribute
{
    public string Code { get; }
    public HasPermissionAttribute(string code) => Code = code;
}

}
