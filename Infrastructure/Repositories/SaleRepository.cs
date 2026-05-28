using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class SaleRepository : ISaleRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        public SaleRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT s.Id, s.CompanyId, s.ClientId, s.SaleNumber, s.Status,
               s.SaleDate, s.SubTotal, s.TaxRate, s.TaxAmount, s.Discount, s.Total,
               s.PaymentMethod, s.Notes, s.IsDeleted, s.CreatedAt, s.CreatedBy,
               c.Id, c.Name, c.Email,
               co.Id, co.Name, co.CurrencyCode
        FROM dbo.Sales s
        LEFT JOIN dbo.Clients   c  ON c.Id  = s.ClientId
        INNER JOIN dbo.Companies co ON co.Id = s.CompanyId
        WHERE s.Id=@Id AND s.IsDeleted=0
        """;
            var result = await conn.QueryAsync<Sale, Client, Company, Sale>(
                sql, (sa, cl, co) => { sa.Client = cl; sa.Company = co; return sa; },
                new { Id = id }, splitOn: "Id,Id");
            return result.FirstOrDefault();
        }

        public async Task<Sale?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        {
            var sale = await GetByIdAsync(id, ct);
            if (sale is null) return null;
            using var conn = _dapper.CreateConnection();
            var items = await conn.QueryAsync<SaleItem>(
                "SELECT * FROM dbo.SaleItems WHERE SaleId=@Id ORDER BY SortOrder", new { Id = id });
            sale.SaleItems = items.ToList();
            return sale;
        }

        public async Task<IEnumerable<Sale>> GetByCompanyAsync(Guid companyId, SaleFilterDto f, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var sql = new System.Text.StringBuilder("""
        SELECT s.Id, s.SaleNumber, s.ClientId, s.SaleDate, s.Status, s.Total, s.CreatedAt,
               c.Id, c.Name
        FROM dbo.Sales s
        LEFT JOIN dbo.Clients c ON c.Id = s.ClientId
        WHERE s.CompanyId=@CId AND s.IsDeleted=0
        """);
            var p = new DynamicParameters(); p.Add("CId", companyId);
            if (f.Status.HasValue) { sql.Append(" AND s.Status=@St"); p.Add("St", (byte)f.Status.Value); }
            if (f.ClientId.HasValue) { sql.Append(" AND s.ClientId=@Cl"); p.Add("Cl", f.ClientId); }
            if (f.DateFrom.HasValue) { sql.Append(" AND s.SaleDate>=@Df"); p.Add("Df", f.DateFrom); }
            if (f.DateTo.HasValue) { sql.Append(" AND s.SaleDate<=@Dt"); p.Add("Dt", f.DateTo); }
            if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (s.SaleNumber LIKE @S OR c.Name LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
            sql.Append(" ORDER BY s.SaleDate DESC");
            if (f.PageSize > 0) { sql.Append(" OFFSET @Off ROWS FETCH NEXT @Ps ROWS ONLY"); p.Add("Off", (f.Page - 1) * f.PageSize); p.Add("Ps", f.PageSize); }
            return await conn.QueryAsync<Sale, Client, Sale>(
                sql.ToString(), (s, c) => { s.Client = c; return s; }, p, splitOn: "Id");
        }

        public async Task AddAsync(Sale s, CancellationToken ct = default)
        { await _db.Set<Sale>().AddAsync(s, ct); await _db.SaveChangesAsync(ct); }

        public async Task AddItemsAsync(IEnumerable<SaleItem> items, CancellationToken ct = default)
        { await _db.Set<SaleItem>().AddRangeAsync(items, ct); await _db.SaveChangesAsync(ct); }

        public async Task UpdateAsync(Sale s, CancellationToken ct = default)
        { _db.Set<Sale>().Update(s); await _db.SaveChangesAsync(ct); }

        public async Task DeleteItemsBySaleAsync(Guid saleId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM dbo.SaleItems WHERE SaleId=@Id", new { Id = saleId });
        }

        public async Task UpdateStatusAsync(Guid id, SaleStatus status, Guid updatedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Sales SET Status=@St,UpdatedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id AND IsDeleted=0",
                new { St = (byte)status, By = updatedBy, Id = id });
        }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Sales SET IsDeleted=1,DeletedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id",
                new { Id = id, By = deletedBy });
        }

        public Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default)
            => AccSeqHelper.GetNextAsync(_dapper, companyId, "SALE");

        public async Task<decimal> GetTotalByCompanyAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Total),0) FROM dbo.Sales WHERE CompanyId=@CId AND IsDeleted=0 AND Status<>2 AND SaleDate BETWEEN @F AND @T",
                new { CId = companyId, F = from, T = to });
        }
    }
}
