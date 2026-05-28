using InvoiceSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InvoiceSaaS.Web.Controllers;

/// <summary>
/// All controllers inherit BaseController.
/// Sets ViewBag.CurrentUser, Permissions, CompanyId on every request.
/// Provides helper methods for Ajax responses.
/// </summary>
[Authorize]
public abstract class BaseController : Controller
{
    protected readonly ICurrentUserService CurrentUser;

    protected BaseController(ICurrentUserService currentUser)
    {
        CurrentUser = currentUser;
    }

    // Runs before every action — populates ViewBag for layout/sidebar
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        ViewBag.CurrentUserId = CurrentUser.UserId;
        ViewBag.CurrentUserName = CurrentUser.FullName ?? "User";
        ViewBag.CurrentEmail = CurrentUser.Email;
        ViewBag.CurrentRole = CurrentUser.RoleName;
        ViewBag.IsSuperAdmin = CurrentUser.IsSuperAdmin;
        ViewBag.CompanyId = CurrentUser.CompanyId;
        ViewBag.Permissions = CurrentUser.Permissions.ToHashSet();

        // Active menu — set by each controller action
        ViewBag.ActiveMenu ??= "dashboard";

    // Try to load company info (logo) for sidebar branding.
    try
    {
        if (CurrentUser.UserId.HasValue)
        {
            var svc = HttpContext.RequestServices.GetService(typeof(InvoiceSaaS.Application.Services.Interfaces.ICompanyService)) as InvoiceSaaS.Application.Services.Interfaces.ICompanyService;
            if (svc is not null)
            {
                var compRes = svc.GetByUserAsync(CurrentUser.UserId.Value).GetAwaiter().GetResult();
                if (compRes.Succeeded && compRes.Data is not null && !string.IsNullOrWhiteSpace(compRes.Data.Logo))
                {
                    var logo = compRes.Data.Logo!;
                    // Normalize: if service stored filename only, prepend uploads path; if it already is a URL, use as-is.
                    ViewBag.CompanyLogo = logo.StartsWith("/") ? logo : $"/uploads/logos/{logo}";
                }
            }
        }
    }
    catch
    {
        // Ignore failures here — logo is optional and should not break page rendering
    }
    }

    // ── Ajax Response Helpers ────────────────────────────────

    protected IActionResult AjaxOk(object? data = null, string? message = null)
        => Json(new { success = true, message, data });

    protected IActionResult AjaxFail(string message, IEnumerable<string>? errors = null)
        => Json(new { success = false, message, errors = errors ?? Array.Empty<string>() });

    protected IActionResult AjaxFail(IEnumerable<string> errors)
        => Json(new { success = false, message = "Validation failed.", errors });

    protected IActionResult AjaxValidationErrors()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        return Json(new { success = false, message = "Validation failed.", errors });
    }

    // ── Permission Guard ─────────────────────────────────────
    protected IActionResult? ForbidIfMissing(string permissionCode)
    {
        if (!CurrentUser.HasPermission(permissionCode))
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return AjaxFail("You do not have permission to perform this action.");
            return RedirectToAction("AccessDenied", "Home");
        }
        return null;
    }

    // ── Toast helper (stored in TempData for next request) ───
    protected void SetSuccessToast(string message) => TempData["ToastSuccess"] = message;
    protected void SetErrorToast(string message) => TempData["ToastError"] = message;
}