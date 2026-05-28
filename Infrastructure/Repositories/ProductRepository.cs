using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        public ProductRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Product>(
                "SELECT * FROM dbo.Products WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
        }

        public async Task<IEnumerable<Product>> GetByCompanyAsync(Guid companyId, string? search = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT * FROM dbo.Products
        WHERE CompanyId = @CompanyId AND IsDeleted = 0
        AND (@Search IS NULL OR Name LIKE '%' + @Search + '%' OR SKU LIKE '%' + @Search + '%')
        ORDER BY Name
        """;
            return await conn.QueryAsync<Product>(sql, new { CompanyId = companyId, Search = search });
        }

        public async Task AddAsync(Product p, CancellationToken ct = default)
        { await _db.Set<Product>().AddAsync(p, ct); await _db.SaveChangesAsync(ct); }

        public async Task UpdateAsync(Product p, CancellationToken ct = default)
        { _db.Set<Product>().Update(p); await _db.SaveChangesAsync(ct); }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Products SET IsDeleted=1,DeletedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id",
                new { Id = id, By = deletedBy });
        }

        public async Task<bool> SkuExistsAsync(string sku, Guid companyId, Guid? excludeId = null, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Products WHERE SKU=@SKU AND CompanyId=@CId AND IsDeleted=0 AND (@ExId IS NULL OR Id<>@ExId)",
                new { SKU = sku, CId = companyId, ExId = excludeId }) > 0;
        }
    }
}
