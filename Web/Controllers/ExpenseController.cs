using Dapper;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class ExpenseController : BaseController
    {
        private readonly IExpenseService _service;
        private readonly IVendorService _vendorService;
        private readonly IDapperContext _dapper;
        public ExpenseController(IExpenseService service, IVendorService vendorService, ICurrentUserService cu, IDapperContext dapper) : base(cu)
        { _service = service; _vendorService = vendorService; _dapper = dapper; }


        // ── Count extensions for Expense ─────────────────────────
        public static class ExpenseCountExtension
        {
            public static async Task<int> CountByCompanyAsync(
                IDapperContext dapper, Guid companyId, Domain.Interfaces.ExpenseFilterDto f)
            {
                using var conn = dapper.CreateConnection();
                var sql = new System.Text.StringBuilder("""
            SELECT COUNT(*) FROM dbo.Expenses e
            WHERE  e.CompanyId=@CId AND e.IsDeleted=0
            """);
                var p = new DynamicParameters(); p.Add("CId", companyId);
                if (!string.IsNullOrEmpty(f.Category)) { sql.Append(" AND e.Category=@Cat"); p.Add("Cat", f.Category); }
                if (f.VendorId.HasValue) { sql.Append(" AND e.VendorId=@Vid"); p.Add("Vid", f.VendorId); }
                if (f.DateFrom.HasValue) { sql.Append(" AND e.ExpenseDate>=@Df"); p.Add("Df", f.DateFrom); }
                if (f.DateTo.HasValue) { sql.Append(" AND e.ExpenseDate<=@Dt"); p.Add("Dt", f.DateTo); }
                if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (e.ExpenseNumber LIKE @S OR e.Description LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
                return await conn.ExecuteScalarAsync<int>(sql.ToString(), p);
            }
        }



        //[HttpGet]
        //[HasPermission("expenses.view")]
        //public async Task<IActionResult> Index([FromQuery] ExpenseFilterDto filter)
        //{
        //    ViewBag.ActiveMenu = "expenses"; ViewData["Title"] = "Expenses";
        //    if (!CurrentUser.CompanyId.HasValue) 
        //        return View(Enumerable.Empty<ExpenseDto>());
        //    var vendors = await _vendorService.GetSelectListAsync(CurrentUser.CompanyId.Value);
        //    ViewBag.Vendors = vendors.Data ?? Enumerable.Empty<Application.DTOs.Common.SelectItemDto>();
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<ExpenseDto>());
        //}


        [HttpGet]
        [HasPermission("expenses.view")]
        public async Task<IActionResult> Index([FromQuery] ExpenseFilterDto filter)
        {
            ViewBag.ActiveMenu = "expenses"; ViewData["Title"] = "Expenses";
            if (!CurrentUser.CompanyId.HasValue) return View(Enumerable.Empty<ExpenseDto>());

            var vendors = await _vendorService.GetSelectListAsync(CurrentUser.CompanyId.Value);
            ViewBag.Vendors = vendors.Data ?? Enumerable.Empty<SelectItemDto>();

            // Domain filter for count
            var domainFilter = new Domain.Interfaces.ExpenseFilterDto
            {
                Category = filter.Category,
                VendorId = filter.VendorId,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                Search = filter.Search,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
            var totalCount = await ExpenseCountExtension.CountByCompanyAsync(
                _dapper, CurrentUser.CompanyId.Value, domainFilter);
            ViewBag.Page = filter.Page; ViewBag.PageSize = filter.PageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = filter.PageSize > 0 ? (int)Math.Ceiling((double)totalCount / filter.PageSize) : 0;

            var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
            return View(r.Succeeded ? r.Data : Enumerable.Empty<ExpenseDto>());
        }


        [HttpGet]
        [HasPermission("expenses.view")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var r = await _service.GetByIdAsync(id);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("expenses.create")]
        public async Task<IActionResult> Save([FromBody] SaveExpenseDto dto)
        {
            if (!CurrentUser.CompanyId.HasValue) 
                return AjaxFail("Company context not found.");
            var r = await _service.SaveAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        [HttpGet]
        [HasPermission("expenses.view")]
        public async Task<IActionResult> GetUnpaid()
        {
            if (!CurrentUser.CompanyId.HasValue) 
                return AjaxOk(Array.Empty<object>());
            var r = await _service.GetUnpaidByCompanyAsync(CurrentUser.CompanyId.Value);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }


        [HttpPost]
        [HasPermission("expenses.delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var r = await _service.DeleteAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }
    }
}
