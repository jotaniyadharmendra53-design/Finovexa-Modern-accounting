using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Invoices;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Enums;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using static System.Net.Mime.MediaTypeNames;
using AppInvoiceFilterDto = InvoiceSaaS.Application.DTOs.Invoices.InvoiceFilterDto;
using DomainInvoiceFilterDto = InvoiceSaaS.Domain.Interfaces.InvoiceFilterDto;

namespace InvoiceSaaS.Application.Services.Implementations
{
    // ═══════════════════════════════════════════════════════════
    //  InvoiceService
    // ═══════════════════════════════════════════════════════════
    public class InvoiceService : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IClientRepository _clientRepo;
        private readonly ICompanyRepository _companyRepo;
        private readonly IEmailService _emailService;
        private readonly ILogger<InvoiceService> _logger;
        private readonly string _appBaseUrl;
        private readonly IFiscalYearService _fyService;
        private readonly string _webRootPath;


        public InvoiceService(IInvoiceRepository invoiceRepo, IClientRepository clientRepo,
            ICompanyRepository companyRepo, IEmailService emailService, ILogger<InvoiceService> logger, IConfiguration configuration, IFiscalYearService fyService, string webRootPath)
        {
            _invoiceRepo = invoiceRepo;
            _clientRepo = clientRepo;
            _companyRepo = companyRepo;
            _emailService = emailService;
            _logger = logger;
            _appBaseUrl = (configuration["AppUrl"] ?? "").TrimEnd('/');
            _fyService = fyService;
            _webRootPath = webRootPath;
        }

        //public async Task<ServiceResult<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        //{
        //    var invoice = await _invoiceRepo.GetWithItemsAsync(id, ct);
        //    if (invoice is null) 
        //        return ServiceResult<InvoiceDto>.Failure("Invoice not found.");
        //    return ServiceResult<InvoiceDto>.Success(MapToDto(invoice));
        //}

        public async Task<ServiceResult<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetWithItemsAsync(id, ct);
            if (invoice is null) return ServiceResult<InvoiceDto>.Failure("Invoice not found.");

            var dto = MapToDto(invoice);

            var history = await _invoiceRepo.GetEditHistoryAsync(id, ct);
            dto.EditHistory = history.Select(h => new InvoiceEditHistoryDto
            {
                Id = h.Id,
                EditedAt = h.EditedAt,
                Remark = h.Remark,
                FromStatus = ((InvoiceStatus)h.FromStatus).ToString()
            }).ToList();

