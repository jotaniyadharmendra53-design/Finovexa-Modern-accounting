using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class EstimateService : IEstimateService
    {
        private readonly IEstimateRepository _repo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IEmailService _emailService;
        private readonly ICompanyRepository _companyRepo;
        private readonly ILogger<EstimateService> _log;
        private readonly IFiscalYearService _fyService;


        public EstimateService(IEstimateRepository repo, IInvoiceRepository invoiceRepo,
            IEmailService emailService, ICompanyRepository companyRepo, ILogger<EstimateService> log, IFiscalYearService fyService)
        {
            _repo = repo; _invoiceRepo = invoiceRepo;
            _emailService = emailService; _companyRepo = companyRepo; _log = log;
            _fyService = fyService; _log = log;
        }

        public async Task<ServiceResult<IEnumerable<EstimateDto>>> GetByCompanyAsync(Guid companyId, EstimateFilterDto filter, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, filter, ct);
            return ServiceResult<IEnumerable<EstimateDto>>.Success(items.Select(e => MapList(e)));
        }

        public async Task<ServiceResult<EstimateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var e = await _repo.GetWithItemsAsync(id, ct);
            return e is null ? ServiceResult<EstimateDto>.Failure("Estimate not found.")
                             : ServiceResult<EstimateDto>.Success(MapFull(e));
        }

        public async Task<ServiceResult<EstimateDto>> SaveAsync(SaveEstimateDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                // ── Fiscal year lock check ──────────────────────
                var fyLock = await _fyService.GetLockViolationAsync(companyId, dto.IssueDate, ct);
                if (fyLock is not null) return ServiceResult<EstimateDto>.Failure(fyLock);

                var subTotal = dto.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
                var taxAmount = Math.Round(subTotal * (dto.TaxRate / 100), 2);
                var total = subTotal + taxAmount - dto.Discount;

                if (dto.Id.HasValue)
                {
                    var existing = await _repo.GetByIdAsync(dto.Id.Value, ct);
                    if (existing is null) return ServiceResult<EstimateDto>.Failure("Estimate not found.");
                    if (existing.Status != EstimateStatus.Draft)
                        return ServiceResult<EstimateDto>.Failure("Only Draft estimates can be edited.");
                    existing.ClientId = dto.ClientId;
                    existing.IssueDate = dto.IssueDate;
                    existing.ExpiryDate = dto.ExpiryDate;
                    //existing.IssueDate = dto.IssueDate.ToDateTime(TimeOnly.MinValue);
                    //existing.ExpiryDate = dto.ExpiryDate.ToDateTime(TimeOnly.MinValue);
                    existing.TaxRate = dto.TaxRate;
                    existing.TaxAmount = taxAmount;
                    existing.SubTotal = subTotal;
                    existing.Discount = dto.Discount;
                    existing.Total = total;
                    existing.Notes = dto.Notes?.Trim();
                    existing.Terms = dto.Terms?.Trim();
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    await _repo.UpdateAsync(existing, ct);
                    await _repo.DeleteItemsByEstimateAsync(existing.Id, ct);
                    await _repo.AddItemsAsync(BuildItems(existing.Id, dto.Items), ct);
                    return ServiceResult<EstimateDto>.Success(MapFull(await _repo.GetWithItemsAsync(existing.Id, ct)!), "Estimate updated.");
                }

                var number = await _repo.GetNextNumberAsync(companyId, ct);
                var estimate = new Estimate
                {
                    CompanyId = companyId,
                    ClientId = dto.ClientId,
                    EstimateNumber = number,
                    IssueDate = dto.IssueDate,
                    ExpiryDate = dto.ExpiryDate,
                    //IssueDate = dto.IssueDate.ToDateTime(TimeOnly.MinValue),
                    //ExpiryDate = dto.ExpiryDate.ToDateTime(TimeOnly.MinValue),
                    TaxRate = dto.TaxRate,
                    TaxAmount = taxAmount,
                    SubTotal = subTotal,
                    Discount = dto.Discount,
                    Total = total,
                    Notes = dto.Notes?.Trim(),
                    Terms = dto.Terms?.Trim(),
                    CreatedBy = userId
                };
                await _repo.AddAsync(estimate, ct);
                await _repo.AddItemsAsync(BuildItems(estimate.Id, dto.Items), ct);
                return ServiceResult<EstimateDto>.Success(MapFull(await _repo.GetWithItemsAsync(estimate.Id, ct)!), "Estimate created.");
            }
            catch (Exception ex) { _log.LogError(ex, "SaveEstimate"); return ServiceResult<EstimateDto>.Failure($"Save failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> SendAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var estimate = await _repo.GetWithItemsAsync(id, ct);
                if (estimate is null) return ServiceResult.Failure("Estimate not found.");
                if (estimate.Status == EstimateStatus.Invoiced) return ServiceResult.Failure("Estimate already invoiced.");
                if (string.IsNullOrEmpty(estimate.Client?.Email))
                    return ServiceResult.Failure("Client does not have an email address.");

                await _repo.UpdateStatusAsync(id, EstimateStatus.Sent, userId, ct);
                var company = await _companyRepo.GetByIdAsync(estimate.CompanyId, ct);

                await _emailService.QueueAsync(new EmailMessage
                {
                    ToEmail = estimate.Client.Email,
                    ToName = estimate.Client.Name,
                    Subject = $"Estimate {estimate.EstimateNumber} from {company?.Name}",
                    Body = BuildEstimateEmail(estimate, company),
                    IsHtml = true,
                    RelatedId = estimate.Id,
                    EmailType = "EstimateSent"
                }, ct);

                return ServiceResult.Success($"Estimate {estimate.EstimateNumber} sent to {estimate.Client.Email}.");
            }
            catch (Exception ex) { _log.LogError(ex, "SendEstimate"); return ServiceResult.Failure($"Send failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult<Guid>> ConvertToInvoiceAsync(Guid estimateId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var estimate = await _repo.GetWithItemsAsync(estimateId, ct);
                if (estimate is null) return ServiceResult<Guid>.Failure("Estimate not found.");
                if (estimate.Status == EstimateStatus.Invoiced)
                    return ServiceResult<Guid>.Failure("This estimate has already been converted to an invoice.");
                if (estimate.Status == EstimateStatus.Declined)
                    return ServiceResult<Guid>.Failure("Declined estimates cannot be converted.");

                // Get next invoice number
                var invoiceNumber = await _invoiceRepo.GetNextInvoiceNumberAsync(estimate.CompanyId, ct);

                // Create invoice
                var invoice = new Invoice
                {
                    CompanyId = estimate.CompanyId,
                    ClientId = estimate.ClientId,
                    InvoiceNumber = invoiceNumber,
                    IssueDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    Status = Domain.Enums.InvoiceStatus.Draft,
                    SubTotal = estimate.SubTotal,
                    TaxRate = estimate.TaxRate,
                    TaxAmount = estimate.TaxAmount,
                    Discount = estimate.Discount,
                    Total = estimate.Total,
                    Notes = estimate.Notes,
                    Terms = estimate.Terms,
                    CreatedBy = userId
                };
                await _invoiceRepo.AddAsync(invoice, ct);

                // Copy estimate items to invoice items
                var invoiceItems = estimate.EstimateItems.Select((item, idx) => new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount,
                    SortOrder = idx
                });
                await _invoiceRepo.AddItemsAsync(invoiceItems, ct);

                // Mark estimate as Invoiced
                await _repo.UpdateStatusAsync(estimateId, EstimateStatus.Invoiced, userId, ct);

                return ServiceResult<Guid>.Success(invoice.Id, $"Invoice {invoiceNumber} created from estimate {estimate.EstimateNumber}.");
            }
            catch (Exception ex) { _log.LogError(ex, "ConvertEstimate"); return ServiceResult<Guid>.Failure($"Convert failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null) return ServiceResult.Failure("Estimate not found.");
            if (e.Status == EstimateStatus.Invoiced) return ServiceResult.Failure("Invoiced estimates cannot be deleted.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Estimate deleted.");
        }

        private static IEnumerable<EstimateItem> BuildItems(Guid estimateId, List<EstimateItemDto> dtos) =>
            dtos.Select((d, idx) => new EstimateItem
            {
                EstimateId = estimateId,
                ProductId = d.ProductId,
                Description = d.Description.Trim(),
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                Amount = Math.Round(d.Quantity * d.UnitPrice, 2),
                SortOrder = idx
            });

        private static EstimateDto MapList(Estimate e) => new()
        {
            Id = e.Id,
            EstimateNumber = e.EstimateNumber,
            ClientId = e.ClientId,
            ClientName = e.Client?.Name ?? string.Empty,
            Status = (int)e.Status,
            StatusName = e.Status.ToString(),
            IssueDate = e.IssueDate,
            ExpiryDate = e.ExpiryDate,
            Total = e.Total,
            CreatedAt = e.CreatedAt,
            ConvertedInvoiceId = e.ConvertedInvoiceId
        };

        private static EstimateDto MapFull(Estimate e) => new()
        {
            Id = e.Id,
            EstimateNumber = e.EstimateNumber,
            ClientId = e.ClientId,
            ClientName = e.Client?.Name ?? string.Empty,
            ClientEmail = e.Client?.Email,
            CompanyName = e.Company?.Name ?? string.Empty,
            CurrencyCode = e.Company?.CurrencyCode ?? "USD",
            Status = (int)e.Status,
            StatusName = e.Status.ToString(),
            IssueDate = e.IssueDate,
            ExpiryDate = e.ExpiryDate,
            SubTotal = e.SubTotal,
            TaxRate = e.TaxRate,
            TaxAmount = e.TaxAmount,
            Discount = e.Discount,
            Total = e.Total,
            Notes = e.Notes,
            Terms = e.Terms,
            ConvertedInvoiceId = e.ConvertedInvoiceId,
            SentAt = e.SentAt,
            CreatedAt = e.CreatedAt,
            Items = e.EstimateItems.OrderBy(i => i.SortOrder).Select(i => new EstimateItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SortOrder = i.SortOrder
            }).ToList()
        };

        private static string BuildEstimateEmail(Estimate estimate, Company? company)
        {
            var items = string.Join("", estimate.EstimateItems.OrderBy(x => x.SortOrder).Select(i =>
                $"<tr><td style='padding:10px 12px;border-bottom:1px solid #e2e8f0;'>{i.Description}</td>" +
                $"<td style='padding:10px 12px;text-align:right;border-bottom:1px solid #e2e8f0;'>{i.Quantity}</td>" +
                $"<td style='padding:10px 12px;text-align:right;border-bottom:1px solid #e2e8f0;font-weight:600;'>{i.Amount:N2}</td></tr>"));
            return $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:40px 20px;">
        <table width="660" style="background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);">
          <tr><td style="background:#0F6E56;padding:28px 40px;">
            <h1 style="color:#fff;margin:0;font-size:22px;font-weight:800;">{company?.Name ?? "Finovexa"}</h1>
            <p style="color:#9FE1CB;margin:6px 0 0;font-size:14px;">Estimate {estimate.EstimateNumber}</p>
          </td></tr>
          <tr><td style="padding:28px 40px;">
            <p style="color:#374151;">Dear <strong>{estimate.Client?.Name}</strong>,</p>
            <p style="color:#6b7280;font-size:13px;margin-bottom:20px;">
              Please find your estimate below. Valid until <strong>{estimate.ExpiryDate:dd MMM yyyy}</strong>.
            </p>
            <table width="100%" style="border-collapse:collapse;">
              <thead><tr style="background:#f8fafc;"><th style="padding:10px 12px;text-align:left;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;border-bottom:2px solid #0F6E56;">Description</th>
                <th style="padding:10px 12px;text-align:right;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Qty</th>
                <th style="padding:10px 12px;text-align:right;color:#6b7280;font-size:11px;font-weight:700;text-transform:uppercase;">Amount</th></tr></thead>
              <tbody>{items}</tbody>
            </table>
            <table width="260" align="right" style="margin-top:16px;">
              <tr><td style="padding:6px 0;color:#6b7280;">Subtotal</td><td align="right">{estimate.SubTotal:N2}</td></tr>
              {(estimate.TaxRate > 0 ? $"<tr><td style='padding:6px 0;color:#6b7280;'>Tax ({estimate.TaxRate}%)</td><td align='right'>{estimate.TaxAmount:N2}</td></tr>" : "")}
              {(estimate.Discount > 0 ? $"<tr><td style='padding:6px 0;color:#6b7280;'>Discount</td><td align='right' style='color:#16a34a;'>-{estimate.Discount:N2}</td></tr>" : "")}
              <tr style="border-top:2px solid #0F6E56;"><td style="padding:10px 0;font-weight:700;font-size:16px;">Total</td>
                <td align="right" style="font-weight:700;color:#0F6E56;font-size:16px;">{estimate.Total:N2} {company?.CurrencyCode}</td></tr>
            </table>
          </td></tr>
          <tr><td style="background:#f8fafc;padding:18px 40px;text-align:center;">
            <p style="color:#94a3b8;font-size:12px;margin:0;">© {DateTime.UtcNow.Year} {company?.Name}. Powered by Finovexa — an AllUpNext product.</p>
          </td></tr>
        </table></td></tr></table></body></html>
        """;
        }
    }

}
