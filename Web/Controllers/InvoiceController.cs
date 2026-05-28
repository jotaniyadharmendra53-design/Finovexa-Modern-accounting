using Dapper;
using FluentValidation;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Invoices;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Enums;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class InvoiceController : BaseController
{
    private readonly IInvoiceService _invoiceService;
    private readonly IClientService _clientService;
    private readonly ICompanyService _companyService;
    private readonly IValidator<CreateInvoiceDto> _createValidator;
    private readonly IValidator<UpdateInvoiceDto> _updateValidator;
    private readonly IDapperContext _dapper;
    private readonly IProductService _productService;
    private readonly IExchangeRateService _exchangeRateService;

    public InvoiceController(
        IInvoiceService invoiceService,
        IClientService clientService,
        ICompanyService companyService,
        IValidator<CreateInvoiceDto> createValidator,
        IValidator<UpdateInvoiceDto> updateValidator,
        IDapperContext dapper,
        IProductService productService,
        ICurrentUserService currentUser,
        IExchangeRateService exchangeRateService) : base(currentUser)
    {
        _invoiceService = invoiceService;
        _clientService = clientService;
        _companyService = companyService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _dapper = dapper;
        _productService = productService;
        _exchangeRateService = exchangeRateService;
    }

    //exchangeRate
    [HttpGet]
    public async Task<IActionResult> GetExchangeRate(string from, string to)
    {
        try
        {
            var rate = await _exchangeRateService.GetRateAsync(from, to);
            return Json(new { success = true, rate });
        }
        catch
        {
            return Json(new { success = false, rate = 1 });
        }
    }


    // ── Count extensions for Invoice ─────────────────────────
    public static class InvoiceCountExtension
    {
        public static async Task<int> CountByCompanyAsync(
            IDapperContext dapper, Guid companyId, Domain.Interfaces.InvoiceFilterDto f)
        {
            using var conn = dapper.CreateConnection();
            var sql = new System.Text.StringBuilder("""
            SELECT COUNT(*) FROM dbo.Invoices i
            INNER JOIN dbo.Clients cl ON cl.Id = i.ClientId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
            """);
            var p = new DynamicParameters();
            p.Add("CompanyId", companyId);
            if (f.Status.HasValue) { sql.Append(" AND i.Status=@Status"); p.Add("Status", (byte)f.Status.Value); }
            if (f.ClientId.HasValue) { sql.Append(" AND i.ClientId=@Cid"); p.Add("Cid", f.ClientId.Value); }
            if (f.DateFrom.HasValue) { sql.Append(" AND i.IssueDate>=@Df"); p.Add("Df", f.DateFrom); }
            if (f.DateTo.HasValue) { sql.Append(" AND i.IssueDate<=@Dt"); p.Add("Dt", f.DateTo); }
            if (!string.IsNullOrWhiteSpace(f.Search))
            { sql.Append(" AND (i.InvoiceNumber LIKE @S OR cl.Name LIKE @S)"); p.Add("S", $"%{f.Search}%"); }
            return await conn.ExecuteScalarAsync<int>(sql.ToString(), p);
        }
    }



    // ── GET /Invoice  (list) ──────────────────────────────────
    [HttpGet]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> Index([FromQuery] Application.DTOs.Invoices.InvoiceFilterDto filter)
    {
        ViewBag.ActiveMenu = "invoices";
        ViewData["Title"] = "Invoices";

        if (!CurrentUser.CompanyId.HasValue)
            return View(Enumerable.Empty<InvoiceListItemDto>());

        // Load client select list for filter dropdown
        var clients = await _clientService.GetSelectListAsync(CurrentUser.CompanyId.Value);
        ViewBag.Clients = clients.Data ?? Enumerable.Empty<SelectItemDto>();
        ViewBag.Filter = filter;


        // Count total for pagination
        var domainFilter = new Domain.Interfaces.InvoiceFilterDto
        {
            Status = filter.Status,
            ClientId = filter.ClientId,
            DateFrom = filter.DateFrom,
            DateTo = filter.DateTo,
            Search = filter.Search,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
        var totalCount = await InvoiceCountExtension.CountByCompanyAsync(
            _dapper, CurrentUser.CompanyId.Value, domainFilter);

        ViewBag.Page = filter.Page;
        ViewBag.PageSize = filter.PageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = filter.PageSize > 0
            ? (int)Math.Ceiling((double)totalCount / filter.PageSize) : 0;



        var result = await _invoiceService.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        return View(result.Succeeded ? result.Data : Enumerable.Empty<InvoiceListItemDto>());
    }

    // ── GET /Invoice/GetList  (Ajax — DataTable reload) ───────
    [HttpGet]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> GetList([FromQuery] Application.DTOs.Invoices.InvoiceFilterDto filter)
    {
        if (!CurrentUser.CompanyId.HasValue)
            return AjaxOk(Array.Empty<object>());

        var result = await _invoiceService.GetByCompanyAsync(CurrentUser.CompanyId.Value, filter);
        return result.Succeeded ? AjaxOk(result.Data) : AjaxFail(result.Errors);
    }

    // ── GET /Invoice/Create ───────────────────────────────────
    [HttpGet]
    [HasPermission("invoices.create")]
    public async Task<IActionResult> Create(Guid? clientId = null)
    {
        ViewBag.ActiveMenu = "invoices-create";
        ViewData["Title"] = "New Invoice";

        await LoadClientDropdown();
        await LoadProductDropdown();
        ViewBag.PreselectedClientId = clientId?.ToString() ?? "";

        var companyResult = await _companyService.GetByUserAsync(CurrentUser.UserId!.Value);
        ViewBag.CompanyCurrency = companyResult.Data?.CurrencyCode ?? "INR";


        return View("CreateEdit", new CreateInvoiceDto
        {
            IssueDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30)
        });
    }

    // ── GET /Invoice/Edit/{id} ────────────────────────────────
    //[HttpGet]
    //[HasPermission("invoices.edit")]
    //public async Task<IActionResult> Edit(Guid id)
    //{
    //    var result = await _invoiceService.GetByIdAsync(id);
    //    if (!result.Succeeded)
    //    {
    //        SetErrorToast("Invoice not found.");
    //        return RedirectToAction("Index");
    //    }

    //    var inv = result.Data!;
    //    if (inv.Status != InvoiceStatus.Draft)
    //    {
    //        SetErrorToast("Only Draft invoices can be edited.");
    //        return RedirectToAction("Detail", new { id });
    //    }

    //    ViewBag.ActiveMenu = "invoices";
    //    ViewData["Title"] = $"Edit {inv.InvoiceNumber}";
    //    ViewBag.InvoiceId = id;
    //    await LoadClientDropdown();
    //    await LoadProductDropdown();

    //    var companyResult = await _companyService.GetByUserAsync(CurrentUser.UserId!.Value);
    //    ViewBag.CompanyCurrency = companyResult.Data?.CurrencyCode ?? "INR";

    //    var dto = new UpdateInvoiceDto
    //    {
    //        Id = inv.Id,
    //        ClientId = inv.ClientId,
    //        IssueDate = inv.IssueDate,
    //        DueDate = inv.DueDate,
    //        TaxRate = inv.TaxRate,
    //        Discount = inv.Discount,
    //        Notes = inv.Notes,
    //        Terms = inv.Terms,
    //        Status = inv.Status,
    //        Items = inv.Items
    //    };
    //    ViewBag.ExistingInvoice = inv;
    //    return View("CreateEdit", dto);
    //}

    [HttpGet]
    [HasPermission("invoices.edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var result = await _invoiceService.GetByIdAsync(id);
        if (!result.Succeeded) { SetErrorToast("Invoice not found."); return RedirectToAction("Index"); }

        var inv = result.Data!;
        if (inv.Status == InvoiceStatus.Paid)
        {
            SetErrorToast("Paid invoices cannot be edited.");
            return RedirectToAction("Detail", new { id });
        }
        if (inv.Status == InvoiceStatus.Cancelled)
        {
            SetErrorToast("Cancelled invoices cannot be edited.");
            return RedirectToAction("Detail", new { id });
        }

        ViewBag.ActiveMenu = "invoices";
        ViewData["Title"] = $"Edit {inv.InvoiceNumber}";
        ViewBag.InvoiceId = id;
        ViewBag.RequiresRemark = inv.Status != InvoiceStatus.Draft;
        await LoadClientDropdown();
        await LoadProductDropdown();

        var companyResult = await _companyService.GetByUserAsync(CurrentUser.UserId!.Value);
        ViewBag.CompanyCurrency = companyResult.Data?.CurrencyCode ?? "INR";

        ViewBag.ExistingInvoice = inv;
        return View("CreateEdit", new UpdateInvoiceDto
        {
            Id = inv.Id,
            ClientId = inv.ClientId,
            IssueDate = inv.IssueDate,
            DueDate = inv.DueDate,
            TaxRate = inv.TaxRate,
            Discount = inv.Discount,
            Notes = inv.Notes,
            Terms = inv.Terms,
            Status = inv.Status,
            Items = inv.Items
        });
    }

    [HttpPost]
    [HasPermission("invoices.edit")]
    public async Task<IActionResult> WriteOff(Guid id)
    {
        var result = await _invoiceService.WriteOffAsync(id, CurrentUser.UserId!.Value);
        return result.Succeeded ? AjaxOk(message: result.Message) : AjaxFail(result.Errors);
    }


    // ── POST /Invoice/Save  (Create + Edit combined, Ajax) ────
    [HttpPost]
    [HasPermission("invoices.create")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveInvoiceRequest req)
    {
        if (!CurrentUser.CompanyId.HasValue)
            return AjaxFail("Company context not found.");

        if (req.IsEdit)
        {
            var dto = new UpdateInvoiceDto
            {
                Id = req.Id!.Value,
                ClientId = req.ClientId,
                IssueDate = req.IssueDate,
                DueDate = req.DueDate,
                TaxRate = req.TaxRate,
                Discount = req.Discount,
                Notes = req.Notes,
                Terms = req.Terms,
                EditRemark = req.EditRemark,
                Status = InvoiceStatus.Draft,
                Items = req.Items,
                SendNow = req.SendNow
            };
            var v = await _updateValidator.ValidateAsync(dto);
            if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));

            var result = await _invoiceService.UpdateAsync(dto, CurrentUser.UserId!.Value);
            if (!result.Succeeded) return AjaxFail(result.Errors);

            if (req.SendNow)
                await _invoiceService.SendAsync(result.Data!.Id, CurrentUser.UserId!.Value);

            return AjaxOk(new { id = result.Data!.Id }, result.Message);
        }
        else
        {
            var dto = new CreateInvoiceDto
            {
                ClientId = req.ClientId,
                IssueDate = req.IssueDate,
                DueDate = req.DueDate,
                TaxRate = req.TaxRate,
                Discount = req.Discount,
                Notes = req.Notes,
                Terms = req.Terms,
                Items = req.Items,
                SendNow = req.SendNow,
                CurrencyCode = req.CurrencyCode,
                ExchangeRate = req.ExchangeRate,
            };
            var v = await _createValidator.ValidateAsync(dto);
            if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));

            var result = await _invoiceService.CreateAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
            if (!result.Succeeded) return AjaxFail(result.Errors);

            return AjaxOk(new { id = result.Data!.Id }, result.Message ?? "Invoice created.");
        }
    }

    // ── GET /Invoice/Detail/{id} ──────────────────────────────
    [HttpGet]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var result = await _invoiceService.GetByIdAsync(id);
        if (!result.Succeeded)
        {
            SetErrorToast("Invoice not found.");
            return RedirectToAction("Index");
        }
        ViewBag.ActiveMenu = "invoices";
        ViewData["Title"] = result.Data!.InvoiceNumber;
        return View(result.Data);
    }

    // ── POST /Invoice/Send/{id}  (Ajax) ───────────────────────
    [HttpPost]
    [HasPermission("invoices.send")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(Guid id)
    {
        var result = await _invoiceService.SendAsync(id, CurrentUser.UserId!.Value);
        return result.Succeeded ? AjaxOk(message: result.Message) : AjaxFail(result.Errors);
    }

    // ── POST /Invoice/MarkPaid/{id}  (Ajax) ───────────────────
    [HttpPost]
    [HasPermission("invoices.paid")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidRequest req)
    {
        var result = await _invoiceService.MarkAsPaidAsync(id, req.Amount, CurrentUser.UserId!.Value);
        return result.Succeeded ? AjaxOk(message: result.Message) : AjaxFail(result.Errors);
    }

    // ── POST /Invoice/Cancel/{id}  (Ajax) ─────────────────────
    [HttpPost]
    [HasPermission("invoices.edit")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _invoiceService.CancelAsync(id, CurrentUser.UserId!.Value);
        return result.Succeeded ? AjaxOk(message: result.Message) : AjaxFail(result.Errors);
    }

    // ── DELETE /Invoice/Delete/{id}  (Ajax) ───────────────────
    [HttpDelete]
    [HasPermission("invoices.delete")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _invoiceService.DeleteAsync(id, CurrentUser.UserId!.Value);
        return result.Succeeded ? AjaxOk(message: result.Message) : AjaxFail(result.Errors);
    }

    // ── GET /Invoice/Print/{id}  (printable HTML) ────────────
    [HttpGet]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> Print(Guid id)
    {
        var result = await _invoiceService.GetByIdAsync(id);
        if (!result.Succeeded) return NotFound();
        ViewData["Title"] = result.Data!.InvoiceNumber;
        return View("Print", result.Data);
    }


    [HttpGet]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> Templates()
    {
        ViewBag.ActiveMenu = "templates";
        ViewData["Title"] = "PDF Templates";

        // Load current template from company settings
        var company = await _companyService.GetByUserAsync(CurrentUser.UserId!.Value);
        ViewBag.CurrentTemplate = company.Data?.InvoiceTemplate ?? "classic";

        return View();
    }

    // ── POST /Invoice/SaveTemplate ────────────────────────────
    // Saves the selected template to the Companies table in DB
    [HttpPost]
    [HasPermission("invoices.view")]
    public async Task<IActionResult> SaveTemplate([FromForm] string template)
    {
        if (!CurrentUser.CompanyId.HasValue)
            return AjaxFail("Company context not found.");

        var result = await _companyService.SaveTemplateAsync(
            CurrentUser.CompanyId.Value, template);

        return result.Succeeded
            ? AjaxOk(message: result.Message)
            : AjaxFail(result.Errors);
    }


    // ── GET /Invoice/TemplatePreview?tpl=classic ──────────────
    public IActionResult TemplatePreview()
    {
        return View();
    }


    // ── Private helpers ───────────────────────────────────────
    private async Task LoadClientDropdown()
    {
        if (!CurrentUser.CompanyId.HasValue) return;
        var clients = await _clientService.GetSelectListAsync(CurrentUser.CompanyId.Value);
        ViewBag.Clients = clients.Data ?? Enumerable.Empty<SelectItemDto>();
    }

    private async Task LoadProductDropdown()
    {
        if (!CurrentUser.CompanyId.HasValue) return;

        var products = await _productService.GetSelectListAsync(CurrentUser.CompanyId.Value);
        ViewBag.Products = products.Data ?? Enumerable.Empty<SelectItemDto>();
    }
}

// ── Request models for [FromBody] ─────────────────────────────
public class SaveInvoiceRequest
{
    public bool IsEdit { get; set; }
    public Guid? Id { get; set; }
    public Guid ClientId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Discount { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
    public bool SendNow { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public decimal ExchangeRate { get; set; } = 1m;

    public string? EditRemark { get; set; }
}

public class MarkPaidRequest
{
    public decimal Amount { get; set; }
}
