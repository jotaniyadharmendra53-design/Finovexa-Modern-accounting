using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  ICompanyRepository
    // ═══════════════════════════════════════════════════════════
    public interface ICompanyRepository : IRepository<Company>
    {
        Task<Company?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
        Task<Company?> GetByUserAsync(Guid userId, CancellationToken ct = default);
        Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    }
}
