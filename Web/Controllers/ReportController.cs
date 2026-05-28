using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class ReportController : BaseController
{
    private readonly IDashboardService _dashboardService;
    private readonly IInvoiceService _invoiceService;

    public ReportController(
        IDashboardService dashboardService,
        IInvoiceService invoiceService,
        ICurrentUserService currentUser) : base(currentUser)
    {
        _dashboardService = dashboardService;
        _invoiceService = invoiceService;
    }

    // ── GET /Report ───────────────────────────────────────────
    [HttpGet]
    [HasPermission("reports.view")]
    public async Task<IActionResult> Index()
    {
        ViewBag.ActiveMenu = "reports";
        ViewData["Title"] = "Reports";

        var stats = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId);
        return View(stats.Succeeded ? stats.Data : new Application.DTOs.Dashboard.DashboardStatsDto());
    }

    // ── GET /Report/Stats  (Ajax) ─────────────────────────────
    [HttpGet]
    [HasPermission("reports.view")]
    public async Task<IActionResult> Stats()
    {
        var result = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId);
        return result.Succeeded ? AjaxOk(result.Data) : AjaxFail(result.Errors);
    }
}
