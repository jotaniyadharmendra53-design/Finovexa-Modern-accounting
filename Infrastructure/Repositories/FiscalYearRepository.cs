using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class FiscalYearRepository : IFiscalYearRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IDapperContext _dapper;

        public FiscalYearRepository(ApplicationDbContext db, IDapperContext dapper)
        {
            _db = db;
            _dapper = dapper;
        }

        // ── GetByIdAsync ──────────────────────────────────────────
        public async Task<FiscalYear?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var result = await conn.QueryFirstOrDefaultAsync<FiscalYear>(
                "SELECT * FROM dbo.FiscalYears WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id });
            return result;
        }

        // ── GetByCompanyAsync ─────────────────────────────────────
        public async Task<IEnumerable<FiscalYear>> GetByCompanyAsync(
            Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<FiscalYear>(
                """
            SELECT * FROM dbo.FiscalYears
            WHERE  CompanyId = @CId AND IsDeleted = 0
            ORDER  BY StartDate DESC
            """,
                new { CId = companyId });
        }

        // ── GetCurrentAsync — returns IsDefault = 1 Open FY ──────
        public async Task<FiscalYear?> GetCurrentAsync(
            Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<FiscalYear>(
                """
            SELECT TOP 1 * FROM dbo.FiscalYears
            WHERE  CompanyId = @CId
              AND  IsDeleted  = 0
              AND  IsDefault  = 1
              AND  Status     = 0
            """,
                new { CId = companyId });
        }

        // ── GetOpenAsync — first Open FY ──────────────────────────
        public async Task<FiscalYear?> GetOpenAsync(
            Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<FiscalYear>(
                """
            SELECT TOP 1 * FROM dbo.FiscalYears
            WHERE  CompanyId = @CId AND IsDeleted = 0 AND Status = 0
            ORDER  BY StartDate DESC
            """,
                new { CId = companyId });
        }

        // ── GetForDateAsync — which FY does this date fall in? ────
        public async Task<FiscalYear?> GetForDateAsync(
            Guid companyId, DateTime date, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<FiscalYear>(
                """
            SELECT TOP 1 * FROM dbo.FiscalYears
            WHERE  CompanyId = @CId
              AND  IsDeleted  = 0
              AND  @Date      >= StartDate
              AND  @Date      <= EndDate
            ORDER  BY StartDate DESC
            """,
                new { CId = companyId, Date = date.Date });
        }

        // ── HasOverlapAsync ───────────────────────────────────────
        public async Task<bool> HasOverlapAsync(
            Guid companyId, DateTime start, DateTime end,
            Guid? excludeId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var count = await conn.ExecuteScalarAsync<int>(
                """
            SELECT COUNT(1) FROM dbo.FiscalYears
            WHERE  CompanyId = @CId
              AND  IsDeleted  = 0
              AND  (@ExId IS NULL OR Id <> @ExId)
              AND  StartDate <= @End
              AND  EndDate   >= @Start
            """,
                new { CId = companyId, Start = start.Date, End = end.Date, ExId = excludeId });
            return count > 0;
        }

        // ── AddAsync ──────────────────────────────────────────────
        public async Task AddAsync(FiscalYear fy, CancellationToken ct = default)
        {
            await _db.Set<FiscalYear>().AddAsync(fy, ct);
            await _db.SaveChangesAsync(ct);
        }

        // ── UpdateAsync ───────────────────────────────────────────
        public async Task UpdateAsync(FiscalYear fy, CancellationToken ct = default)
        {
            _db.Set<FiscalYear>().Update(fy);
            await _db.SaveChangesAsync(ct);
        }

        // ── ClearDefaultAsync — removes IsDefault flag from all FYs ──
        public async Task ClearDefaultAsync(Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "UPDATE dbo.FiscalYears SET IsDefault = 0 WHERE CompanyId = @CId",
                new { CId = companyId });
        }
    }
}
