using Dapper;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Enums;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    public class InvoiceRepository : BaseRepository<Invoice>, IInvoiceRepository
    {
        public InvoiceRepository(ApplicationDbContext db, IDapperContext dapper) : base(db, dapper) { }

        // ── GetByIdAsync ──────────────────────────────────────────────
        // Single implementation — no duplicate.
        // QueryAsync<T1,T2,T3,TReturn> multi-map is valid on IDbConnection.
        public override async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT i.*, c.Id, c.Name, c.Email, c.Phone, c.Address,
                   co.Id, co.Name, co.CurrencyCode
            FROM   dbo.Invoices i
            INNER  JOIN dbo.Clients   c  ON c.Id  = i.ClientId
            INNER  JOIN dbo.Companies co ON co.Id = i.CompanyId
            WHERE  i.Id = @Id AND i.IsDeleted = 0
            """;

            var result = await conn.QueryAsync<Invoice, Client, Company, Invoice>(
                sql,
                (inv, client, company) => { inv.Client = client; inv.Company = company; return inv; },
                new { Id = id },
                splitOn: "Id,Id");

            return result.FirstOrDefault();
        }

        // ── GetWithItemsAsync ─────────────────────────────────────────
        // FIX for CS0305: GridReader.ReadAsync<T1,T2,T3,TReturn> does NOT exist.
        // GridReader only has single-type ReadAsync<T>().
        // Solution: two separate QueryAsync calls on the same IDbConnection.
        //   • Query 1 → invoice + client + company  (multi-map on IDbConnection ✓)
        //   • Query 2 → invoice items               (plain single-type QueryAsync<T> ✓)
        public async Task<Invoice?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();

            // Query 1: invoice header with joins
            const string invSql = """
            SELECT i.*, c.Id, c.Name, c.Email, c.Phone,
                   co.Id, co.Name, co.CurrencyCode, co.Email, co.Phone,
                   co.Address, co.TaxNumber, co.Logo
            FROM   dbo.Invoices i
            INNER  JOIN dbo.Clients   c  ON c.Id  = i.ClientId
            INNER  JOIN dbo.Companies co ON co.Id = i.CompanyId
            WHERE  i.Id = @Id AND i.IsDeleted = 0
            """;

            var invoices = await conn.QueryAsync<Invoice, Client, Company, Invoice>(
                invSql,
                (inv, client, company) =>
                {
                    inv.Client = client;
                    inv.Company = company;
                    return inv;
                },
                new { Id = id },
                splitOn: "Id,Id");

            var invoice = invoices.FirstOrDefault();
            if (invoice is null) return null;

            // Query 2: line items — single-type, always supported
            var items = await conn.QueryAsync<InvoiceItem>(
                "SELECT * FROM dbo.InvoiceItems WHERE InvoiceId = @Id ORDER BY SortOrder",
                new { Id = id });

            invoice.InvoiceItems = items.ToList();
            return invoice;
        }

        // ── UpdatePaidAmountAsync (updates PaidAmount + Status + PaidAt) ───────────────

        public async Task UpdatePaidAmountAsync(
                Guid id,
                decimal paidAmount,
                InvoiceStatus status,
                Guid updatedBy,
                CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
        UPDATE dbo.Invoices
        SET    PaidAmount = @PaidAmount,
               Status     = @Status,
               PaidAt     = CASE WHEN @Status = 2 THEN GETUTCDATE() ELSE NULL END,
               UpdatedAt  = GETUTCDATE(),
               UpdatedBy  = @UpdatedBy
        WHERE  Id = @Id AND IsDeleted = 0
        """,
                new { Id = id, PaidAmount = paidAmount, Status = (byte)status, UpdatedBy = updatedBy });
        }


        // ── GetAllAsync ───────────────────────────────────────────────
        public override async Task<IEnumerable<Invoice>> GetAllAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<Invoice>(
                "SELECT * FROM dbo.Invoices WHERE IsDeleted = 0 ORDER BY CreatedAt DESC");
        }

        // ── GetByCompanyAsync (dynamic filters + pagination) ──────────
        public async Task<IEnumerable<Invoice>> GetByCompanyAsync(
            Guid companyId,
            InvoiceFilterDto filter,
            CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();

            var sql = new StringBuilder("""
            SELECT i.*, cl.Id, cl.Name AS Name, cl.Email,
                   co.Id, co.Name AS Name, co.CurrencyCode
            FROM   dbo.Invoices i
            INNER  JOIN dbo.Clients   cl ON cl.Id = i.ClientId
            INNER  JOIN dbo.Companies co ON co.Id = i.CompanyId
            WHERE  i.CompanyId = @CompanyId AND i.IsDeleted = 0
            """);

            var p = new DynamicParameters();
            p.Add("CompanyId", companyId);

            if (filter.Status.HasValue)
            {
                sql.Append(" AND i.Status = @Status");
                p.Add("Status", (byte)filter.Status.Value);
            }
            if (filter.ClientId.HasValue)
            {
                sql.Append(" AND i.ClientId = @ClientId");
                p.Add("ClientId", filter.ClientId.Value);
            }
            if (filter.DateFrom.HasValue)
            {
                sql.Append(" AND i.IssueDate >= @DateFrom");
                p.Add("DateFrom", filter.DateFrom.Value);
            }
            if (filter.DateTo.HasValue)
            {
                sql.Append(" AND i.IssueDate <= @DateTo");
                p.Add("DateTo", filter.DateTo.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                sql.Append(" AND (i.InvoiceNumber LIKE @Search OR cl.Name LIKE @Search)");
                p.Add("Search", $"%{filter.Search}%");
            }

            // Whitelist sort columns — prevents SQL injection
            var allowed = new HashSet<string>
            { "CreatedAt", "IssueDate", "DueDate", "Total", "Status", "InvoiceNumber" };
            var sortCol = allowed.Contains(filter.SortBy) ? filter.SortBy : "CreatedAt";
            var sortDir = filter.SortDesc ? "DESC" : "ASC";
            sql.Append($" ORDER BY i.{sortCol} {sortDir}");

            if (filter.PageSize > 0)
            {
                sql.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
                p.Add("Offset", (filter.Page - 1) * filter.PageSize);
                p.Add("PageSize", filter.PageSize);
            }

            return await conn.QueryAsync<Invoice, Client, Company, Invoice>(
                sql.ToString(),
                (inv, client, company) => { inv.Client = client; inv.Company = company; return inv; },
                p,
                splitOn: "Id,Id");
        }

        // ── GetByClientAsync ──────────────────────────────────────────
        public async Task<IEnumerable<Invoice>> GetByClientAsync(Guid clientId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT i.*, cl.Id, cl.Name, cl.Email,
                   co.Id, co.Name, co.CurrencyCode
            FROM   dbo.Invoices i
            INNER  JOIN dbo.Clients   cl ON cl.Id = i.ClientId
            INNER  JOIN dbo.Companies co ON co.Id = i.CompanyId
            WHERE  i.ClientId = @ClientId AND i.IsDeleted = 0
            ORDER  BY i.CreatedAt DESC
            """;

            return await conn.QueryAsync<Invoice, Client, Company, Invoice>(
                sql,
                (inv, client, company) => { inv.Client = client; inv.Company = company; return inv; },
                new { ClientId = clientId },
                splitOn: "Id,Id");
        }

        // ── GetNextInvoiceNumberAsync (stored procedure OUTPUT param) ─
        public async Task<string> GetNextInvoiceNumberAsync(Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            var p = new DynamicParameters();
            p.Add("CompanyId", companyId);
            p.Add("InvoiceNumber", dbType: DbType.String, size: 30,
                  direction: ParameterDirection.Output);

            await conn.ExecuteAsync(
                "dbo.sp_GetNextInvoiceNumber",
                p,
                commandType: CommandType.StoredProcedure);

            return p.Get<string>("InvoiceNumber");
        }
        //ISNULL(SUM(CASE WHEN Status = 2 THEN Total ELSE 0 END), 0)                    AS TotalRevenue,
        // ── GetStatsAsync ─────────────────────────────────────────────
        public async Task<InvoiceSummaryStats> GetStatsAsync(Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT
                COUNT(*)                                                                       AS TotalInvoices,
                SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END)                                   AS DraftCount,
                SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END)                                   AS SentCount,
                SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)                                   AS PaidCount,
                SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)                                   AS OverdueCount,
                ISNULL(SUM(CASE WHEN Status = 2 THEN BaseAmount ELSE 0 END), 0)                    AS TotalRevenue,
                ISNULL(SUM(CASE WHEN Status IN (1,3) THEN (Total - PaidAmount) ELSE 0 END),0) AS PendingAmount,
                ISNULL(SUM(CASE WHEN Status = 3    THEN (Total - PaidAmount) ELSE 0 END),0)   AS OverdueAmount,
                ISNULL(SUM(CASE WHEN Status = 2
                                AND MONTH(PaidAt) = MONTH(GETUTCDATE())
                                AND YEAR(PaidAt)  = YEAR(GETUTCDATE())
                           THEN Total ELSE 0 END), 0)                                          AS ThisMonthTotal
            FROM dbo.Invoices
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
            """;

            return await conn.QueryFirstOrDefaultAsync<InvoiceSummaryStats>(sql, new { CompanyId = companyId })
                   ?? new InvoiceSummaryStats();
        }
        //SELECT i.*, cl.Id, cl.Name, cl.Email,
        //         co.Id, co.Name, co.CurrencyCode

        // ── GetOverdueAsync ───────────────────────────────────────────
        public async Task<IEnumerable<Invoice>> GetOverdueAsync(CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
            SELECT i.Id, i.CompanyId, i.ClientId, ...,
            c.Id, c.Name, c.Email,
            co.Id, co.Name, co.CurrencyCode
            FROM   dbo.Invoices i
            INNER  JOIN dbo.Clients   cl ON cl.Id = i.ClientId
            INNER  JOIN dbo.Companies co ON co.Id = i.CompanyId
            WHERE  i.Status = 1
            AND    i.DueDate < CAST(GETUTCDATE() AS DATE)
            AND    i.IsDeleted = 0
            """;

            return await conn.QueryAsync<Invoice, Client, Company, Invoice>(
                sql,
                (inv, client, company) => { inv.Client = client; inv.Company = company; return inv; },
                splitOn: "Id,Id");
        }

        // ── UpdateStatusAsync ─────────────────────────────────────────
        public async Task UpdateStatusAsync(
            Guid id,
            InvoiceStatus status,
            Guid updatedBy,
            CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();

            var extraSet = status == InvoiceStatus.Sent
                ? ", SentAt = GETUTCDATE()"
                : status == InvoiceStatus.Paid
                    ? ", PaidAt = GETUTCDATE()"
                    : string.Empty;

            await conn.ExecuteAsync($"""
            UPDATE dbo.Invoices
            SET    Status    = @Status,
                   UpdatedAt = GETUTCDATE(),
                   UpdatedBy = @UpdatedBy
                   {extraSet}
            WHERE  Id = @Id AND IsDeleted = 0
            """,
                new { Id = id, Status = (byte)status, UpdatedBy = updatedBy });
        }

        // ── AddItemsAsync (EF Core bulk insert) ───────────────────────
        public async Task AddItemsAsync(IEnumerable<InvoiceItem> items, CancellationToken ct = default)
        {
            await _db.InvoiceItems.AddRangeAsync(items, ct);
            await _db.SaveChangesAsync(ct);
        }

        // ── DeleteItemsByInvoiceAsync (Dapper — faster than EF) ───────
        public async Task DeleteItemsByInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.InvoiceItems WHERE InvoiceId = @InvoiceId",
                new { InvoiceId = invoiceId });
        }

        public async Task UpdateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
        UPDATE dbo.Invoices
        SET    ClientId  = @ClientId,
               IssueDate = @IssueDate,
               DueDate   = @DueDate,
               TaxRate   = @TaxRate,
               TaxAmount = @TaxAmount,
               SubTotal  = @SubTotal,
               Discount  = @Discount,
               Total     = @Total,
               Notes     = @Notes,
               Terms     = @Terms,
               LastEditRemark = @LastEditRemark,
               UpdatedAt = GETUTCDATE(),
               UpdatedBy = @UpdatedBy
        WHERE  Id = @Id AND IsDeleted = 0
        """,
                new
                {
                    invoice.Id,
                    invoice.ClientId,
                    invoice.IssueDate,
                    invoice.DueDate,
                    invoice.TaxRate,
                    invoice.TaxAmount,
                    invoice.SubTotal,
                    invoice.Discount,
                    invoice.Total,
                    invoice.Notes,
                    invoice.Terms,
                    invoice.LastEditRemark,
                    invoice.UpdatedBy
                });
        }


        public async Task<IEnumerable<CurrencyRevenueRow>> GetCurrencyStatsAsync(Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
                SELECT
                     CurrencyCode,
                     COUNT(*)                                                            AS InvoiceCount,
                     ISNULL(SUM(CASE WHEN Status = 2 THEN Total      ELSE 0 END), 0)   AS Revenue,
                     ISNULL(SUM(CASE WHEN Status = 2 THEN BaseAmount ELSE 0 END), 0)   AS BaseRevenue,
                     ISNULL(SUM(CASE WHEN Status IN (1,3) THEN (Total - PaidAmount)
                     ELSE 0 END), 0)                                    AS Pending
                FROM   dbo.Invoices
                WHERE  CompanyId = @CompanyId AND IsDeleted = 0
                GROUP  BY CurrencyCode
                ORDER  BY Revenue DESC
                """;
            return await conn.QueryAsync<CurrencyRevenueRow>(sql, new { CompanyId = companyId });
        }

        public async Task<IEnumerable<MonthlyRevenueRow>> GetMonthlyRevenueAsync(
    Guid companyId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            const string sql = """
        SELECT
            FORMAT(IssueDate, 'MMM yy')  AS Label,
            YEAR(IssueDate)              AS Year,
            MONTH(IssueDate)             AS Month,
            ISNULL(SUM(BaseAmount), 0)   AS Amount
        FROM  dbo.Invoices
        WHERE CompanyId = @CompanyId
          AND IsDeleted = 0
          AND Status    = 2
          AND IssueDate >= DATEADD(MONTH, -11,
                DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1))
        GROUP BY
            FORMAT(IssueDate, 'MMM yy'),
            YEAR(IssueDate),
            MONTH(IssueDate)
        ORDER BY Year, Month
        """;

            return await conn.QueryAsync<MonthlyRevenueRow>(sql, new { CompanyId = companyId });
        }

        public async Task AddEditHistoryAsync(InvoiceEditHistory entry, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
        INSERT INTO dbo.InvoiceEditHistory (Id, InvoiceId, EditedBy, EditedAt, Remark, FromStatus)
        VALUES (@Id, @InvoiceId, @EditedBy, @EditedAt, @Remark, @FromStatus)
        """, entry);
        }

        public async Task<IEnumerable<InvoiceEditHistory>> GetEditHistoryAsync(
            Guid invoiceId, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            return await conn.QueryAsync<InvoiceEditHistory>("""
        SELECT * FROM dbo.InvoiceEditHistory
        WHERE InvoiceId = @InvoiceId
        ORDER BY EditedAt DESC
        """, new { InvoiceId = invoiceId });
        }

        public async Task WriteOffAsync(Guid id, Guid updatedBy, CancellationToken ct = default)
        {
            using var conn = _dapper.CreateConnection();
            await conn.ExecuteAsync("""
        UPDATE dbo.Invoices
        SET  PaidAmount     = Total,
             Status         = 2,
             PaidAt         = GETUTCDATE(),
             LastEditRemark = 'Balance written off (rounding adjustment)',
             UpdatedAt      = GETUTCDATE(),
             UpdatedBy      = @UpdatedBy
        WHERE Id = @Id AND IsDeleted = 0
        """, new { Id = id, UpdatedBy = updatedBy });
        }


    }
}