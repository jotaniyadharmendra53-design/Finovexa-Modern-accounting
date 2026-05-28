using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.FiscalYear;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IFiscalYearService
    {
        // ── Queries ───────────────────────────────────────────────
        Task<ServiceResult<IEnumerable<FiscalYearDto>>> GetAllAsync(Guid companyId, CancellationToken ct = default);
        Task<ServiceResult<FiscalYearDto>> GetCurrentAsync(Guid companyId, CancellationToken ct = default);
        Task<ServiceResult<FiscalYearDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

        // Returns null-safe: if no FY found for date → returns null (don't block)
        Task<ServiceResult<FiscalYearDto?>> GetForDateAsync(Guid companyId, DateTime date, CancellationToken ct = default);

        // Pre-close validation — call this before showing the close confirmation
        Task<ServiceResult<FiscalYearCloseCheckDto>> PreCloseCheckAsync(Guid fiscalYearId, CancellationToken ct = default);

        // ── Commands ──────────────────────────────────────────────
        // Creates the first FY for a new company (called on company creation)
        Task<ServiceResult<FiscalYearDto>> OpenFirstAsync(Guid companyId, int startMonth, Guid userId, CancellationToken ct = default);

        // Creates the next consecutive FY (only allowed when current FY is Open)
        Task<ServiceResult<FiscalYearDto>> OpenNextAsync(Guid companyId, Guid userId, CancellationToken ct = default);

        // Locks the FY — irreversible — optionally opens next year automatically
        Task<ServiceResult<FiscalYearDto>> CloseAsync(CloseFiscalYearDto dto, Guid userId, CancellationToken ct = default);

        // Update notes only
        Task<ServiceResult<FiscalYearDto>> UpdateNotesAsync(UpdateFiscalYearDto dto, Guid userId, CancellationToken ct = default);

        // ── Lock-check helper (used by Invoice/Expense/Payment services) ──
        // Returns error message if the date falls in a locked FY, null if safe to proceed
        Task<string?> GetLockViolationAsync(Guid companyId, DateTime transactionDate, CancellationToken ct = default);
    }

}
