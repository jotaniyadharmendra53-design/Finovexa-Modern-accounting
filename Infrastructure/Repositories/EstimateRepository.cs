using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class EstimateRepository : IEstimateRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        public EstimateRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Estimate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT e.Id, e.CompanyId, e.ClientId, e.EstimateNumber, e.Status,
               e.IssueDate, e.ExpiryDate, e.SubTotal, e.TaxRate, e.TaxAmount,
               e.Discount, e.Total, e.Notes, e.Terms, e.ConvertedInvoiceId,
               e.SentAt, e.IsDeleted, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy,
               c.Id, c.Name, c.Email,
               co.Id, co.Name, co.CurrencyCode
        FROM dbo.Estimates e
        INNER JOIN dbo.Clients   c  ON c.Id  = e.ClientId
        INNER JOIN dbo.Companies co ON co.Id = e.CompanyId
        WHERE e.Id=@Id AND e.IsDeleted=0
        """;
            var result = await conn.QueryAsync<Estimate, Client, Company, Estimate>(
                sql, (est, cl, co) => 
                { 
                 est.Client = cl; 
                 est.Company = co; 
                    return est; },
                new { Id = id }, splitOn: "Id,Id");
            return result.FirstOrDefault();
        }

        public async Task<Estimate?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        {
            var estimate = await GetByIdAsync(id, ct);
            if (estimate is null) return null;
            using var conn = _dapper.CreateConnection();
            var items = await conn.QueryAsync<EstimateItem>(
                "SELECT * FROM dbo.EstimateItems WHERE EstimateId=@Id ORDER BY SortOrder", new { Id = id });
            estimate.EstimateItems = items.ToList();
            return estimate;
        }

        public async Task<IEnumerable<Estimate>> GetByCompanyAsync(Guid companyId, EstimateFilterDto f, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var sql = new System.Text.StringBuilder("""
        SELECT e.Id, e.CompanyId, e.ClientId, e.EstimateNumber, e.Status,
               e.IssueDate, e.ExpiryDate, e.Total, e.CreatedAt,
               c.Id, c.Name, c.Email
        FROM dbo.Estimates e
        INNER JOIN dbo.Clients c ON c.Id = e.ClientId
        WHERE e.CompanyId=@CId AND e.IsDeleted=0
        """);
            var p = new DynamicParameters(); p.Add("CId", companyId);
            if (f.Status.HasValue) { sql.Append(" AND e.Status=@St"); p.Add("St", (byte)f.Status.Value); }
            if (f.ClientId.HasValue) { sql.Append(" AND e.ClientId=@Cl"); p.Add("Cl", f.ClientId); }
            if (f.DateFrom.HasValue) { sql.Append(" AND e.IssueDate>=@Df"); p.Add("Df", f.DateFrom); }
            if (f.DateTo.HasValue) { sql.Append(" AND e.IssueDate<=@Dt"); p.Add("Dt", f.DateTo); }
            if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (e.EstimateNumber LIKE @S OR c.Name LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
            sql.Append(" ORDER BY e.CreatedAt DESC");
            if (f.PageSize > 0) { sql.Append(" OFFSET @Off ROWS FETCH NEXT @Ps ROWS ONLY"); p.Add("Off", (f.Page - 1) * f.PageSize); p.Add("Ps", f.PageSize); }
            return await conn.QueryAsync<Estimate, Client, Estimate>(
                sql.ToString(), (e, c) => { e.Client = c; return e; }, p, splitOn: "Id");
        }

        public async Task AddAsync(Estimate e, CancellationToken ct = default)
        { await _db.Set<Estimate>().AddAsync(e, ct); await _db.SaveChangesAsync(ct); }

        public async Task AddItemsAsync(IEnumerable<EstimateItem> items, CancellationToken ct = default)
        { await _db.Set<EstimateItem>().AddRangeAsync(items, ct); await _db.SaveChangesAsync(ct); }

        public async Task UpdateAsync(Estimate e, CancellationToken ct = default)
        { _db.Set<Estimate>().Update(e); await _db.SaveChangesAsync(ct); }

        public async Task DeleteItemsByEstimateAsync(Guid estimateId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM dbo.EstimateItems WHERE EstimateId=@Id", new { Id = estimateId });
        }

        public async Task UpdateStatusAsync(Guid id, EstimateStatus status, Guid updatedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var extra = status == EstimateStatus.Sent ? ", SentAt = GETUTCDATE()" : "";
            await conn.ExecuteAsync($"""
        UPDATE dbo.Estimates SET Status=@St, UpdatedAt=GETUTCDATE(), UpdatedBy=@By {extra}
        WHERE Id=@Id AND IsDeleted=0
        """, new { St = (byte)status, By = updatedBy, Id = id });
        }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Estimates SET IsDeleted=1,DeletedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id",
                new { Id = id, By = deletedBy });
        }

        public Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default)
            => AccSeqHelper.GetNextAsync(_dapper, companyId, "EST");
    }
}
