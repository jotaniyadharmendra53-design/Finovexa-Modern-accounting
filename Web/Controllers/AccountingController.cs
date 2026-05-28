//using InvoiceSaaS.Application.DTOs;
//using InvoiceSaaS.Application.Services.Interfaces;
//using InvoiceSaaS.Domain.Interfaces;
//using InvoiceSaaS.Infrastructure.Identity;
//using Microsoft.AspNetCore.Mvc;

//namespace InvoiceSaaS.Web.Controllers;

//public class AccountingController : BaseController
//{
//    private readonly IAccountingReportService _service;

//    public AccountingController(IAccountingReportService service, ICurrentUserService cu)
//        : base(cu) => _service = service;

//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public IActionResult Index()
//    {
//        ViewBag.ActiveMenu = "accounting";
//        ViewData["Title"] = "Accounting";
//        return View();
//    }

//    // ── PnL VIEW (browser navigation) ─────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public IActionResult PnL()
//    {
//        ViewBag.ActiveMenu = "accounting";
//        ViewData["Title"] = "Profit & Loss";
//        return View();
//    }

//    // ── PnL DATA (AJAX only) ───────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public async Task<IActionResult> PnLData([FromQuery] AccountingFilterDto filter)
//    {
//        if (!CurrentUser.CompanyId.HasValue) 
//            return AjaxFail("Company not found.");
//        var r = await _service.GetPnLAsync(CurrentUser.CompanyId.Value, filter);
//        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
//    }

//    // ── CashFlow VIEW ──────────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public IActionResult CashFlow()
//    {
//        ViewBag.ActiveMenu = "accounting";
//        ViewData["Title"] = "Cash Flow";
//        return View();
//    }

//    // ── CashFlow DATA ──────────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public async Task<IActionResult> CashFlowData([FromQuery] AccountingFilterDto filter)
//    {
//        if (!CurrentUser.CompanyId.HasValue) 
//            return AjaxFail("Company not found.");
//        var r = await _service.GetCashFlowAsync(CurrentUser.CompanyId.Value, filter);
//        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
//    }

//    // ── Receivables VIEW ───────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public IActionResult Receivables()
//    {
//        ViewBag.ActiveMenu = "accounting";
//        ViewData["Title"] = "Accounts Receivable";
//        return View();
//    }

//   //  ── Receivables DATA ───────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public async Task<IActionResult> ReceivablesData()
//    {
//        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
//        var r = await _service.GetReceivablesAgingAsync(CurrentUser.CompanyId.Value);
//        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
//    }

//    // ── Payables VIEW ──────────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public IActionResult Payables()
//    {
//        ViewBag.ActiveMenu = "accounting";
//        ViewData["Title"] = "Accounts Payable";
//        return View();
//    }

//    // ── Payables DATA ──────────────────────────────────────────
//    [HttpGet]
//    [HasPermission("accounting.view")]
//    public async Task<IActionResult> PayablesData()
//    {
//        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
//        var r = await _service.GetPayablesAgingAsync(CurrentUser.CompanyId.Value);
//        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
//    }
//}


// ============================================================
//  ACCOUNTING CONTROLLER — with FY picker integration
//  File: Web/Controllers/AccountingController.cs
// ============================================================

using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Accounting;
using InvoiceSaaS.Application.DTOs.FiscalYear;
using InvoiceSaaS.Application.Services.Implementations;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class AccountingController : BaseController
{
    private readonly IAccountingReportService _service;
    private readonly IFiscalYearService _fyService;

    public AccountingController(
        IAccountingReportService service,
        IFiscalYearService fyService,
        ICurrentUserService cu) : base(cu)
    {
        _service = service;
        _fyService = fyService;
    }

    // ── Index ─────────────────────────────────────────────────
    [HttpGet]
    [HasPermission("accounting.view")]
    public async Task<IActionResult> Index()
    {
        ViewBag.ActiveMenu = "accounting";
        ViewData["Title"] = "Accounting";
        await LoadFiscalYearsAsync();
        return View();
    }

    // ── PnL ───────────────────────────────────────────────────
    [HttpGet]
    [HasPermission("accounting.view")]
    public async Task<IActionResult> PnL([FromQuery] AccountingFilterDto filter)
    {
        if (!IsAjax())
        {
            ViewBag.ActiveMenu = "accounting";
            ViewData["Title"] = "Profit & Loss";
            await LoadFiscalYearsAsync();
            return View();
        }
        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");

        // If FY date range was passed — use it directly (overrides Year/Month)
        ResolveFilterFromFY(filter);

        var r = await _service.GetPnLAsync(CurrentUser.CompanyId.Value, filter);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    // ── CashFlow ──────────────────────────────────────────────
    [HttpGet]
    [HasPermission("accounting.view")]
    public async Task<IActionResult> CashFlow([FromQuery] AccountingFilterDto filter)
    {
        if (!IsAjax())
        {
            ViewBag.ActiveMenu = "accounting";
            ViewData["Title"] = "Cash Flow";
            await LoadFiscalYearsAsync();
            return View();
        }
        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
        ResolveFilterFromFY(filter);
        var r = await _service.GetCashFlowAsync(CurrentUser.CompanyId.Value, filter);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    // ── Receivables ───────────────────────────────────────────
    [HttpGet]
    [HasPermission("accounting.view")]
    public async Task<IActionResult> Receivables([FromQuery] AccountingFilterDto filter)
    {
        if (!IsAjax())
        {
            ViewBag.ActiveMenu = "accounting";
            ViewData["Title"] = "Accounts Receivable";
            await LoadFiscalYearsAsync();
            return View();
        }
        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
        var r = await _service.GetReceivablesAgingAsync(CurrentUser.CompanyId.Value);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    // ── Payables ──────────────────────────────────────────────
    [HttpGet]
    [HasPermission("accounting.view")]
    public async Task<IActionResult> Payables([FromQuery] AccountingFilterDto filter)
    {
        if (!IsAjax())
        {
            ViewBag.ActiveMenu = "accounting";
            ViewData["Title"] = "Accounts Payable";
            await LoadFiscalYearsAsync();
            return View();
        }
        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
        var r = await _service.GetPayablesAgingAsync(CurrentUser.CompanyId.Value);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    // ── Helpers ───────────────────────────────────────────────
    private async Task LoadFiscalYearsAsync()
    {
        if (!CurrentUser.CompanyId.HasValue) return;
        var r = await _fyService.GetAllAsync(CurrentUser.CompanyId.Value);
        ViewBag.FiscalYears = r.Succeeded ? r.Data : Enumerable.Empty<FiscalYearDto>();
        var current = (r.Data ?? Enumerable.Empty<FiscalYearDto>())
            .FirstOrDefault(f => f.IsDefault && f.IsOpen);
        ViewBag.CurrentFY = current;
    }

    // When DateFrom+DateTo are passed directly (from FY picker), they already
    // contain the correct range — nothing more to resolve.
    private static void ResolveFilterFromFY(AccountingFilterDto filter)
    {
        // DateFrom/DateTo already set by JS from selected FY — just ensure
        // Year is set too (used by GetDateRange fallback)
        if (filter.DateFrom.HasValue)
            filter.Year = filter.DateFrom.Value.Year;
    }

    private bool IsAjax()
        => Request.Headers["X-Requested-With"] == "XMLHttpRequest"
        || Request.Query.ContainsKey("year")
        || Request.Query.ContainsKey("month")
        || Request.Query.ContainsKey("dateFrom");
}