            return ServiceResult<InvoiceDto>.Success(dto);
        }


        public async Task<ServiceResult<IEnumerable<InvoiceListItemDto>>> GetByCompanyAsync(Guid companyId, AppInvoiceFilterDto filter, CancellationToken ct = default)
        {
            var domainFilter = new Domain.Interfaces.InvoiceFilterDto
            {
                Status = filter.Status,
                ClientId = filter.ClientId,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                Search = filter.Search,
                SortBy = filter.SortBy,
                SortDesc = filter.SortDesc,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
            var invoices = await _invoiceRepo.GetByCompanyAsync(companyId, domainFilter, ct);
            return ServiceResult<IEnumerable<InvoiceListItemDto>>.Success(invoices.Select(MapToListDto));
        }

        public async Task<ServiceResult<InvoiceDto>> CreateAsync(CreateInvoiceDto dto, Guid companyId, Guid createdBy, CancellationToken ct = default)
        {
            try
            {
                // ── Fiscal year lock check ──────────────────────
                var fyLock = await _fyService.GetLockViolationAsync(companyId, dto.IssueDate, ct);
                if (fyLock is not null) return ServiceResult<InvoiceDto>.Failure(fyLock);

                var invoiceNumber = await _invoiceRepo.GetNextInvoiceNumberAsync(companyId, ct);
                var subTotal = dto.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
                var taxAmount = Math.Round(subTotal * (dto.TaxRate / 100), 2);
                var total = subTotal + taxAmount - dto.Discount;


                //
                var exchangeRate = dto.ExchangeRate > 0 ? dto.ExchangeRate : 1m;
                var baseCurrency = !string.IsNullOrWhiteSpace(dto.CurrencyCode)
                        ? dto.CurrencyCode.Trim().ToUpper() : "INR";
                var baseAmount = Math.Round(total * exchangeRate, 2);

                //
                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    CompanyId = companyId,
                    ClientId = dto.ClientId,
                    IssueDate = dto.IssueDate,
                    DueDate = dto.DueDate,
                    Status = InvoiceStatus.Draft,
                    SubTotal = subTotal,
                    TaxRate = dto.TaxRate,
                    TaxAmount = taxAmount,
                    Discount = dto.Discount,
                    Total = total,
                    CurrencyCode = baseCurrency,      
                    ExchangeRate = exchangeRate,      
                    BaseAmount = baseAmount,        
                    Notes = dto.Notes?.Trim(),
                    Terms = dto.Terms?.Trim(),
                    CreatedBy = createdBy
                };
                await _invoiceRepo.AddAsync(invoice, ct);

                // Add items
                var items = dto.Items.Select((item, index) => new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = Math.Round(item.Quantity * item.UnitPrice, 2),
                    SortOrder = index
                });
                await _invoiceRepo.AddItemsAsync(items, ct);

                // If SendNow requested
                if (dto.SendNow)
                    await SendAsync(invoice.Id, createdBy, ct);

                _logger.LogInformation("Invoice {Number} created by {UserId}", invoiceNumber, createdBy);
                var created = await _invoiceRepo.GetWithItemsAsync(invoice.Id, ct);
                return ServiceResult<InvoiceDto>.Success(MapToDto(created!), "Invoice created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return ServiceResult<InvoiceDto>.Failure("An error occurred while creating the invoice.");
            }
        }

        //public async Task<ServiceResult<InvoiceDto>> UpdateAsync(UpdateInvoiceDto dto, Guid updatedBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var invoice = await _invoiceRepo.GetWithItemsAsync(dto.Id, ct);
        //        if (invoice is null) return ServiceResult<InvoiceDto>.Failure("Invoice not found.");
        //        if (invoice.Status != InvoiceStatus.Draft)
        //            return ServiceResult<InvoiceDto>.Failure("Only Draft invoices can be edited.");

        //        // ── Fiscal year lock check ──────────────────────
        //        var fyLock = await _fyService.GetLockViolationAsync(invoice.CompanyId, dto.IssueDate, ct);
        //        if (fyLock is not null) return ServiceResult<InvoiceDto>.Failure(fyLock);


        //        var subTotal = dto.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
        //        var taxAmount = Math.Round(subTotal * (dto.TaxRate / 100), 2);

        //        invoice.ClientId = dto.ClientId;
        //        invoice.IssueDate = dto.IssueDate;
        //        invoice.DueDate = dto.DueDate;
        //        invoice.TaxRate = dto.TaxRate;
        //        invoice.TaxAmount = taxAmount;
        //        invoice.SubTotal = subTotal;
        //        invoice.Discount = dto.Discount;
        //        invoice.Total = subTotal + taxAmount - dto.Discount;
        //        invoice.Notes = dto.Notes?.Trim();
        //        invoice.Terms = dto.Terms?.Trim();
        //        invoice.UpdatedAt = DateTime.UtcNow;
        //        invoice.UpdatedBy = updatedBy;
        //        //await _invoiceRepo.UpdateAsync(invoice, ct);
        //        await _invoiceRepo.UpdateInvoiceAsync(invoice, ct);
        //        // Replace all items
        //        await _invoiceRepo.DeleteItemsByInvoiceAsync(invoice.Id, ct);
        //        var items = dto.Items.Select((item, index) => new InvoiceItem
        //        {
        //            InvoiceId = invoice.Id,
        //            Description = item.Description.Trim(),
        //            Quantity = item.Quantity,
        //            UnitPrice = item.UnitPrice,
        //            Amount = Math.Round(item.Quantity * item.UnitPrice, 2),
        //            SortOrder = index
        //        });
        //        await _invoiceRepo.AddItemsAsync(items, ct);

        //        var updated = await _invoiceRepo.GetWithItemsAsync(invoice.Id, ct);
        //        return ServiceResult<InvoiceDto>.Success(MapToDto(updated!), "Invoice updated successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error updating invoice {Id}", dto.Id);
        //        return ServiceResult<InvoiceDto>.Failure("An error occurred while updating the invoice.");
        //    }
        //}

        public async Task<ServiceResult<InvoiceDto>> UpdateAsync(UpdateInvoiceDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                var invoice = await _invoiceRepo.GetWithItemsAsync(dto.Id, ct);
                if (invoice is null) return ServiceResult<InvoiceDto>.Failure("Invoice not found.");

                if (invoice.Status == InvoiceStatus.Paid)
                    return ServiceResult<InvoiceDto>.Failure("Paid invoices cannot be edited.");
                if (invoice.Status == InvoiceStatus.Cancelled)
                    return ServiceResult<InvoiceDto>.Failure("Cancelled invoices cannot be edited.");

                var isDraft = invoice.Status == InvoiceStatus.Draft;
                if (!isDraft && string.IsNullOrWhiteSpace(dto.EditRemark))
                    return ServiceResult<InvoiceDto>.Failure("A remark is required when editing a sent invoice.");

                var fyLock = await _fyService.GetLockViolationAsync(invoice.CompanyId, dto.IssueDate, ct);
                if (fyLock is not null) return ServiceResult<InvoiceDto>.Failure(fyLock);

                var previousStatus = invoice.Status;
                var subTotal = dto.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
                var taxAmount = Math.Round(subTotal * (dto.TaxRate / 100), 2);

                invoice.ClientId = dto.ClientId;
                invoice.IssueDate = dto.IssueDate;
                invoice.DueDate = dto.DueDate;
                invoice.TaxRate = dto.TaxRate;
                invoice.TaxAmount = taxAmount;
                invoice.SubTotal = subTotal;
                invoice.Discount = dto.Discount;
                invoice.Total = subTotal + taxAmount - dto.Discount;
                invoice.Notes = dto.Notes?.Trim();
                invoice.Terms = dto.Terms?.Trim();
                invoice.LastEditRemark = dto.EditRemark?.Trim();
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = updatedBy;

                await _invoiceRepo.UpdateInvoiceAsync(invoice, ct);
                await _invoiceRepo.DeleteItemsByInvoiceAsync(invoice.Id, ct);

                var items = dto.Items.Select((item, index) => new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    Description = item.Description.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = Math.Round(item.Quantity * item.UnitPrice, 2),
                    SortOrder = index
                });
                await _invoiceRepo.AddItemsAsync(items, ct);

                // Save audit trail for non-Draft edits
                if (!isDraft && !string.IsNullOrWhiteSpace(dto.EditRemark))
                {
                    await _invoiceRepo.AddEditHistoryAsync(new InvoiceEditHistory
                    {
                        InvoiceId = invoice.Id,
                        EditedBy = updatedBy,
                        Remark = dto.EditRemark.Trim(),
                        FromStatus = (byte)previousStatus
                    }, ct);
                }

                var updated = await _invoiceRepo.GetWithItemsAsync(invoice.Id, ct);
                return ServiceResult<InvoiceDto>.Success(MapToDto(updated!), "Invoice updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice {Id}", dto.Id);
                return ServiceResult<InvoiceDto>.Failure("An error occurred while updating the invoice.");
            }
        }

        public async Task<ServiceResult> WriteOffAsync(Guid id, Guid updatedBy, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id, ct);
            if (invoice is null) return ServiceResult.Failure("Invoice not found.");
            if (invoice.Status != InvoiceStatus.PartiallyPaid)
                return ServiceResult.Failure("Write-off is only available for partially paid invoices.");

            var balance = invoice.Total - invoice.PaidAmount;

            await _invoiceRepo.WriteOffAsync(id, updatedBy, ct);

            await _invoiceRepo.AddEditHistoryAsync(new InvoiceEditHistory
            {
                InvoiceId = id,
                EditedBy = updatedBy,
                Remark = $"Balance of {balance:N2} {invoice.CurrencyCode} written off as rounding adjustment.",
                FromStatus = (byte)invoice.Status
            }, ct);

            return ServiceResult.Success($"Balance of {balance:N2} written off. Invoice marked as Paid.");
        }



        public async Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id, ct);
            if (invoice is null) return ServiceResult.Failure("Invoice not found.");
            if (invoice.Status == InvoiceStatus.Paid)
                return ServiceResult.Failure("Paid invoices cannot be deleted.");
            await _invoiceRepo.DeleteAsync(id, deletedBy, ct);
            return ServiceResult.Success("Invoice deleted successfully.");
        }

        public async Task<ServiceResult> SendAsync(Guid id, Guid updatedBy, CancellationToken ct = default)
        {
            Console.WriteLine("🔥 SendAsync called");
            try
            {
                var invoice = await _invoiceRepo.GetWithItemsAsync(id, ct);
                if (invoice is null) return ServiceResult.Failure("Invoice not found.");
                if (invoice.Status == InvoiceStatus.Paid)
                    return ServiceResult.Failure("Invoice is already paid.");
                if (invoice.Status == InvoiceStatus.Cancelled)
                    return ServiceResult.Failure("Cancelled invoices cannot be sent.");
                if (string.IsNullOrEmpty(invoice.Client?.Email))
                    return ServiceResult.Failure("Client does not have an email address. Please update the client and try again.");

                await _invoiceRepo.UpdateStatusAsync(id, InvoiceStatus.Sent, updatedBy, ct);

                var company = await _companyRepo.GetByIdAsync(invoice.CompanyId, ct);
                await _emailService.QueueAsync(new EmailMessage
                {
                    ToEmail = invoice.Client.Email,
                    ToName = invoice.Client.Name,
                    Subject = $"Invoice {invoice.InvoiceNumber} from {company?.Name}",
                    Body = BuildInvoiceEmail(invoice, company),
                    IsHtml = true,
                    RelatedId = invoice.Id,
                    EmailType = "InvoiceSent"
                }, ct);
                Console.WriteLine("📨 Email queued");
                return ServiceResult.Success($"Invoice {invoice.InvoiceNumber} sent to {invoice.Client.Email}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invoice {Id}", id);
                return ServiceResult.Failure("An error occurred while sending the invoice.");
            }
        }

        //public async Task<ServiceResult> MarkAsPaidAsync(Guid id, decimal amount, Guid updatedBy, CancellationToken ct = default)
        //{
        //    var invoice = await _invoiceRepo.GetByIdAsync(id, ct);
        //    if (invoice is null) return ServiceResult.Failure("Invoice not found.");
        //    if (invoice.Status == InvoiceStatus.Cancelled)
        //        return ServiceResult.Failure("Cancelled invoices cannot be marked as paid.");

        //    invoice.PaidAmount += amount;
        //    var newStatus = invoice.PaidAmount >= invoice.Total
        //        ? InvoiceStatus.Paid
        //        : InvoiceStatus.PartiallyPaid;
        //    invoice.PaidAt = newStatus == InvoiceStatus.Paid ? DateTime.UtcNow : null;
        //    invoice.UpdatedAt = DateTime.UtcNow;
        //    invoice.UpdatedBy = updatedBy;
        //    await _invoiceRepo.UpdateAsync(invoice, ct);
        //    await _invoiceRepo.UpdateStatusAsync(id, newStatus, updatedBy, ct);
        //    return ServiceResult.Success("Payment recorded successfully.");
        //}

        public async Task<ServiceResult> MarkAsPaidAsync(Guid id, decimal amount, Guid updatedBy, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id, ct);
            if (invoice is null) return ServiceResult.Failure("Invoice not found.");
            if (invoice.Status == InvoiceStatus.Cancelled)
                return ServiceResult.Failure("Cancelled invoices cannot be marked as paid.");

            // Normalize amount to 2 decimals and validate
            amount = Math.Round(amount, 2);
            if (amount <= 0) return ServiceResult.Failure("Enter a valid amount greater than zero.");

            var balance = invoice.Total - invoice.PaidAmount;
            if (amount > balance) return ServiceResult.Failure("Amount cannot exceed the balance due.");

            var newPaidAmount = invoice.PaidAmount + amount;
            var newStatus = newPaidAmount >= invoice.Total
                ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;

            // ✅ Single targeted Dapper UPDATE — no EF entity tracking, no cascade side-effects
            await _invoiceRepo.UpdatePaidAmountAsync(id, newPaidAmount, newStatus, updatedBy, ct);

            return ServiceResult.Success("Payment recorded successfully.");
        }



        public async Task<ServiceResult> CancelAsync(Guid id, Guid updatedBy, CancellationToken ct = default)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id, ct);
            if (invoice is null) return ServiceResult.Failure("Invoice not found.");
            if (invoice.Status == InvoiceStatus.Paid)
                return ServiceResult.Failure("Paid invoices cannot be cancelled.");
            await _invoiceRepo.UpdateStatusAsync(id, InvoiceStatus.Cancelled, updatedBy, ct);
            return ServiceResult.Success("Invoice cancelled.");
        }

        public Task<ServiceResult<byte[]>> GeneratePdfAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(ServiceResult<byte[]>.Failure("PDF generation is implemented in Step 14 (email templates)."));

        private static string GetStatusBadge(InvoiceStatus s) => s switch
        {
            InvoiceStatus.Draft => "bg-secondary",
            InvoiceStatus.Sent => "bg-primary",
            InvoiceStatus.Paid => "bg-success",
            InvoiceStatus.Overdue => "bg-danger",
            InvoiceStatus.Cancelled => "bg-dark",
            InvoiceStatus.PartiallyPaid => "bg-warning text-dark",
            _ => "bg-secondary"
        };

        private static InvoiceDto MapToDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            CompanyId = i.CompanyId,
            CompanyName = i.Company?.Name ?? string.Empty,
            ClientId = i.ClientId,
            ClientName = i.Client?.Name ?? string.Empty,
            ClientEmail = i.Client?.Email,
            IssueDate = i.IssueDate,
            DueDate = i.DueDate,
            Status = i.Status,
            StatusName = i.Status.ToString(),
            StatusBadge = GetStatusBadge(i.Status),
            SubTotal = i.SubTotal,
            TaxRate = i.TaxRate,
            TaxAmount = i.TaxAmount,
            Discount = i.Discount,
            Total = i.Total,
            PaidAmount = i.PaidAmount,
            BalanceDue = i.Total - i.PaidAmount,
            Notes = i.Notes,
            Terms = i.Terms,
            SentAt = i.SentAt,
            PaidAt = i.PaidAt,
            CreatedAt = i.CreatedAt,
            //CurrencyCode = i.Company?.CurrencyCode ?? "USD",
            CurrencyCode = i.CurrencyCode,
            ExchangeRate = i.ExchangeRate,
            BaseAmount = i.BaseAmount,
            LastEditRemark = i.LastEditRemark,
            CompanyLogo = i.Company?.Logo,
            Items = i.InvoiceItems?.OrderBy(x => x.SortOrder).Select(it => new InvoiceItemDto
            {
                Id = it.Id,
                Description = it.Description,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                SortOrder = it.SortOrder
            }).ToList() ?? new()
        };

        private static InvoiceListItemDto MapToListDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            ClientName = i.Client?.Name ?? string.Empty,
            ClientId = i.ClientId,
            IssueDate = i.IssueDate,
            DueDate = i.DueDate,
            Status = i.Status,
            StatusName = i.Status.ToString(),
            StatusBadge = GetStatusBadge(i.Status),
            Total = i.Total,
            BalanceDue = i.Total - i.PaidAmount,
            //CurrencyCode = i.Company?.CurrencyCode ?? "USD",
            CurrencyCode = i.CurrencyCode,
            ExchangeRate = i.ExchangeRate,
            BaseAmount = i.BaseAmount,

            IsOverdue = i.IsOverdue,
            CreatedAt = i.CreatedAt
        };


        // ── Logo folder constant — one place to change if folder moves
        private const string LogoFolder = "uploads/logos";

        private string LogoHtml(Domain.Entities.Company? company)
        {
            if (string.IsNullOrEmpty(company?.Logo)) return "";

            try
            {
                var fileName = Path.GetFileName(company.Logo); // 🔥 FIX
                var physicalPath = Path.Combine(_webRootPath, LogoFolder, fileName);

                Console.WriteLine($"PATH: {physicalPath}");

                if (!File.Exists(physicalPath))
                {
                    _logger.LogWarning("Logo not found: {Path}", physicalPath);
                    return "";
                }

                var bytes = File.ReadAllBytes(physicalPath);
                var base64 = Convert.ToBase64String(bytes);

                var ext = Path.GetExtension(fileName).ToLowerInvariant();

                var mimeType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };

                return $"<img src=\"data:{mimeType};base64,{base64}\" " +
                       $"style=\"max-height:55px;max-width:140px;display:block;\" />";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Logo error");
                return "";
            }
        }


        private string AbsoluteUrl(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "";
            if (relativePath.StartsWith("http://") || relativePath.StartsWith("https://"))
                return relativePath;
            // Ensure single slash between base and path
            var path = relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
            return _appBaseUrl + path;
        }



        // ── Email template dispatcher ─────────────────────────────
        // Reads company.InvoiceTemplate and calls the matching builder.
        // Templates: classic | modern | minimal | elegant
        private string BuildInvoiceEmail(Invoice invoice, Domain.Entities.Company? company)
        {
            var tpl = (company?.InvoiceTemplate ?? "classic").ToLower().Trim();
            return tpl switch
            {
                "modern" => BuildEmailModern(invoice, company),
                "minimal" => BuildEmailMinimal(invoice, company),
                "elegant" => BuildEmailElegant(invoice, company),
                _ => BuildEmailClassic(invoice, company)   // "classic" is the default
            };
        }




        // ── Shared items table HTML ───────────────────────────────
        private static string ItemsHtml(Invoice invoice, string tdStyle) =>
       string.Join("", invoice.InvoiceItems.OrderBy(x => x.SortOrder).Select(item =>
           $"<tr>" +
           $"<td style=\"{tdStyle}\">{item.Description}</td>" +
           $"<td style=\"{tdStyle};text-align:right;\">{item.Quantity}</td>" +
           $"<td style=\"{tdStyle};text-align:right;\">{item.UnitPrice:N2}</td>" +
           $"<td style=\"{tdStyle};text-align:right;font-weight:600;\">{item.Amount:N2}</td>" +
           $"</tr>"));



        private static string TotalsHtml(Invoice invoice, Domain.Entities.Company? company, string totalColor)
        {
            var discount = invoice.Discount > 0
                ? $"<tr><td style=\"padding:6px 0;color:#6b7280;\">Discount</td><td align=\"right\" style=\"color:#16a34a;\">-{invoice.Discount:N2}</td></tr>"
                : "";

            var notes = string.IsNullOrEmpty(invoice.Notes) ? ""
                : $"<p style=\"color:#6b7280;font-size:13px;margin-top:28px;\"><strong>Notes:</strong> {invoice.Notes}</p>";

            var terms = string.IsNullOrEmpty(invoice.Terms) ? ""
                : $"<p style='color:#e11d48;margin-top:10px;'><strong>Terms & Conditions:</strong> {invoice.Terms}</p>";

            return
                $"<table width=\"300\" align=\"right\" style=\"margin-top:16px;\">" +
                $"<tr><td style=\"padding:6px 0;color:#6b7280;\">Subtotal</td><td align=\"right\" style=\"color:#374151;\">{invoice.SubTotal:N2}</td></tr>" +
                (invoice.TaxRate > 0 ? $"<tr><td style=\"padding:6px 0;color:#6b7280;\">Tax ({invoice.TaxRate}%)</td><td align=\"right\" style=\"color:#374151;\">{invoice.TaxAmount:N2}</td></tr>" : "") +
                discount +
                $"<tr style=\"border-top:2px solid #e2e8f0;\"><td style=\"padding:10px 0;font-weight:700;color:#111827;font-size:16px;\">Total</td>" +
                $"<td align=\"right\" style=\"font-weight:700;color:{totalColor};font-size:16px;\">{invoice.Total:N2} {company?.CurrencyCode}</td></tr>" +
                $"</table>{notes}{terms}";
        }


        // ════════════════════════════════════════════════════════
        //  TEMPLATE 1: Classic Professional  (indigo header)
        // ════════════════════════════════════════════════════════

        //{LogoHtml(company)}

        private string BuildEmailClassic(Invoice invoice, Domain.Entities.Company? company) =>
            $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 20px;">
            <table width="660" style="background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);">
              <tr><td style="background:#4F46E5;padding:28px 40px;">
                 <img src="https://allupnext.com/wp-content/uploads/2025/04/aun-logo.png"
                    style="max-height:55px;max-width:140px;" />

                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:800;">{company?.Name ?? "Finovexa"}</h1>
                <p style="color:#c7d2fe;margin:6px 0 0;font-size:14px;">Invoice {invoice.InvoiceNumber}</p>
              </td></tr>
              <tr><td style="padding:32px 40px;">
                <table width="100%"><tr>
                  <td><p style="color:#6b7280;margin:0;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;">BILLED TO</p>
                      <p style="color:#111827;font-weight:700;margin:6px 0 2px;font-size:15px;">{invoice.Client?.Name}</p>
                      <p style="color:#6b7280;margin:0;font-size:13px;">{invoice.Client?.Email}</p></td>
                  <td align="right">
                      <p style="color:#6b7280;margin:0;font-size:11px;">Issue: {invoice.IssueDate:dd MMM yyyy}</p>
                      <p style="color:#e11d48;font-weight:700;margin:4px 0 0;font-size:13px;">Due: {invoice.DueDate:dd MMM yyyy}</p>
                  </td>
                </tr></table>
                <table width="100%" style="margin-top:24px;border-collapse:collapse;">
                  <thead><tr style="background:#f8fafc;border-bottom:2px solid #e2e8f0;">
                    <th style="padding:10px 12px;text-align:left;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Description</th>
                    <th style="padding:10px 12px;text-align:right;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Qty</th>
                    <th style="padding:10px 12px;text-align:right;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Price</th>
                    <th style="padding:10px 12px;text-align:right;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Amount</th>
                  </tr></thead>
                  <tbody>{ItemsHtml(invoice, "padding:10px 12px;border-bottom:1px solid #f3f4f6;color:#374151;font-size:13px;")}</tbody>
                </table>
                {TotalsHtml(invoice, company, "#4F46E5")}
              </td></tr>
              <tr><td style="background:#f8fafc;padding:18px 40px;text-align:center;border-top:1px solid #e5e7eb;">
                <p style="color:#94a3b8;font-size:12px;margin:0;">© {DateTime.UtcNow.Year} {company?.Name}. Powered by Finovexa — an AllUpNext product.</p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;


        // ════════════════════════════════════════════════════════
        //  TEMPLATE 2: Modern Bold  (dark/black header, amber accent)
        // ════════════════════════════════════════════════════════
        private string BuildEmailModern(Invoice invoice, Domain.Entities.Company? company) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 20px;">
            <table width="660" style="background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);">
              <tr><td style="background:#111827;padding:28px 40px;">
                <table width="100%"><tr>
               
                  <td>{LogoHtml(company)}<h1 style="color:#fff;margin:0;font-size:22px;font-weight:900;letter-spacing:-.5px;">{company?.Name ?? "Finovexa"}</h1>
                      <p style="color:rgba(255,255,255,.5);margin:4px 0 0;font-size:13px;">{company?.Email ?? ""}</p></td>
                  <td align="right"><p style="color:#F59E0B;font-size:28px;font-weight:900;margin:0;letter-spacing:-1px;">INVOICE</p>
                      <p style="color:rgba(255,255,255,.6);margin:4px 0 0;font-size:13px;">{invoice.InvoiceNumber}</p>
                      <p style="color:rgba(255,255,255,.5);margin:3px 0 0;font-size:12px;">Due: {invoice.DueDate:dd MMM yyyy}</p></td>
                </tr></table>
              </td></tr>
              <tr><td style="padding:28px 40px;">
                <table width="100%" style="background:#f9fafb;border-radius:8px;padding:16px;margin-bottom:24px;">
                  <tr>
                    <td style="padding:0 16px 0 0;"><p style="color:#F59E0B;margin:0;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">BILLED TO</p>
                        <p style="color:#111827;font-weight:700;margin:6px 0 2px;font-size:15px;">{invoice.Client?.Name}</p>
                        <p style="color:#6b7280;margin:0;font-size:13px;">{invoice.Client?.Email}</p></td>
                    <td align="right"><p style="color:#F59E0B;margin:0;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">PERIOD</p>
                        <p style="color:#111827;margin:6px 0 2px;font-size:13px;">Issue: {invoice.IssueDate:dd MMM yyyy}</p>
                        <p style="color:#111827;margin:0;font-size:13px;font-weight:700;">Due: {invoice.DueDate:dd MMM yyyy}</p></td>
                  </tr>
                </table>
                <table width="100%" style="border-collapse:collapse;">
                  <thead><tr style="background:#111827;">
                    <th style="padding:10px 12px;text-align:left;color:#F59E0B;font-size:11px;font-weight:700;text-transform:uppercase;">Description</th>
                    <th style="padding:10px 12px;text-align:right;color:#F59E0B;font-size:11px;font-weight:700;text-transform:uppercase;">Qty</th>
                    <th style="padding:10px 12px;text-align:right;color:#F59E0B;font-size:11px;font-weight:700;text-transform:uppercase;">Price</th>
                    <th style="padding:10px 12px;text-align:right;color:#F59E0B;font-size:11px;font-weight:700;text-transform:uppercase;">Amount</th>
                  </tr></thead>
                  <tbody>{ItemsHtml(invoice, "padding:10px 12px;border-bottom:1px solid #f3f4f6;color:#374151;font-size:13px;")}</tbody>
                </table>
                {TotalsHtml(invoice, company, "#111827")}
              </td></tr>
              <tr><td style="padding:14px 40px;text-align:center;border-top:1px solid #e5e7eb;">
                <p style="color:#94a3b8;font-size:12px;margin:0;">© {DateTime.UtcNow.Year} {company?.Name}. Powered by Finovexa — an AllUpNext product.</p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;

        // ════════════════════════════════════════════════════════
        //  TEMPLATE 3: Minimal Clean  (white, serif, thin lines)
        // ════════════════════════════════════════════════════════
        private string BuildEmailMinimal(Invoice invoice, Domain.Entities.Company? company) => $"""
        <!DOCTYPE html><html><body style="font-family:Georgia,serif;background:#fff;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 20px;">
            <table width="620" style="background:#fff;">
              <tr><td style="padding:0 0 24px;">
           
                {LogoHtml(company)}<h1 style="font-size:22px;font-weight:700;color:#111827;margin:0;letter-spacing:-.5px;">{company?.Name ?? "Finovexa"}</h1>
                <p style="font-size:12px;color:#9ca3af;margin:4px 0 0;font-style:italic;">{company?.Email ?? ""}</p>
              </td></tr>
              <tr><td style="padding:20px 0;border-top:1px solid #111827;border-bottom:1px solid #e5e7eb;">
                <table width="100%"><tr>
                  <td><p style="color:#9ca3af;margin:0;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.12em;">Invoice</p>
                      <p style="color:#111827;font-size:18px;font-weight:700;margin:4px 0 0;">{invoice.InvoiceNumber}</p></td>
                  <td align="right" style="font-size:12px;color:#6b7280;line-height:1.8;">
                      Issued: {invoice.IssueDate:dd MMM yyyy}<br/>
                      Due: <strong style="color:#111827;">{invoice.DueDate:dd MMM yyyy}</strong>
                  </td>
                </tr></table>
              </td></tr>
              <tr><td style="padding:24px 0;">
                <table width="100%"><tr>
                  <td style="padding-right:32px;"><p style="color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;margin:0 0 8px;">Bill To</p>
                      <p style="color:#111827;font-weight:700;font-size:14px;margin:0 0 2px;">{invoice.Client?.Name}</p>
                      <p style="color:#6b7280;font-size:12px;margin:0;">{invoice.Client?.Email}</p></td>
                  <td><p style="color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;margin:0 0 8px;">From</p>
                      <p style="color:#111827;font-weight:700;font-size:14px;margin:0 0 2px;">{company?.Name}</p>
                      <p style="color:#6b7280;font-size:12px;margin:0;">{company?.Email}</p></td>
                </tr></table>
              </td></tr>
              <tr><td>
                <table width="100%" style="border-collapse:collapse;">
                  <thead><tr style="border-bottom:1px solid #e5e7eb;">
                    <th style="padding:8px 0;text-align:left;color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">Description</th>
                    <th style="padding:8px 0;text-align:right;color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">Qty</th>
                    <th style="padding:8px 0;text-align:right;color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">Rate</th>
                    <th style="padding:8px 0;text-align:right;color:#9ca3af;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;">Amount</th>
                  </tr></thead>
                  <tbody>{ItemsHtml(invoice, "padding:10px 0;border-bottom:1px solid #f9fafb;color:#374151;font-size:13px;")}</tbody>
                </table>
                {TotalsHtml(invoice, company, "#111827")}
              </td></tr>
              <tr><td style="padding:28px 0 0;border-top:1px solid #e5e7eb;text-align:center;">
                <p style="color:#9ca3af;font-size:11px;font-style:italic;margin:0;">Payment due by {invoice.DueDate:dd MMM yyyy}. Thank you.</p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;

        // ════════════════════════════════════════════════════════
        //  TEMPLATE 4: Elegant Accent  (teal gradient accent bar)
        // ════════════════════════════════════════════════════════
        private string BuildEmailElegant(Invoice invoice, Domain.Entities.Company? company) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 20px;">
            <table width="660" style="background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);">
              <tr><td style="background:linear-gradient(90deg,#0D9488,#06B6D4);height:5px;font-size:1px;line-height:1px;">&nbsp;</td></tr>
              <tr><td style="padding:28px 40px 16px;">
                <table width="100%"><tr>
               
                  <td>{LogoHtml(company)}<h1 style="color:#0D9488;margin:0;font-size:22px;font-weight:800;">{company?.Name ?? "Finovexa"}</h1>
                      <p style="color:#6b7280;margin:4px 0 0;font-size:12px;">{company?.Email ?? ""}</p></td>
                  <td align="right"><p style="color:#111827;font-size:26px;font-weight:800;margin:0;letter-spacing:-1px;">INVOICE</p>
                      <p style="color:#6b7280;margin:4px 0 0;font-size:12px;">{invoice.InvoiceNumber}</p>
                      <p style="color:#6b7280;margin:2px 0 0;font-size:12px;">Due: {invoice.DueDate:dd MMM yyyy}</p></td>
                </tr></table>
              </td></tr>
              <tr><td style="padding:0 40px 24px;">
                <table width="100%" style="background:#f0fdfa;border-radius:8px;padding:16px;">
                  <tr>
                    <td><p style="color:#0D9488;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;margin:0 0 6px;">Billed To</p>
                        <p style="color:#111827;font-weight:700;font-size:15px;margin:0 0 2px;">{invoice.Client?.Name}</p>
                        <p style="color:#6b7280;font-size:13px;margin:0;">{invoice.Client?.Email}</p></td>
                    <td align="right"><p style="color:#0D9488;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.1em;margin:0 0 6px;">Payment Due</p>
                        <p style="color:#111827;font-weight:800;font-size:18px;margin:0;">{invoice.Total:N2} {company?.CurrencyCode}</p>
                        <p style="color:#6b7280;font-size:12px;margin:2px 0 0;">{invoice.DueDate:dd MMM yyyy}</p></td>
                  </tr>
                </table>
              </td></tr>
              <tr><td style="padding:0 40px 28px;">
                <table width="100%" style="border-collapse:collapse;">
                  <thead><tr style="border-bottom:2px solid #0D9488;">
                    <th style="padding:9px 0;text-align:left;color:#0D9488;font-size:11px;font-weight:700;text-transform:uppercase;">Description</th>
                    <th style="padding:9px 0;text-align:right;color:#0D9488;font-size:11px;font-weight:700;text-transform:uppercase;">Qty</th>
                    <th style="padding:9px 0;text-align:right;color:#0D9488;font-size:11px;font-weight:700;text-transform:uppercase;">Price</th>
                    <th style="padding:9px 0;text-align:right;color:#0D9488;font-size:11px;font-weight:700;text-transform:uppercase;">Amount</th>
                  </tr></thead>
                  <tbody>{ItemsHtml(invoice, "padding:10px 0;border-bottom:1px solid #f0fdfa;color:#374151;font-size:13px;")}</tbody>
                </table>
                {TotalsHtml(invoice, company, "#0D9488")}
              </td></tr>
              <tr><td style="background:linear-gradient(90deg,#0D9488,#06B6D4);height:3px;font-size:1px;line-height:1px;">&nbsp;</td></tr>
              <tr><td style="padding:14px 40px;text-align:center;">
                <p style="color:#94a3b8;font-size:12px;margin:0;">© {DateTime.UtcNow.Year} {company?.Name}. Powered by Finovexa — an AllUpNext product.</p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;

    }
}

