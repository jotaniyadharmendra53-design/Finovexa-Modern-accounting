using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        
        public PaymentRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Payment>(
                "SELECT * FROM dbo.Payments WHERE Id=@Id AND IsDeleted=0", new { Id = id });
        }

        public async Task<IEnumerable<Payment>> GetByCompanyAsync(Guid companyId, PaymentFilterDto f, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var sql = new System.Text.StringBuilder("""
        SELECT p.*, i.InvoiceNumber, e.ExpenseNumber, v.Name AS VendorName, c.Name AS ClientName
        FROM dbo.Payments p
        LEFT JOIN dbo.Invoices i ON i.Id = p.InvoiceId
        LEFT JOIN dbo.Expenses e ON e.Id = p.ExpenseId
        LEFT JOIN dbo.Vendors  v ON v.Id = p.VendorId
        LEFT JOIN dbo.Clients  c ON c.Id = p.ClientId
        WHERE p.CompanyId=@CId AND p.IsDeleted=0
        """);
            var p = new DynamicParameters(); p.Add("CId", companyId);
            if (f.Direction.HasValue) { sql.Append(" AND p.Direction=@Dir"); p.Add("Dir", (byte)f.Direction.Value); }
            if (f.DateFrom.HasValue) { sql.Append(" AND p.PaymentDate>=@Df"); p.Add("Df", f.DateFrom); }
            if (f.DateTo.HasValue) { sql.Append(" AND p.PaymentDate<=@Dt"); p.Add("Dt", f.DateTo); }
            if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND p.PaymentNumber LIKE @S"); p.Add("S", $"%{f.Search}%"); }
            sql.Append(" ORDER BY p.PaymentDate DESC");
            if (f.PageSize > 0) { sql.Append(" OFFSET @Off ROWS FETCH NEXT @Ps ROWS ONLY"); p.Add("Off", (f.Page - 1) * f.PageSize); p.Add("Ps", f.PageSize); }
            return await conn.QueryAsync<Payment>(sql.ToString(), p);
        }

        public async Task AddAsync(Payment pay, CancellationToken ct = default)
        { await _db.Set<Payment>().AddAsync(pay, ct); await _db.SaveChangesAsync(ct); }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Payments SET IsDeleted=1,DeletedAt=GETUTCDATE() WHERE Id=@Id",
                new { Id = id });
        }

        public Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default)
            => AccSeqHelper.GetNextAsync(_dapper, companyId, "PAY");

        public async Task<decimal> GetTotalInboundAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM dbo.Payments WHERE CompanyId=@CId AND IsDeleted=0 AND Direction=0 AND PaymentDate BETWEEN @F AND @T",
                new { CId = companyId, F = from, T = to });
        }

        public async Task<decimal> GetTotalOutboundAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM dbo.Payments WHERE CompanyId=@CId AND IsDeleted=0 AND Direction=1 AND PaymentDate BETWEEN @F AND @T",
                new { CId = companyId, F = from, T = to });
        }
    }
}
