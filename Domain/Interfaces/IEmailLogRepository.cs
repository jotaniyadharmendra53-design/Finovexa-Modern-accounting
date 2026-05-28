using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IEmailLogRepository
    // ═══════════════════════════════════════════════════════════
    public interface IEmailLogRepository
    {
        Task AddAsync(EmailLog log, CancellationToken ct = default);
        Task<IEnumerable<EmailLog>> GetRecentAsync(int count = 50, CancellationToken ct = default);
    }
}
