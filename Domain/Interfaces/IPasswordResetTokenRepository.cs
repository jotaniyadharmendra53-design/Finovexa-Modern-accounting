using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IPasswordResetTokenRepository
    // ═══════════════════════════════════════════════════════════
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default);
        Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
        Task MarkUsedAsync(string token, CancellationToken ct = default);
        Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default);
    }
}
