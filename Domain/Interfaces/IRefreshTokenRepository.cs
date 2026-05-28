using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IRefreshTokenRepository
    // ═══════════════════════════════════════════════════════════
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
        Task AddAsync(RefreshToken token, CancellationToken ct = default);
        Task RevokeAsync(string token, CancellationToken ct = default);
        Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
        Task CleanExpiredAsync(CancellationToken ct = default);
    }
}
