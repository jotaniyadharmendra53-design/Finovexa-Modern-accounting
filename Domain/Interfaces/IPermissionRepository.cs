using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IPermissionRepository
    // ═══════════════════════════════════════════════════════════
    public interface IPermissionRepository
    {
        Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<IGrouping<string, Permission>>> GetGroupedByModuleAsync(CancellationToken ct = default);
        Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
    }
}
