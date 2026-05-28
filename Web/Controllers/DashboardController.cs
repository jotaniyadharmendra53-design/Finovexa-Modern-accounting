using InvoiceSaaS.Application.Services.Implementations;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IDashboardService _dashboardService;
    private readonly ICompanyService _companyService;

    public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUser, ICompanyService companyService)
        : base(currentUser)
    {
        _dashboardService = dashboardService;
        _companyService = companyService;
    }
  

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.ActiveMenu = "dashboard";
        ViewData["Title"]  = "Dashboard";

        // ── SuperAdmin: show platform dashboard ───────────────
        if (CurrentUser.IsSuperAdmin)
        {
            var saResult = await _dashboardService.GetSuperAdminStatsAsync(ct);
            return View(
                "~/Views/SuperAdmin/Dashboard.cshtml",
                saResult.Succeeded
                    ? saResult.Data!
                    : new Application.DTOs.Dashboard.SuperAdminStatsDto());
        }

        // ── Company user: check first-login setup ─────────────
        if (CurrentUser.UserId.HasValue)
        {
            var needsSetup = await _companyService.NeedsSetupAsync(
                CurrentUser.UserId.Value, ct);

            if (needsSetup)
                return RedirectToAction("Setup", "Company");
        }

        //var result = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId, ct);
        //var stats  = result.Succeeded ? result.Data! : new Application.DTOs.Dashboard.DashboardStatsDto();
        //return View(stats);

        var companyResult = await _companyService
            .GetByUserAsync(CurrentUser.UserId!.Value, ct);
        ViewBag.CompanyCurrency = companyResult.Data?.CurrencyCode ?? "INR";


        var result = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId, ct);
        return View(result.Succeeded
            ? result.Data!
            : new Application.DTOs.Dashboard.DashboardStatsDto());
    }

    //[HttpGet]
    //public async Task<IActionResult> GetStats(CancellationToken ct)
    //{
    //    var result = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId, ct);
    //    return result.Succeeded ? AjaxOk(result.Data) : AjaxFail(result.Errors);
    //}

    [HttpGet]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (CurrentUser.IsSuperAdmin)
        {
            var r = await _dashboardService.GetSuperAdminStatsAsync(ct);
            return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
        }
        var result = await _dashboardService.GetStatsAsync(CurrentUser.CompanyId, ct);
        return result.Succeeded ? AjaxOk(result.Data) : AjaxFail(result.Errors);
    }

}
