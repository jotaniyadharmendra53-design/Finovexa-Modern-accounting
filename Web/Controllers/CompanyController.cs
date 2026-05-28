using FluentValidation;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Companies;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class CompanyController : BaseController
{
    private readonly ICompanyService _companyService;
    private readonly IValidator<UpdateCompanyDto> _validator;

    public CompanyController(
        ICompanyService companyService,
        IValidator<UpdateCompanyDto> validator,
        ICurrentUserService currentUser) : base(currentUser)
    {
        _companyService = companyService;
        _validator = validator;
    }

    // ── GET /Company ──────────────────────────────────────────
    [HttpGet]
    [HasPermission("company.view")]
    public async Task<IActionResult> Index()
    {
        //ViewBag.ActiveMenu = "company";
        ViewBag.ActiveMenu = "companies";
        ViewData["Title"] = "Company Settings";

        if (!CurrentUser.UserId.HasValue)
            return RedirectToAction("Login", "Account");

        var result = await _companyService.GetByUserAsync(CurrentUser.UserId.Value);
        return View(result.Succeeded ? result.Data : new CompanyDto());
    }

    // ── POST /Company/Save  (Ajax) ────────────────────────────
    [HttpPost]
    [HasPermission("company.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] UpdateCompanyDto dto)
    {

        if (dto.Id == Guid.Empty)
            return AjaxFail("Company ID is missing.");

        var v = await _validator.ValidateAsync(dto);
        if (!v.IsValid)
            return AjaxFail(v.Errors.Select(e => e.ErrorMessage));

        var result = await _companyService.UpdateAsync(dto, CurrentUser.UserId!.Value);
        return result.Succeeded
            ? AjaxOk(result.Data, result.Message)
            : AjaxFail(result.Errors);
    }

    // ── POST /Company/UploadLogo  (multipart) ─────────────────
    [HttpPost]
    [HasPermission("company.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return AjaxFail("Please select a file to upload.");

        if (file.Length > 5 * 1024 * 1024)
            return AjaxFail("File size cannot exceed 5 MB.");

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return AjaxFail("Only JPG, PNG, WebP or SVG files are allowed.");

        var companyResult = await _companyService.GetByUserAsync(CurrentUser.UserId!.Value);
        if (!companyResult.Succeeded)
            return AjaxFail("Company not found.");

        using var stream = file.OpenReadStream();
        var result = await _companyService.UploadLogoAsync(
            companyResult.Data!.Id, stream, file.FileName);

        return result.Succeeded
            ? AjaxOk(new { logoUrl = result.Data }, result.Message)
            : AjaxFail(result.Errors);
    }


    // ════════════════════════════════════════════════════════
    //  SETUP WIZARD — first-login only
    // ════════════════════════════════════════════════════════

    // ── GET /Company/Setup ────────────────────────────────────
    // Shown to admin on first login before they see the dashboard
    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        if (!CurrentUser.UserId.HasValue)
            return RedirectToAction("Login", "Account");

        // If setup is already done, redirect to dashboard
        var needsSetup = await _companyService.NeedsSetupAsync(CurrentUser.UserId.Value);
        if (!needsSetup) return RedirectToAction("Index", "Dashboard");

        ViewData["Title"] = "Complete Company Setup";
        ViewBag.ActiveMenu = "company";
        var companyResult = await _companyService.GetByUserAsync(CurrentUser.UserId.Value);
        if (!companyResult.Succeeded)
            return RedirectToAction("Index", "Dashboard");

        var c = companyResult.Data!;
        var dto = new SetupCompanyDto
        {
            CompanyId = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Address = c.Address,
            TaxNumber = c.TaxNumber,
            CurrencyCode = c.CurrencyCode,
            Timezone = c.Timezone,
            DateFormat = c.DateFormat,
            TaxType = c.TaxType ?? Domain.Enums.TaxType.GST
        };
        return View(dto);
    }

    // ── POST /Company/Setup (AJAX) ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup([FromBody] SetupCompanyDto dto)
    {
        if (!CurrentUser.UserId.HasValue)
            return AjaxFail("Not authenticated.");
        if (dto.CompanyId == Guid.Empty)
            return AjaxFail("Company ID is missing.");
        if (string.IsNullOrWhiteSpace(dto.NewPassword))
            return AjaxFail("New password is required.");
        if (dto.NewPassword != dto.ConfirmNewPassword)
            return AjaxFail("Passwords do not match.");

        var result = await _companyService.CompleteSetupAsync(dto, CurrentUser.UserId.Value);
        return result.Succeeded
            ? AjaxOk(message: "Setup complete! Redirecting…")
            : AjaxFail(result.Errors);
    }


}
