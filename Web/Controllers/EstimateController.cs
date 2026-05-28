using Dapper;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class EstimateController : BaseController
    {
        private readonly IEstimateService _service;
        private readonly IClientService _clientService;
        private readonly IProductService _productService;
        private readonly IDapperContext _dapper;

        public EstimateController(IEstimateService service, IClientService clientService,
            IProductService productService, ICurrentUserService cu, IDapperContext dapper) : base(cu)
        { _service = service; _clientService = clientService; _productService = productService; _dapper = dapper; }


        // ── Count extensions for Estimate ────────────────────────
        public static class EstimateCountExtension
        {
            public static async Task<int> CountByCompanyAsync(
                IDapperContext dapper, Guid companyId, Domain.Interfaces.EstimateFilterDto f)
            {
                using var conn = dapper.CreateConnection();
                var sql = new System.Text.StringBuilder("""
            SELECT COUNT(*) FROM dbo.Estimates e
            INNER JOIN dbo.Clients c ON c.Id=e.ClientId
            WHERE e.CompanyId=@CId AND e.IsDeleted=0
            """);
                var p = new DynamicParameters(); p.Add("CId", companyId);
                if (f.Status.HasValue) { sql.Append(" AND e.Status=@St"); p.Add("St", (byte)f.Status.Value); }
                if (f.ClientId.HasValue) { sql.Append(" AND e.ClientId=@Cl"); p.Add("Cl", f.ClientId); }
                if (f.DateFrom.HasValue) { sql.Append(" AND e.IssueDate>=@Df"); p.Add("Df", f.DateFrom); }
                if (f.DateTo.HasValue) { sql.Append(" AND e.IssueDate<=@Dt"); p.Add("Dt", f.DateTo); }
                if (!string.IsNullOrEmpty(f.Search)) { sql.Append(" AND (e.EstimateNumber LIKE @S OR c.Name LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
                return await conn.ExecuteScalarAsync<int>(sql.ToString(), p);
            }
        }


        //[HttpGet]
        //[HasPermission("estimates.view")]
        //public async Task<IActionResult> Index([FromQuery] EstimateFilterDto filter)
        //{
        //    ViewBag.ActiveMenu = "estimates"; ViewData["Title"] = "Estimates";
        //    if (!CurrentUser.CompanyId.HasValue) 
        //        return View(Enumerable.Empty<EstimateDto>());
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<EstimateDto>());
        //}


        //[HttpGet]
        //[HasPermission("estimates.view")]
        //public async Task<IActionResult> Index([FromQuery] EstimateFilterDto filter)
        //{
        //    ViewBag.ActiveMenu = "estimates"; ViewData["Title"] = "Estimates";
        //    if (!CurrentUser.CompanyId.HasValue)
        //        return View(Enumerable.Empty<EstimateDto>());
        //    var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        //    return View(r.Succeeded ? r.Data : Enumerable.Empty<EstimateDto>());
        //}


        [HttpGet]
        [HasPermission("estimates.view")]
        public async Task<IActionResult> Index([FromQuery] EstimateFilterDto filter)
        {
            ViewBag.ActiveMenu = "estimates";
            ViewData["Title"] = "Estimates";

            if (!CurrentUser.CompanyId.HasValue)
                return View(Enumerable.Empty<EstimateDto>());

            // Domain filter
            var domainFilter = new Domain.Interfaces.EstimateFilterDto
            {
                Search = filter.Search,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            // Total count
            var totalCount = await EstimateCountExtension.CountByCompanyAsync(
                _dapper, CurrentUser.CompanyId.Value, domainFilter);

            ViewBag.Page = filter.Page;
            ViewBag.PageSize = filter.PageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = filter.PageSize > 0
                ? (int)Math.Ceiling((double)totalCount / filter.PageSize)
                : 0;

            var r = await _service.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);

            return View(r.Succeeded ? r.Data : Enumerable.Empty<EstimateDto>());
        }



        [HttpGet]
        [HasPermission("estimates.view")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var r = await _service.GetByIdAsync(id);
            if (!r.Succeeded) { SetErrorToast("Estimate not found."); 
                return RedirectToAction("Index"); }
            ViewBag.ActiveMenu = "estimates";
            ViewData["Title"] = r.Data!.EstimateNumber;
            return View(r.Data);
        }

        [HttpGet]
        [HasPermission("estimates.create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.ActiveMenu = "estimates"; ViewData["Title"] = "New Estimate";
            await LoadDropdowns();
            return View("CreateEdit", new SaveEstimateDto
            {
                IssueDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddDays(30)
            });
        }

        [HttpGet]
        [HasPermission("estimates.edit")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var r = await _service.GetByIdAsync(id);
            if (!r.Succeeded) { SetErrorToast("Estimate not found.");
                return RedirectToAction("Index"); }
            if (r.Data!.Status != (int)Domain.Entities.EstimateStatus.Draft)
            { SetErrorToast("Only Draft estimates can be edited.");
                return RedirectToAction("Detail", new { id }); }
            ViewBag.ActiveMenu = "estimates";
            ViewData["Title"] = $"Edit {r.Data.EstimateNumber}";
            ViewBag.ExistingEstimate = r.Data;
            await LoadDropdowns();
            return View("CreateEdit", new SaveEstimateDto
            {
                Id = id,
                ClientId = r.Data.ClientId,
                IssueDate = r.Data.IssueDate,
                ExpiryDate = r.Data.ExpiryDate,
                TaxRate = r.Data.TaxRate,
                Discount = r.Data.Discount,
                Notes = r.Data.Notes,
                Terms = r.Data.Terms,
                Items = r.Data.Items
            });
        }

        [HttpPost]
        [HasPermission("estimates.create")]
        public async Task<IActionResult> Save([FromBody] SaveEstimateDto dto)
        {
            if (!CurrentUser.CompanyId.HasValue) 
                return AjaxFail("Company context not found.");
            var r = await _service.SaveAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(new { id = r.Data!.Id }, r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("estimates.send")]
        public async Task<IActionResult> Send(Guid id)
        {
            var r = await _service.SendAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("estimates.convert")]
        public async Task<IActionResult> Convert(Guid id)
        {
            var r = await _service.ConvertToInvoiceAsync(id, CurrentUser.UserId!.Value);
            return r.Succeeded
                ? AjaxOk(new { invoiceId = r.Data }, r.Message)
                : AjaxFail(r.Errors);
        }

        [HttpPost]
        [HasPermission("estimates.delete")]
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
