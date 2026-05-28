using Dapper;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class PaymentController : BaseController
    {
        private readonly IPaymentService _service;
        private readonly IDapperContext _dapper;
        public PaymentController(IPaymentService service, ICurrentUserService cu, IDapperContext dapper ) : base(cu)
        { _service = service; _dapper = dapper; }


        // ── Count extensions for Payment ─────────────────────────
        public static class PaymentCountExtension
        {
            public static async Task<int> CountByCompanyAsync(
                IDapperContext dapper, Guid companyId, Domain.Interfaces.PaymentFilterDto f)
            {
                using var conn = dapper.CreateConnection();
                var sql = new System.Text.StringBuilder("""
            SELECT COUNT(*) FROM dbo.Payments p
            WHERE  p.CompanyId=@CId AND p.IsDeleted=0
            """);
                var par = new DynamicParameters(); par.Add("CId", companyId);
                if (f.Direction.HasValue) { sql.Append(" AND p.Direction=@Dir"); par.Add("Dir", (byte)f.Direction.Value); }
                if (f.DateFrom.HasValue) { sql.Append(" AND p.PaymentDate>=@Df"); par.Add("Df", f.DateFrom); }
                if (f.DateTo.HasValue) { sql.Append(" AND p.PaymentDate<=@Dt"); par.Add("Dt", f.DateTo); }
                if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND p.PaymentNumber LIKE @S"); par.Add("S", $"%{f.Search}%"); }
                return await conn.ExecuteScalarAsync<int>(sql.ToString(), par);
            }
        }


        //[HttpGet]
        //[HasPermission("payments.view")]
        //public async Task<IActionResult> Index([FromQuery] PaymentFilterDto filter)
        //{
        //    ViewBag.ActiveMenu = "payments"; ViewData["Title"] = "Payments";
        //    if (!CurrentUser.CompanyId.HasValue) 
        //        return View(Enumerable.Empty<PaymentDto>());
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<PaymentDto>());
        //}

        [HttpGet]
        [HasPermission("payments.view")]
        public async Task<IActionResult> Index([FromQuery] PaymentFilterDto filter)
        {
            ViewBag.ActiveMenu = "payments";
            ViewData["Title"] = "Payments";

            if (!CurrentUser.CompanyId.HasValue)
                return View(Enumerable.Empty<PaymentDto>());

            var domainFilter = new Domain.Interfaces.PaymentFilterDto
            {
                Search = filter.Search,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            var totalCount = await PaymentCountExtension.CountByCompanyAsync(
                _dapper, CurrentUser.CompanyId.Value, domainFilter);

            ViewBag.Page = filter.Page;
            ViewBag.PageSize = filter.PageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = filter.PageSize > 0
                ? (int)Math.Ceiling((double)totalCount / filter.PageSize)
                : 0;

            var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);

            return View(r.Succeeded ? r.Data : Enumerable.Empty<PaymentDto>());
        }



        [HttpPost]
        [HasPermission("payments.create")]
        public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
        {
            if (!CurrentUser.CompanyId.HasValue) 
                return AjaxFail("Company context not found.");
            var r = await _service.CreateAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("payments.delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var r = await _service.DeleteAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }


        /// <summary>
        /// Returns Sent + PartiallyPaid invoices for a client (for the inbound payment dropdown).
        /// GET /Payment/GetUnpaidInvoicesByClient?clientId=...
        /// </summary>
        [HttpGet]
        [HasPermission("payments.create")]
        public async Task<IActionResult> GetUnpaidInvoicesByClient(Guid clientId)
        {
            if (!CurrentUser.CompanyId.HasValue)
                return AjaxFail("Company context not found.");

            using var conn = _dapper.CreateConnection();

            // Status values: Sent = 1, PartiallyPaid = 5
            const string sql = """
        SELECT
            i.Id,
            i.InvoiceNumber,
            i.Total,
            i.PaidAmount,
            (i.Total - i.PaidAmount) AS BalanceDue,
            i.DueDate
        FROM dbo.Invoices i
        WHERE i.CompanyId = @CId
          AND i.ClientId  = @ClientId
          AND i.IsDeleted = 0
          AND i.Status IN (1, 5)
        ORDER BY i.DueDate ASC
        """;

            var rows = await conn.QueryAsync(sql, new
            {
                CId = CurrentUser.CompanyId.Value,
                ClientId = clientId
            });

            var data = rows.Select(r => new
            {
                id = (Guid)r.Id,
                invoiceNumber = (string)r.InvoiceNumber,
                total = (decimal)r.Total,
                paidAmount = (decimal)r.PaidAmount,
                balanceDue = (decimal)r.BalanceDue,
                dueDate = ((DateTime)r.DueDate).ToString("dd MMM yyyy")
            });

            return Json(new { success = true, data });
        }


    }
}
