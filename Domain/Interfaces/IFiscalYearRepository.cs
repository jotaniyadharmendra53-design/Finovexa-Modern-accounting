using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface IFiscalYearRepository
    {
        // ── Queries ───────────────────────────────────────────────
        Task<FiscalYear?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<FiscalYear>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
        Task<FiscalYear?> GetCurrentAsync(Guid companyId, CancellationToken ct = default);
        Task<FiscalYear?> GetForDateAsync(Guid companyId, DateTime date, CancellationToken ct = default);
        Task<FiscalYear?> GetOpenAsync(Guid companyId, CancellationToken ct = default);
        Task<bool> HasOverlapAsync(Guid companyId, DateTime start, DateTime end, Guid? excludeId, CancellationToken ct = default);

        // ── Commands ──────────────────────────────────────────────
        Task AddAsync(FiscalYear fy, CancellationToken ct = default);
        Task UpdateAsync(FiscalYear fy, CancellationToken ct = default);
        Task ClearDefaultAsync(Guid companyId, CancellationToken ct = default);
    }
}
