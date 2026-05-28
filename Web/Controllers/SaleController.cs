using Dapper;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class SaleController : BaseController
    {
        private readonly ISaleService _service;
        private readonly IClientService _clientService;
        private readonly IProductService _productService;
        private readonly IDapperContext _dapper;
        public SaleController(ISaleService service, IClientService clientService,
            IProductService productService, ICurrentUserService cu, IDapperContext dapper   ) : base(cu)
        {
            _service = service; _clientService = clientService; _productService = productService;
            _dapper = dapper;
        }



        // ── Count extensions for Sale ─────────────────────────────
        public static class SaleCountExtension
        {
            public static async Task<int> CountByCompanyAsync(
                IDapperContext dapper, Guid companyId, Domain.Interfaces.SaleFilterDto f)
            {
                using var conn = dapper.CreateConnection();
                var sql = new System.Text.StringBuilder("""
            SELECT COUNT(*) FROM dbo.Sales s
            LEFT JOIN dbo.Clients c ON c.Id=s.ClientId
            WHERE s.CompanyId=@CId AND s.IsDeleted=0
            """);
                var p = new DynamicParameters(); p.Add("CId", companyId);
                if (f.Status.HasValue) { sql.Append(" AND s.Status=@St"); p.Add("St", (byte)f.Status.Value); }
                if (f.ClientId.HasValue) { sql.Append(" AND s.ClientId=@Cl"); p.Add("Cl", f.ClientId); }
                if (f.DateFrom.HasValue) { sql.Append(" AND s.SaleDate>=@Df"); p.Add("Df", f.DateFrom); }
                if (f.DateTo.HasValue) { sql.Append(" AND s.SaleDate<=@Dt"); p.Add("Dt", f.DateTo); }
                if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (s.SaleNumber LIKE @S OR c.Name LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
                return await conn.ExecuteScalarAsync<int>(sql.ToString(), p);
            }
        }


        //[HttpGet]
        //[HasPermission("sales.view")]
        //public async Task<IActionResult> Index([FromQuery] SaleFilterDto filter)
        //{
        //    ViewBag.ActiveMenu = "sales"; ViewData["Title"] = "Sales";
        //    if (!CurrentUser.CompanyId.HasValue) return View(Enumerable.Empty<SaleDto>());
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<SaleDto>());
        //}

        [HttpGet]
        [HasPermission("sales.view")]
        public async Task<IActionResult> Index([FromQuery] SaleFilterDto filter)
        {
            ViewBag.ActiveMenu = "sales";
            ViewData["Title"] = "Sales";

            if (!CurrentUser.CompanyId.HasValue)
                return View(Enumerable.Empty<SaleDto>());

            var domainFilter = new Domain.Interfaces.SaleFilterDto
            {
                Search = filter.Search,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            var totalCount = await SaleCountExtension.CountByCompanyAsync(
                _dapper, CurrentUser.CompanyId.Value, domainFilter);

            ViewBag.Page = filter.Page;
            ViewBag.PageSize = filter.PageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = filter.PageSize > 0
                ? (int)Math.Ceiling((double)totalCount / filter.PageSize)
                : 0;

            var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);

            return View(r.Succeeded ? r.Data : Enumerable.Empty<SaleDto>());
        }


        [HttpGet]
        [HasPermission("sales.view")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var r = await _service.GetByIdAsync(id);
            if (!r.Succeeded) { SetErrorToast("Sale not found."); return RedirectToAction("Index"); }
            ViewBag.ActiveMenu = "sales"; ViewData["Title"] = r.Data!.SaleNumber;
            return View(r.Data);
        }

        [HttpGet]
        [HasPermission("sales.create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.ActiveMenu = "sales"; ViewData["Title"] = "New Sale";
            await LoadDropdowns();
            return View("CreateEdit", new SaveSaleDto
            {
                SaleDate = DateTime.Today
            });
        }

        [HttpPost]
        [HasPermission("sales.create")]
        public async Task<IActionResult> Save([FromBody] SaveSaleDto dto)
        {
            if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company context not found.");
            var r = await _service.SaveAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(new { id = r.Data!.Id }, r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("sales.refund")]
        public async Task<IActionResult> Refund(Guid id)
        {
            var r = await _service.RefundAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("sales.delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var r = await _service.DeleteAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }

        private async Task LoadDropdowns()
        {
            if (!CurrentUser.CompanyId.HasValue) return;
            var clients = await _clientService.GetSelectListAsync(CurrentUser.CompanyId.Value);
            var products = await _productService.GetSelectListAsync(CurrentUser.CompanyId.Value);
            ViewBag.Clients = clients.Data ?? Enumerable.Empty<Application.DTOs.Common.SelectItemDto>();
            ViewBag.Products = products.Data ?? Enumerable.Empty<Application.DTOs.Common.SelectItemDto>();
        }
    }
}
