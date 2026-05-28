using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;
        public ExpenseRepository(ApplicationDbContext db, IDapperContext dapper) { _db = db; _dapper = dapper; }

        public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT e.*, v.Id, v.Name FROM dbo.Expenses e
        LEFT JOIN dbo.Vendors v ON v.Id = e.VendorId
        WHERE e.Id=@Id AND e.IsDeleted=0
        """;
            var result = await conn.QueryAsync<Expense, Vendor, Expense>(
                sql, (e, v) => { e.Vendor = v; return e; }, new { Id = id }, splitOn: "Id");
            return result.FirstOrDefault();
        }

        public async Task<IEnumerable<Expense>> GetByCompanyAsync(Guid companyId, ExpenseFilterDto f, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var sql = new System.Text.StringBuilder("""
        SELECT e.*, v.Id, v.Name FROM dbo.Expenses e
        LEFT JOIN dbo.Vendors v ON v.Id = e.VendorId
        WHERE e.CompanyId=@CId AND e.IsDeleted=0
        """);
            var p = new DynamicParameters();
            p.Add("CId", companyId);
            if (!string.IsNullOrEmpty(f.Category)) { sql.Append(" AND e.Category=@Cat"); p.Add("Cat", f.Category); }
            if (f.VendorId.HasValue) { sql.Append(" AND e.VendorId=@Vid"); p.Add("Vid", f.VendorId); }
            if (f.DateFrom.HasValue) { sql.Append(" AND e.ExpenseDate>=@Df"); p.Add("Df", f.DateFrom); }
            if (f.DateTo.HasValue) { sql.Append(" AND e.ExpenseDate<=@Dt"); p.Add("Dt", f.DateTo); }
            if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (e.ExpenseNumber LIKE @S OR e.Description LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
            sql.Append(" ORDER BY e.ExpenseDate DESC");
            if (f.PageSize > 0) { sql.Append(" OFFSET @Off ROWS FETCH NEXT @Ps ROWS ONLY"); p.Add("Off", (f.Page - 1) * f.PageSize); p.Add("Ps", f.PageSize); }
            return await conn.QueryAsync<Expense, Vendor, Expense>(
                sql.ToString(), (e, v) => { e.Vendor = v; return e; }, p, splitOn: "Id");
        }

        public async Task AddAsync(Expense e, CancellationToken ct = default)
        { await _db.Set<Expense>().AddAsync(e, ct); await _db.SaveChangesAsync(ct); }

        public async Task UpdateAsync(Expense e, CancellationToken ct = default)
        { _db.Set<Expense>().Update(e); await _db.SaveChangesAsync(ct); }

        public async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Expenses SET IsDeleted=1,DeletedAt=GETUTCDATE(),UpdatedBy=@By WHERE Id=@Id",
                new { Id = id, By = deletedBy });
        }

        public Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default)
            => AccSeqHelper.GetNextAsync(_dapper, companyId, "EXP");

        public async Task<decimal> GetTotalByCompanyAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Total),0) FROM dbo.Expenses WHERE CompanyId=@CId AND IsDeleted=0 AND ExpenseDate BETWEEN @F AND @T",
                new { CId = companyId, F = from, T = to });
        }

        public async Task MarkAsPaidAsync(Guid expenseId, Guid updatedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.Expenses SET Status=1, UpdatedAt=GETUTCDATE(), UpdatedBy=@By WHERE Id=@Id AND IsDeleted=0",
                new { Id = expenseId, By = updatedBy });
        }

        public async Task<IEnumerable<Expense>> GetUnpaidByCompanyAsync(Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT e.Id, e.ExpenseNumber, e.Category, e.Description, e.Total, e.ExpenseDate,e.VendorId,
               v.Id, v.Name
        FROM   dbo.Expenses e
        LEFT   JOIN dbo.Vendors v ON v.Id = e.VendorId
        WHERE  e.CompanyId = @CId AND e.IsDeleted = 0 AND e.Status = 0
        ORDER  BY e.ExpenseDate DESC
        """;
            return await conn.QueryAsync<Expense, Vendor, Expense>(
                sql, (exp, vendor) => { exp.Vendor = vendor; return exp; },
                new { CId = companyId }, splitOn: "Id");
        }


    }
}
