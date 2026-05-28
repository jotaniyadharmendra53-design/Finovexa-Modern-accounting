using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        public VendorRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Vendor>(
                "SELECT * FROM dbo.Vendors WHERE Id=@Id AND IsDeleted=0", new { Id = id });
        }

        public async Task<IEnumerable<Vendor>> GetByCompanyAsync(Guid companyId, string? search = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT v.*,
          (SELECT COUNT(*) FROM dbo.Expenses e WHERE e.VendorId=v.Id AND e.IsDeleted=0) AS ExpenseCount
        FROM dbo.Vendors v
        WHERE v.CompanyId=@CId AND v.IsDeleted=0
        AND (@Search IS NULL OR v.Name LIKE '%'+@Search+'%' OR v.Email LIKE '%'+@Search+'%')
        ORDER BY v.Name
        """;
            return await conn.QueryAsync<Vendor>(sql, new { CId = companyId, Search = search });
        }

        public async Task AddAsync(Vendor v, CancellationToken ct = default)
        { await _db.Set<Vendor>().AddAsync(v, ct); await _db.SaveChangesAsync(ct); }

        public async Task UpdateAsync(Vendor v, CancellationToken ct = default)
        { _db.Set<Vendor>().Update(v); await _db.SaveChangesAsync(ct); }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Vendors SET IsDeleted=1,DeletedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id",
                new { Id = id, By = deletedBy });
        }

        public async Task<bool> HasExpensesAsync(Guid vendorId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Expenses WHERE VendorId=@Id AND IsDeleted=0",
                new { Id = vendorId }) > 0;
        }
    }
}
