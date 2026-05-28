using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Sale?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Sale>> GetByCompanyAsync(Guid companyId, SaleFilterDto filter, CancellationToken ct = default);
        Task AddAsync(Sale sale, CancellationToken ct = default);
        Task AddItemsAsync(IEnumerable<SaleItem> items, CancellationToken ct = default);
        Task UpdateAsync(Sale sale, CancellationToken ct = default);
        Task DeleteItemsBySaleAsync(Guid saleId, CancellationToken ct = default);
        Task UpdateStatusAsync(Guid id, SaleStatus status, Guid updatedBy, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default);
        Task<decimal> GetTotalByCompanyAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default);
    }
}
