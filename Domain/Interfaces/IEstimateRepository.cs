using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface IEstimateRepository
    {
        Task<Estimate?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Estimate?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Estimate>> GetByCompanyAsync(Guid companyId, EstimateFilterDto filter, CancellationToken ct = default);
        Task AddAsync(Estimate estimate, CancellationToken ct = default);
        Task AddItemsAsync(IEnumerable<EstimateItem> items, CancellationToken ct = default);
        Task UpdateAsync(Estimate estimate, CancellationToken ct = default);
        Task DeleteItemsByEstimateAsync(Guid estimateId, CancellationToken ct = default);
        Task UpdateStatusAsync(Guid id, EstimateStatus status, Guid updatedBy, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default);
    }
}
