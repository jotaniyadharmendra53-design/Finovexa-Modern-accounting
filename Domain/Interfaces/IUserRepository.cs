using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  IUserRepository
    // ═══════════════════════════════════════════════════════════
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default);
        Task<IEnumerable<User>> GetAllWithRolesAsync(Guid? companyId = null, CancellationToken ct = default);
        Task<User?> GetWithRoleAndPermissionsAsync(Guid userId, CancellationToken ct = default);
        Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default);
        Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);

        // Links a user to a company via UserCompanies join table
        Task AddToCompanyAsync(Guid userId, Guid companyId, CancellationToken ct = default);

        // Checks if a user belongs to a specific company
        Task<bool> BelongsToCompanyAsync(Guid userId, Guid companyId, CancellationToken ct = default);

    }
}
