using InvoiceSaaS.Application.DTOs.FiscalYear;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers
{
    public class FiscalYearController : BaseController
    {
        private readonly IFiscalYearService _service;

        public FiscalYearController(IFiscalYearService service, ICurrentUserService cu)
            : base(cu) => _service = service;

        // ── GET /FiscalYear ───────────────────────────────────────
        [HttpGet]
        [HasPermission("company.view")]
        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveMenu = "fiscalyear";
            ViewData["Title"] = "Fiscal Years";

            if (!CurrentUser.CompanyId.HasValue)
                return RedirectToAction("Login", "Account");

            var result = await _service.GetAllAsync(CurrentUser.CompanyId.Value);
            return View(result.Succeeded
                ? result.Data
                : Enumerable.Empty<FiscalYearDto>());
        }

        // ── GET /FiscalYear/GetAll (AJAX refresh) ─────────────────
        [HttpGet]
        [HasPermission("company.view")]
        public async Task<IActionResult> GetAll()
        {
            if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
            var r = await _service.GetAllAsync(CurrentUser.CompanyId.Value);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }

        // ── GET /FiscalYear/PreCloseCheck?id= ────────────────────
        [HttpGet]
        [HasPermission("company.edit")]
        public async Task<IActionResult> PreCloseCheck(Guid id)
        {
            var r = await _service.PreCloseCheckAsync(id);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }

        // ── POST /FiscalYear/OpenNext ─────────────────────────────
        [HttpPost]
        [HasPermission("company.edit")]
        public async Task<IActionResult> OpenNext()
        {
            if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
            var r = await _service.OpenNextAsync(
                CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        // ── POST /FiscalYear/Close ────────────────────────────────
        [HttpPost]
        [HasPermission("company.edit")]
        public async Task<IActionResult> Close([FromBody] CloseFiscalYearDto dto)
        {
            var r = await _service.CloseAsync(dto, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }

        // ── POST /FiscalYear/UpdateNotes ──────────────────────────
        [HttpPost]
        [HasPermission("company.edit")]
        public async Task<IActionResult> UpdateNotes([FromBody] UpdateFiscalYearDto dto)
        {
            var r = await _service.UpdateNotesAsync(dto, CurrentUser.UserId!.Value);
            return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
        }
    }
}
