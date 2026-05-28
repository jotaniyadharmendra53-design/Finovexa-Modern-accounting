using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IClientRepository
    // ═══════════════════════════════════════════════════════════
    public interface IClientRepository : IRepository<Client>
    {
        Task<IEnumerable<Client>> GetByCompanyAsync(Guid companyId, string? search = null, bool? isActive = null, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string email, Guid companyId, Guid? excludeId = null, CancellationToken ct = default);
        Task<bool> HasInvoicesAsync(Guid clientId, CancellationToken ct = default);
    }
}
