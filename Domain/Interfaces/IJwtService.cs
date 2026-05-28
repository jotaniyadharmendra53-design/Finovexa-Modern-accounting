using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  JWT Service Interface
    // ═══════════════════════════════════════════════════════════
    public interface IJwtService
    {
        string GenerateAccessToken(JwtPayload payload);
        string GenerateRefreshToken();
        JwtPayload? ValidateAccessToken(string token);
        Task<TokenPair> RefreshTokensAsync(string refreshToken, string ipAddress, CancellationToken ct = default);
    }

    public record JwtPayload(
    Guid UserId,
    string Email,
    string FullName,
    string RoleName,
    Guid? CompanyId,
    IEnumerable<string> Permissions
);

    public record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    DateTime RefreshTokenExpiry
);

}
