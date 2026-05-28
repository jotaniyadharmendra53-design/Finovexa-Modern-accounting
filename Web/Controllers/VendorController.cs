using Dapper;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class VendorController : BaseController
    {
        private readonly IVendorService _service;
        private readonly IDapperContext _dapper;
        public VendorController(IVendorService service, ICurrentUserService cu, IDapperContext dapper) : base(cu)
        { _service = service; _dapper = dapper; }


        public static class VendorCountExtension
        {
            public static async Task<int> CountByCompanyAsync(
                IDapperContext dapper, Guid companyId, string? search = null)
            {
                using var conn = dapper.CreateConnection();
                return await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM dbo.Vendors
            WHERE  CompanyId=@CId AND IsDeleted=0
            AND    (@S IS NULL OR Name LIKE '%'+@S+'%' OR Email LIKE '%'+@S+'%')
            """, new { CId = companyId, S = search });
            }
        }


        //[HttpGet]
        //[HasPermission("vendors.view")]
        //public async Task<IActionResult> Index()
        //{
        //    ViewBag.ActiveMenu = "vendors"; ViewData["Title"] = "Vendors";
        //    if (!CurrentUser.CompanyId.HasValue)
        //        return View(Enumerable.Empty<VendorDto>());
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<VendorDto>());
        //}

        [HttpGet]
        [HasPermission("vendors.view")]
        public async Task<IActionResult> Index(string? search = null, int page = 1, int pageSize = 20)
        {
            ViewBag.ActiveMenu = "vendors"; ViewData["Title"] = "Vendors";
            if (!CurrentUser.CompanyId.HasValue) return View(Enumerable.Empty<VendorDto>());

            var totalCount = await VendorCountExtension.CountByCompanyAsync(
                _dapper, CurrentUser.CompanyId.Value, search);
            ViewBag.Page = page; ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

            var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, search, page, pageSize);
            return View(r.Succeeded ? r.Data : Enumerable.Empty<VendorDto>());
        }


        [HttpGet]
        [HasPermission("vendors.view")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var r = await _service.GetByIdAsync(id);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("vendors.create")]
        public async Task<IActionResult> Save([FromBody] SaveVendorDto dto)
        {
            if (!CurrentUser.CompanyId.HasValue) 
                return AjaxFail("Company context not found.");
            var r = await _service.SaveAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("vendors.delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var r = await _service.DeleteAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }

        [HttpGet]
        public async Task<IActionResult> SelectList()
        {
            if (!CurrentUser.CompanyId.HasValue)
                return AjaxOk(Array.Empty<object>());

            var r = await _service.GetSelectListAsync(CurrentUser.CompanyId.Value);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }
    }
}
