using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IInvoiceRepository
    // ═══════════════════════════════════════════════════════════
    public interface IInvoiceRepository : IRepository<Invoice>
    {
        Task<Invoice?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Invoice>> GetByCompanyAsync(Guid companyId, InvoiceFilterDto filter, CancellationToken ct = default);
        Task<IEnumerable<Invoice>> GetByClientAsync(Guid clientId, CancellationToken ct = default);
        Task<string> GetNextInvoiceNumberAsync(Guid companyId, CancellationToken ct = default);
        Task<InvoiceSummaryStats> GetStatsAsync(Guid companyId, CancellationToken ct = default);
        Task<IEnumerable<Invoice>> GetOverdueAsync(CancellationToken ct = default);
        Task UpdateStatusAsync(Guid id,InvoiceStatus status, Guid updatedBy, CancellationToken ct = default);
        Task AddItemsAsync(IEnumerable<InvoiceItem> items, CancellationToken ct = default);
        Task DeleteItemsByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
        Task UpdateInvoiceAsync(Invoice invoice, CancellationToken ct = default);
        Task UpdatePaidAmountAsync(Guid id, decimal paidAmount, InvoiceStatus status, Guid updatedBy, CancellationToken ct = default);
        Task<IEnumerable<CurrencyRevenueRow>> GetCurrencyStatsAsync(
    Guid companyId, CancellationToken ct = default);

        Task<IEnumerable<MonthlyRevenueRow>> GetMonthlyRevenueAsync(
    Guid companyId, CancellationToken ct = default);

        Task AddEditHistoryAsync(InvoiceEditHistory entry, CancellationToken ct = default);
        Task<IEnumerable<InvoiceEditHistory>> GetEditHistoryAsync(Guid invoiceId, CancellationToken ct = default);
        Task WriteOffAsync(Guid id, Guid updatedBy, CancellationToken ct = default);

    }
}
