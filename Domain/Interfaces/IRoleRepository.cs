using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IRoleRepository
    // ═══════════════════════════════════════════════════════════
    public interface IRoleRepository : IRepository<Role>
    {
        //Task<IEnumerable<Role>> GetAllWithPermissionsAsync(CancellationToken ct = default);
        //Task<Role?> GetWithPermissionsAsync(Guid roleId, CancellationToken ct = default);
        //Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
        //Task AssignPermissionsAsync(Guid roleId, IEnumerable<Guid> permissionIds, Guid assignedBy, CancellationToken ct = default);
        //Task RemoveAllPermissionsAsync(Guid roleId, CancellationToken ct = default);
        //Task<bool> HasUsersAssignedAsync(Guid roleId, CancellationToken ct = default);
        //Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default);
        //Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);


        // ── Queries ───────────────────────────────────────────────
        // companyId = the company whose roles to fetch
        // returns company roles + IsSystem global roles (Super Admin etc.)
        Task<IEnumerable<Role>> GetAllWithPermissionsAsync(Guid? companyId = null, CancellationToken ct = default);
        Task<Role?> GetWithPermissionsAsync(Guid roleId, CancellationToken ct = default);

        // Name uniqueness check — scoped to a company
        Task<bool> NameExistsAsync(string name, Guid companyId, Guid? excludeId = null, CancellationToken ct = default);

        // Find by name — scoped to company (used for restore logic)
        Task<Role?> GetByNameAsync(string name, Guid? companyId = null, CancellationToken ct = default);

        // ── Commands ──────────────────────────────────────────────
        Task AssignPermissionsAsync(Guid roleId, IEnumerable<Guid> permissionIds, Guid assignedBy, CancellationToken ct = default);
        Task RemoveAllPermissionsAsync(Guid roleId, CancellationToken ct = default);

        // ── Checks ────────────────────────────────────────────────
        Task<bool> HasUsersAssignedAsync(Guid roleId, CancellationToken ct = default);
        Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default);

    }
}
