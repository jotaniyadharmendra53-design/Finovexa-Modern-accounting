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
    public class SaleService : ISaleService
    {
        private readonly ISaleRepository _repo;
        private readonly ILogger<SaleService> _log;
        private readonly IFiscalYearService _fyService;
        public SaleService(ISaleRepository repo, ILogger<SaleService> log, IFiscalYearService fyService) { _repo = repo; _log = log; _fyService = fyService; }

        public async Task<ServiceResult<IEnumerable<SaleDto>>> GetByCompanyAsync(Guid companyId, SaleFilterDto filter, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, filter, ct);
            return ServiceResult<IEnumerable<SaleDto>>.Success(items.Select(MapList));
        }

        public async Task<ServiceResult<SaleDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var s = await _repo.GetWithItemsAsync(id, ct);
            return s is null ? ServiceResult<SaleDto>.Failure("Sale not found.")
                             : ServiceResult<SaleDto>.Success(MapFull(s));
        }

        public async Task<ServiceResult<SaleDto>> SaveAsync(SaveSaleDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                // ── Fiscal year lock check ──────────────────────
                var fyLock = await _fyService.GetLockViolationAsync(companyId, dto.SaleDate, ct);
                if (fyLock is not null) return ServiceResult<SaleDto>.Failure(fyLock);

                var subTotal = dto.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2));
                var taxAmount = Math.Round(subTotal * (dto.TaxRate / 100), 2);
                var total = subTotal + taxAmount - dto.Discount;

                if (dto.Id.HasValue)
                {
                    var existing = await _repo.GetByIdAsync(dto.Id.Value, ct);
                    if (existing is null) return ServiceResult<SaleDto>.Failure("Sale not found.");
                    if (existing.Status != SaleStatus.Draft) return ServiceResult<SaleDto>.Failure("Only draft sales can be edited.");
                    existing.ClientId = dto.ClientId;
                    existing.SaleDate = dto.SaleDate;
                    existing.TaxRate = dto.TaxRate;
                    existing.TaxAmount = taxAmount;
                    existing.SubTotal = subTotal;
                    existing.Discount = dto.Discount;
                    existing.Total = total;
                    existing.PaymentMethod = dto.PaymentMethod;
                    existing.Notes = dto.Notes?.Trim();
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    await _repo.UpdateAsync(existing, ct);
                    await _repo.DeleteItemsBySaleAsync(existing.Id, ct);
                    await _repo.AddItemsAsync(BuildItems(existing.Id, dto.Items), ct);
                    return ServiceResult<SaleDto>.Success(MapFull(await _repo.GetWithItemsAsync(existing.Id, ct)!), "Sale updated.");
                }

                var number = await _repo.GetNextNumberAsync(companyId, ct);
                var sale = new Sale
                {
                    CompanyId = companyId,
                    ClientId = dto.ClientId,
                    SaleNumber = number,
                    SaleDate = dto.SaleDate,
                    Status = SaleStatus.Completed,
                    SubTotal = subTotal,
                    TaxRate = dto.TaxRate,
                    TaxAmount = taxAmount,
                    Discount = dto.Discount,
                    Total = total,
                    PaymentMethod = dto.PaymentMethod,
                    Notes = dto.Notes?.Trim(),
                    CreatedBy = userId
                };
                await _repo.AddAsync(sale, ct);
                await _repo.AddItemsAsync(BuildItems(sale.Id, dto.Items), ct);
                return ServiceResult<SaleDto>.Success(MapFull(await _repo.GetWithItemsAsync(sale.Id, ct)!), "Sale recorded.");
            }
            catch (Exception ex) { _log.LogError(ex, "SaveSale"); return ServiceResult<SaleDto>.Failure($"Save failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> RefundAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var s = await _repo.GetByIdAsync(id, ct);
            if (s is null) return ServiceResult.Failure("Sale not found.");
            if (s.Status == SaleStatus.Refunded) return ServiceResult.Failure("Sale already refunded.");
            await _repo.UpdateStatusAsync(id, SaleStatus.Refunded, userId, ct);
            return ServiceResult.Success("Sale marked as refunded.");
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var s = await _repo.GetByIdAsync(id, ct);
            if (s is null) return ServiceResult.Failure("Sale not found.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Sale deleted.");
        }

        private static IEnumerable<SaleItem> BuildItems(Guid saleId, List<SaleItemDto> dtos) =>
            dtos.Select((d, idx) => new SaleItem
            {
                SaleId = saleId,
                ProductId = d.ProductId,
                Description = d.Description.Trim(),
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                TaxRate = d.TaxRate,
                Amount = Math.Round(d.Quantity * d.UnitPrice, 2),
                SortOrder = idx
            });

        private static SaleDto MapList(Sale s) => new()
        {
            Id = s.Id,
            SaleNumber = s.SaleNumber,
            ClientId = s.ClientId,
            ClientName = s.Client?.Name,
            SaleDate = s.SaleDate,
            Status = (int)s.Status,
            StatusName = s.Status.ToString(),
            Total = s.Total,
            CreatedAt = s.CreatedAt
        };

        private static SaleDto MapFull(Sale s) => new()
        {
            Id = s.Id,
            SaleNumber = s.SaleNumber,
            ClientId = s.ClientId,
            ClientName = s.Client?.Name,
            SaleDate = s.SaleDate,
            Status = (int)s.Status,
            StatusName = s.Status.ToString(),
            SubTotal = s.SubTotal,
            TaxRate = s.TaxRate,
            TaxAmount = s.TaxAmount,
            Discount = s.Discount,
            Total = s.Total,
            PaymentMethod = s.PaymentMethod,
            Notes = s.Notes,
            CurrencyCode = s.Company?.CurrencyCode ?? "USD",
            CreatedAt = s.CreatedAt,
            Items = s.SaleItems.OrderBy(i => i.SortOrder).Select(i => new SaleItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxRate = i.TaxRate,
                SortOrder = i.SortOrder
            }).ToList()
        };
    }

}
