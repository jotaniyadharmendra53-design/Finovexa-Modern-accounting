using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface IVendorRepository
    {
        Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Vendor>> GetByCompanyAsync(Guid companyId, string? search = null, CancellationToken ct = default);
        Task AddAsync(Vendor vendor, CancellationToken ct = default);
        Task UpdateAsync(Vendor vendor, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<bool> HasExpensesAsync(Guid vendorId, CancellationToken ct = default);
    }
}
