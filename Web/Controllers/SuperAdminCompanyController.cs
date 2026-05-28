using InvoiceSaaS.Application.DTOs.Companies;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    [Route("SuperAdmin/Company")]
    public class SuperAdminCompanyController : BaseController
    {
        private readonly ICompanyService _companyService;

        public SuperAdminCompanyController(
            ICompanyService companyService,
            ICurrentUserService cu) : base(cu)
        {
            _companyService = companyService;
        }

        // ── Guard: every action checks IsSuperAdmin ───────────────
        private IActionResult? GuardSuperAdmin()
        {
            if (!CurrentUser.IsSuperAdmin)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return AjaxFail("Access denied. SuperAdmin only.");
                return RedirectToAction("AccessDenied", "Home");
            }
            return null;
        }

        // ── GET /SuperAdmin/Company ───────────────────────────────
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            if (GuardSuperAdmin() is { } g) return g;

            ViewBag.ActiveMenu = "companies";
            ViewData["Title"] = "Companies";

            var r = await _companyService.GetAllAsync();
            return View("~/Views/SuperAdmin/Companies.cshtml",
                r.Succeeded ? r.Data : Enumerable.Empty<CompanyDto>());
        }


        // ── GET /SuperAdmin/Company/GetAll  (AJAX) ────────────────
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            if (GuardSuperAdmin() is { } g) return g;
            var r = await _companyService.GetAllAsync();
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }


        // ── POST /SuperAdmin/Company/Create  (AJAX JSON) ──────────
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
        {
            if (GuardSuperAdmin() is { } g) 
                return g;

            if (string.IsNullOrWhiteSpace(dto.CompanyName))
                return AjaxFail("Company name is required.");
            if (string.IsNullOrWhiteSpace(dto.AdminEmail))
                return AjaxFail("Admin email is required.");
            if (string.IsNullOrWhiteSpace(dto.AdminPassword))
                return AjaxFail("Admin password is required.");

            var r = await _companyService.ProvisionAsync(dto, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }


        // ── GET /SuperAdmin/Company/GetById?id=... (AJAX) ─────
        // Returns company data to pre-fill the Edit modal
        [HttpGet("GetById")]
        public async Task<IActionResult> GetById(Guid id)
        {
            if (GuardSuperAdmin() is { } g) 
                return g;
            var r = await _companyService.GetAllAsync();
            if (!r.Succeeded) return AjaxFail("Could not load company.");
            var company = r.Data?.FirstOrDefault(c => c.Id == id);
            if (company is null) 
                return AjaxFail("Company not found.");
            return AjaxOk(company);
        }

        // ── POST /SuperAdmin/Company/Edit (AJAX) ──────────────
        [HttpPost("Edit")]
        public async Task<IActionResult> Edit([FromBody] EditCompanyDto dto)
        {
            if (GuardSuperAdmin() is { } g) 
                return g;
            if (dto.Id == Guid.Empty) return AjaxFail("Company ID is missing.");
            if (string.IsNullOrWhiteSpace(dto.Name)) 
                return AjaxFail("Company name is required.");
            var r = await _companyService.EditAsync(dto, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        // ── POST /SuperAdmin/Company/Delete (AJAX) ────────────
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromBody] DeleteCompanyRequest req)
        {
            if (GuardSuperAdmin() is { } g) return g;
            if (req.Id == Guid.Empty) return AjaxFail("Company ID is missing.");
            var r = await _companyService.DeleteAsync(req.Id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }



        // ── POST /SuperAdmin/Company/ToggleActive ─────────────────
        [HttpPost("ToggleActive")]
        public async Task<IActionResult> ToggleActive([FromBody] ToggleActiveRequest req)
        {
            if (GuardSuperAdmin() is { } g) return g;
            var r = await _companyService.ToggleActiveAsync(req.Id, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
        }
    }
}

// ── Request model ─────────────────────────────────────────────
public class ToggleActiveRequest
{
    public Guid Id { get; set; }
}
public class DeleteCompanyRequest
{    
    public Guid Id { get; set; } 
}