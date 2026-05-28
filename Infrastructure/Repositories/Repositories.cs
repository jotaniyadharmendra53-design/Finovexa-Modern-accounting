using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
// ═══════════════════════════════════════════════════════════
//  UserRepository
// ═══════════════════════════════════════════════════════════
public class UserRepository : BaseRepository<User>, IUserRepository
    {

        public new async Task UpdateAsync(User user, CancellationToken ct = default)
        {
            // Use Dapper for the update to avoid EF tracking issues
            // and to ensure IsFirstLogin is saved correctly.
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
    UPDATE dbo.Users
    SET    FullName       = @FullName,
           Email          = @Email,
           PasswordHash   = @PasswordHash,
           RoleId         = @RoleId,
           Phone          = @Phone,
           IsActive       = @IsActive,
           IsFirstLogin   = @IsFirstLogin,
           UpdatedAt      = GETUTCDATE(),
           UpdatedBy      = @UpdatedBy
    WHERE  Id = @Id AND IsDeleted = 0
    """,
            new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PasswordHash,
                user.RoleId,
                user.Phone,
                user.IsActive,
                user.IsFirstLogin,
                user.UpdatedBy
            });
        }


        public UserRepository(ApplicationDbContext db, IDapperContext dapper) : base(db, dapper) { }

        // ── Dapper reads ─────────────────────────────────────────
        public override async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT u.*, r.Id, r.Name, r.Description, r.IsSystem, r.IsActive
            FROM   dbo.Users u
            INNER  JOIN dbo.Roles r ON r.Id = u.RoleId
            WHERE  u.Id = @Id AND u.IsDeleted = 0
            """;
            var result = await conn.QueryAsync<User, Role, User>(
                sql,
                (user, role) => { user.Role = role; return user; },
                new { Id = id },
                splitOn: "Id");
            return result.FirstOrDefault();
        }

        public override async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
            => await GetAllWithRolesAsync(null, ct);

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT u.*, r.Id, r.Name, r.Description, r.IsSystem, r.IsActive
            FROM   dbo.Users u
            INNER  JOIN dbo.Roles r ON r.Id = u.RoleId
            WHERE  u.Email = @Email AND u.IsDeleted = 0
            """;
            var result = await conn.QueryAsync<User, Role, User>(
                sql,
                (user, role) => { user.Role = role; return user; },
                new { Email = email.ToLower() },
                splitOn: "Id");
            return result.FirstOrDefault();
        }

        public async Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT COUNT(1) FROM dbo.Users
            WHERE  Email = @Email AND IsDeleted = 0
            AND    (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """;
            var count = await conn.ExecuteScalarAsync<int>(sql, new { Email = email.ToLower(), ExcludeId = excludeId });
            return count > 0;
        }

       
        public async Task<IEnumerable<User>> GetAllWithRolesAsync(
            Guid? companyId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT u.*, r.Id, r.Name, r.Description, r.IsSystem, r.IsActive
        FROM   dbo.Users u
        INNER  JOIN dbo.Roles r         ON r.Id  = u.RoleId
        LEFT   JOIN dbo.UserCompanies uc ON uc.UserId = u.Id
        WHERE  u.IsDeleted = 0
        AND    (@CompanyId IS NULL OR uc.CompanyId = @CompanyId)
        ORDER  BY u.FullName
        """;
            var result = await conn.QueryAsync<User, Role, User>(
                sql,
                (user, role) => { user.Role = role; return user; },
                new { CompanyId = companyId }, splitOn: "Id");
            return result.DistinctBy(u => u.Id);
        }



        public async Task<User?> GetWithRoleAndPermissionsAsync(Guid userId, CancellationToken ct = default)
            => await GetByIdAsync(userId, ct);

        public async Task UpdateLastLoginAsync(Guid userId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Users SET LastLoginAt = GETUTCDATE() WHERE Id = @Id",
                new { Id = userId });
        }

        public async Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT p.Code
            FROM   dbo.Users u
            INNER  JOIN dbo.Roles r ON r.Id = u.RoleId
            INNER  JOIN dbo.RolePermissions rp ON rp.RoleId = r.Id
            INNER  JOIN dbo.Permissions p ON p.Id = rp.PermissionId
            WHERE  u.Id = @UserId AND u.IsDeleted = 0
            AND    r.IsDeleted = 0 AND p.IsDeleted = 0
            """;
            return await conn.QueryAsync<string>(sql, new { UserId = userId });
        }

        // ── NEW: AddToCompanyAsync ────────────────────────────────
        // Links a user to a company via the UserCompanies join table.
        // Called by UserService.CreateAsync when a company user is
        // created, and by the company provisioning flow in Step 2.
        public async Task AddToCompanyAsync(
            Guid userId, Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
        IF NOT EXISTS (
            SELECT 1 FROM dbo.UserCompanies
            WHERE UserId = @UserId AND CompanyId = @CompanyId)
        BEGIN
            INSERT INTO dbo.UserCompanies (UserId, CompanyId, JoinedAt)
            VALUES (@UserId, @CompanyId, GETUTCDATE())
        END
        """, new { UserId = userId, CompanyId = companyId });
        }

        // ── NEW: BelongsToCompanyAsync ────────────────────────────
        public async Task<bool> BelongsToCompanyAsync(
            Guid userId, Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(
                """
            SELECT COUNT(1) FROM dbo.UserCompanies
            WHERE UserId = @UserId AND CompanyId = @CompanyId
            """,
                new { UserId = userId, CompanyId = companyId }) > 0;
        }

    }

    // ═══════════════════════════════════════════════════════════
    //  RoleRepository
    // ═══════════════════════════════════════════════════════════
    public class RoleRepository : BaseRepository<Role>, IRoleRepository
    {
        public RoleRepository(ApplicationDbContext db, IDapperContext dapper) : base(db, dapper) { }

        public override async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = "SELECT * FROM dbo.Roles WHERE Id = @Id AND IsDeleted = 0";
            return await conn.QueryFirstOrDefaultAsync<Role>(sql, new { Id = id });
        }

        public override async Task<IEnumerable<Role>> GetAllAsync(CancellationToken ct = default)
            => await GetAllWithPermissionsAsync(null,ct);

    
        public async Task<IEnumerable<Role>> GetAllWithPermissionsAsync(
            Guid? companyId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT r.*,
               (SELECT COUNT(*) FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id) AS PermissionCount,
               (SELECT COUNT(*) FROM dbo.Users u WHERE u.RoleId = r.Id AND u.IsDeleted = 0) AS UserCount
        FROM   dbo.Roles r
        WHERE  r.IsDeleted = 0
        AND    (
                  @CompanyId IS NULL
               OR r.CompanyId = @CompanyId
               OR r.CompanyId IS NULL       -- include global/system roles
               )
        ORDER  BY r.IsSystem DESC, r.Name
        """;
            var rows = await conn.QueryAsync<dynamic>(sql, new { CompanyId = companyId });
            return rows.Select(r => new Role
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsSystem = r.IsSystem,
                IsActive = r.IsActive,
                CompanyId = r.CompanyId,
                IsDeleted = r.IsDeleted,
                CreatedAt = r.CreatedAt,
                RolePermissions = Enumerable.Range(0, (int)r.PermissionCount)
                                    .Select(_ => new RolePermission()).ToList()
            });
        }



        public async Task<Role?> GetWithPermissionsAsync(Guid roleId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT r.* FROM dbo.Roles r WHERE r.Id = @Id AND r.IsDeleted = 0;
            SELECT rp.RoleId, rp.PermissionId FROM dbo.RolePermissions rp WHERE rp.RoleId = @Id;
            """;
            using var multi = await conn.QueryMultipleAsync(sql, new { Id = roleId });
            var role = await multi.ReadFirstOrDefaultAsync<Role>();
            if (role is null) return null;
            var perms = await multi.ReadAsync<RolePermission>();
            role.RolePermissions = perms.ToList();
            return role;
        }

        public async Task<bool> NameExistsAsync(
            string name, Guid companyId, Guid? excludeId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT COUNT(1) FROM dbo.Roles
        WHERE  Name = @Name
        AND    IsDeleted = 0
        AND    (@ExcludeId IS NULL OR Id <> @ExcludeId)
        AND    (CompanyId = @CompanyId OR CompanyId IS NULL)
        """;
            return await conn.ExecuteScalarAsync<int>(sql,
                new { Name = name, CompanyId = companyId, ExcludeId = excludeId }) > 0;
        }


        // ── GetByNameAsync — scoped lookup ────────────────────────
        public async Task<Role?> GetByNameAsync(
            string name, Guid? companyId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT TOP 1 * FROM dbo.Roles
        WHERE  Name      = @Name
        AND    IsDeleted = 0
        AND    (
                  @CompanyId IS NULL
               OR CompanyId  = @CompanyId
               OR CompanyId IS NULL
               )
        ORDER BY IsSystem DESC
        """;
            return await conn.QueryFirstOrDefaultAsync<Role>(sql,
                new { Name = name, CompanyId = companyId });
        }
        public async Task AssignPermissionsAsync(Guid roleId, IEnumerable<Guid> permissionIds, Guid assignedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Ensure we don't attempt to insert duplicate (RoleId, PermissionId) pairs.
                var distinctIds = (permissionIds ?? Enumerable.Empty<Guid>()).Distinct().ToList();

                if (!distinctIds.Any())
                {
                    tx.Commit();
                    return;
                }

                var rows = distinctIds.Select(pid => new
                {
                    RoleId = roleId,
                    PermissionId = pid,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = assignedBy
                });

                await conn.ExecuteAsync("""
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AssignedAt, AssignedBy)
            VALUES (@RoleId, @PermissionId, @AssignedAt, @AssignedBy)
            """, rows, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task RemoveAllPermissionsAsync(Guid roleId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId",
                new { RoleId = roleId });
        }

        public async Task<bool> HasUsersAssignedAsync(Guid roleId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE RoleId = @RoleId AND IsDeleted = 0",
                new { RoleId = roleId });
            return count > 0;
        }

        public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT p.*
            FROM   dbo.Permissions p
            INNER  JOIN dbo.RolePermissions rp ON rp.PermissionId = p.Id
            WHERE  rp.RoleId = @RoleId AND p.IsDeleted = 0
            """;
            return await conn.QueryAsync<Permission>(sql, new { RoleId = roleId });
        }

    }

    // ═══════════════════════════════════════════════════════════
    //  PermissionRepository
    // ═══════════════════════════════════════════════════════════
    public class PermissionRepository : IPermissionRepository
    {
        private readonly IDapperContext _dapper;
        public PermissionRepository(IDapperContext dapper) { _dapper = dapper; }

        public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<Permission>(
                "SELECT * FROM dbo.Permissions WHERE IsDeleted = 0 ORDER BY Module, Name");
        }

        public async Task<IEnumerable<IGrouping<string, Permission>>> GetGroupedByModuleAsync(CancellationToken ct = default)
        {
            var all = await GetAllAsync(ct);
            return all.GroupBy(p => p.Module);
        }

        public async Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Permission>(
                "SELECT * FROM dbo.Permissions WHERE Code = @Code AND IsDeleted = 0",
                new { Code = code });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  CompanyRepository
    // ═══════════════════════════════════════════════════════════
    public class CompanyRepository : BaseRepository<Company>, ICompanyRepository
    {
        public CompanyRepository(ApplicationDbContext db, IDapperContext dapper) : base(db, dapper) { }

        public override async Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Company>(
                "SELECT * FROM dbo.Companies WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id });
        }

        public override async Task<IEnumerable<Company>> GetAllAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<Company>(
                "SELECT * FROM dbo.Companies WHERE IsDeleted = 0 ORDER BY Name");
        }

        public async Task<Company?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Company>(
                "SELECT * FROM dbo.Companies WHERE OwnerId = @OwnerId AND IsDeleted = 0",
                new { OwnerId = ownerId });
        }

        public async Task<Company?> GetByUserAsync(Guid userId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT c.*
            FROM   dbo.Companies c
            INNER  JOIN dbo.UserCompanies uc ON uc.CompanyId = c.Id
            WHERE  uc.UserId = @UserId AND c.IsDeleted = 0
            """;
            return await conn.QueryFirstOrDefaultAsync<Company>(sql, new { UserId = userId });
        }

        public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT COUNT(1) FROM dbo.Companies
            WHERE  Name = @Name AND IsDeleted = 0
            AND    (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """;
            return await conn.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId }) > 0;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ClientRepository
    // ═══════════════════════════════════════════════════════════
    public class ClientRepository : BaseRepository<Client>, IClientRepository
    {
        public ClientRepository(ApplicationDbContext db, IDapperContext dapper) : base(db, dapper) { }

        public override async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Client>(
                "SELECT * FROM dbo.Clients WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id });
        }

        public override async Task<IEnumerable<Client>> GetAllAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<Client>(
                "SELECT * FROM dbo.Clients WHERE IsDeleted = 0 ORDER BY Name");
        }

        //public async Task<IEnumerable<Client>> GetByCompanyAsync(Guid companyId, string? search = null, CancellationToken ct = default)
        //{
        //    using var conn = _dapper.CreateConnection();
        //    const string sql = """
        //    SELECT c.*,
        //           (SELECT COUNT(*) FROM dbo.Invoices i
        //            WHERE i.ClientId = c.Id AND i.IsDeleted = 0) AS InvoiceCount
        //    FROM   dbo.Clients c
        //    WHERE  c.CompanyId = @CompanyId AND c.IsDeleted = 0
        //    AND    (@Search IS NULL
        //            OR c.Name  LIKE '%' + @Search + '%'
        //            OR c.Email LIKE '%' + @Search + '%'
        //            OR c.Phone LIKE '%' + @Search + '%')
        //    ORDER  BY c.Name
        //    """;
        //    var rows = await conn.QueryAsync<dynamic>(sql, new { CompanyId = companyId, Search = search });
        //    return rows.Select(r => new Client
        //    {
        //        Id = r.Id,
        //        CompanyId = r.CompanyId,
        //        Name = r.Name,
        //        Email = r.Email,
        //        Phone = r.Phone,
        //        Address = r.Address,
        //        City = r.City,
        //        State = r.State,
        //        Country = r.Country,
        //        PostalCode = r.PostalCode,
        //        TaxNumber = r.TaxNumber,
        //        Notes = r.Notes,
        //        IsActive = r.IsActive,
        //        IsDeleted = r.IsDeleted,
        //        CreatedAt = r.CreatedAt,
        //        // Populate Invoices list just for count
        //        Invoices = Enumerable.Range(0, (int)(r.InvoiceCount ?? 0))
        //                       .Select(_ => new Invoice()).ToList()
        //    });
        //}

        public async Task<IEnumerable<Client>> GetByCompanyAsync(Guid companyId, string? search = null, bool? isActive = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
    SELECT c.Id, c.CompanyId, c.Name, c.Email, c.Phone, c.Address,
           c.City, c.State, c.Country, c.PostalCode, c.TaxNumber,
           c.Notes, c.IsActive, c.IsDeleted, c.CreatedAt,
           (SELECT COUNT(*) FROM dbo.Invoices i
            WHERE i.ClientId = c.Id AND i.IsDeleted = 0) AS InvoiceCount
    FROM   dbo.Clients c
    WHERE  c.CompanyId = @CompanyId AND c.IsDeleted = 0
    AND    (@IsActive IS NULL OR c.IsActive = @IsActive)
    AND    (@Search IS NULL
            OR c.Name  LIKE '%' + @Search + '%'
            OR c.Email LIKE '%' + @Search + '%'
            OR c.Phone LIKE '%' + @Search + '%')
    ORDER  BY c.Name
    """;

            // Use a concrete DTO instead of dynamic
            var rows = await conn.QueryAsync<ClientWithCount>(sql, new { CompanyId = companyId, Search = search ,IsActive = isActive });

            return rows.Select(r => new Client
            {
                Id = r.Id,
                CompanyId = r.CompanyId,
                Name = r.Name,
                Email = r.Email,
                Phone = r.Phone,
                Address = r.Address,
                City = r.City,
                State = r.State,
                Country = r.Country,
                PostalCode = r.PostalCode,
                TaxNumber = r.TaxNumber,
                Notes = r.Notes,
                IsActive = r.IsActive,
                IsDeleted = r.IsDeleted,
                CreatedAt = r.CreatedAt,
                Invoices = Enumerable.Range(0, r.InvoiceCount)
                               .Select(_ => new Invoice()).ToList()
            });
        }

        // ── Private helper DTO ────────────────────────────────────
        private class ClientWithCount
        {
            public Guid Id { get; set; }
            public Guid CompanyId { get; set; }
            public string Name { get; set; } = default!;
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? Country { get; set; }
            public string? PostalCode { get; set; }
            public string? TaxNumber { get; set; }
            public string? Notes { get; set; }
            public bool IsActive { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public int InvoiceCount { get; set; }
        }



        public async Task<bool> EmailExistsAsync(string email, Guid companyId, Guid? excludeId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT COUNT(1) FROM dbo.Clients
            WHERE  Email = @Email AND CompanyId = @CompanyId AND IsDeleted = 0
            AND    (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """;
            return await conn.ExecuteScalarAsync<int>(sql,
                new { Email = email, CompanyId = companyId, ExcludeId = excludeId }) > 0;
        }

        public async Task<bool> HasInvoicesAsync(Guid clientId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Invoices WHERE ClientId = @ClientId AND IsDeleted = 0",
                new { ClientId = clientId }) > 0;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  RefreshTokenRepository
    // ═══════════════════════════════════════════════════════════
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;

        public RefreshTokenRepository(ApplicationDbContext db, IDapperContext dapper)
        {
            _db = db; _dapper = dapper;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<RefreshToken>(
                "SELECT * FROM dbo.RefreshTokens WHERE Token = @Token",
                new { Token = token });
        }

        public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            await _db.RefreshTokens.AddAsync(token, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeAsync(string token, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.RefreshTokens SET IsRevoked = 1, RevokedAt = GETUTCDATE() WHERE Token = @Token",
                new { Token = token });
        }

        public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.RefreshTokens SET IsRevoked = 1, RevokedAt = GETUTCDATE() WHERE UserId = @UserId AND IsRevoked = 0",
                new { UserId = userId });
        }

        public async Task CleanExpiredAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.RefreshTokens WHERE ExpiresAt < GETUTCDATE() AND IsRevoked = 1");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PasswordResetTokenRepository
    // ═══════════════════════════════════════════════════════════
    public class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;

        public PasswordResetTokenRepository(ApplicationDbContext db, IDapperContext dapper)
        {
            _db = db; _dapper = dapper;
        }

        public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<PasswordResetToken>(
                "SELECT * FROM dbo.PasswordResetTokens WHERE Token = @Token",
                new { Token = token });
        }

        public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
        {
            await _db.PasswordResetTokens.AddAsync(token, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkUsedAsync(string token, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.PasswordResetTokens SET IsUsed = 1, UsedAt = GETUTCDATE() WHERE Token = @Token",
                new { Token = token });
        }

        public async Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.PasswordResetTokens SET IsUsed = 1, UsedAt = GETUTCDATE() WHERE UserId = @UserId AND IsUsed = 0",
                new { UserId = userId });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  EmailLogRepository
    // ═══════════════════════════════════════════════════════════
    public class EmailLogRepository : IEmailLogRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;

        public EmailLogRepository(ApplicationDbContext db, IDapperContext dapper)
        {
            _db = db; _dapper = dapper;
        }

        public async Task AddAsync(EmailLog log, CancellationToken ct = default)
        {
            await _db.EmailLogs.AddAsync(log, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IEnumerable<EmailLog>> GetRecentAsync(int count = 50, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<EmailLog>(
                "SELECT TOP (@Count) * FROM dbo.EmailLogs ORDER BY CreatedAt DESC",
                new { Count = count });
        }
    }
}
