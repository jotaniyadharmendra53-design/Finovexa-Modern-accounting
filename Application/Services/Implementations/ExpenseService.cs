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
    public class ExpenseService : IExpenseService
    {
        private readonly IExpenseRepository _repo;
        private readonly ILogger<ExpenseService> _log;
        private readonly IFiscalYearService _fyService;
        public ExpenseService(IExpenseRepository repo, ILogger<ExpenseService> log, IFiscalYearService fyService) { _repo = repo; _fyService = fyService; _log = log; }

        public async Task<ServiceResult<IEnumerable<ExpenseDto>>> GetByCompanyAsync(Guid companyId, ExpenseFilterDto filter, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, filter, ct);
            return ServiceResult<IEnumerable<ExpenseDto>>.Success(items.Select(Map));
        }

        public async Task<ServiceResult<ExpenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            return e is null ? ServiceResult<ExpenseDto>.Failure("Expense not found.")
                             : ServiceResult<ExpenseDto>.Success(Map(e));
        }

        public async Task<ServiceResult<IEnumerable<ExpenseDto>>> GetUnpaidByCompanyAsync(
    Guid companyId,
    CancellationToken ct = default)
        {
            try
            {
                var items = await _repo.GetUnpaidByCompanyAsync(companyId, ct);

                return ServiceResult<IEnumerable<ExpenseDto>>.Success(
                    items.Select(Map)
                );
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetUnpaidExpenses");
                return ServiceResult<IEnumerable<ExpenseDto>>
                    .Failure($"Failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<ServiceResult<ExpenseDto>> SaveAsync(SaveExpenseDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {

                // ── Fiscal year lock check ──────────────────────
                var fyLock = await _fyService.GetLockViolationAsync(companyId, dto.ExpenseDate, ct);
                if (fyLock is not null) return ServiceResult<ExpenseDto>.Failure(fyLock);



                var tax = Math.Round(dto.Amount * (dto.TaxRate / 100), 2);
                var total = dto.Amount + tax;

                if (dto.Id.HasValue)
                {
                    var existing = await _repo.GetByIdAsync(dto.Id.Value, ct);
                    if (existing is null) return ServiceResult<ExpenseDto>.Failure("Expense not found.");
                    existing.VendorId = dto.VendorId;
                    existing.Category = dto.Category;
                    existing.Description = dto.Description?.Trim();
                    existing.Amount = dto.Amount;
                    existing.TaxRate = dto.TaxRate;
                    existing.TaxAmount = tax;
                    existing.Total = total;
                    existing.ExpenseDate = dto.ExpenseDate;
                    existing.PaymentMethod = dto.PaymentMethod;
                    existing.Status = (ExpenseStatus)dto.Status;
                    existing.Notes = dto.Notes?.Trim();
                    existing.IsRecurring = dto.IsRecurring;
                    existing.RecurrencePeriod = dto.RecurrencePeriod;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    await _repo.UpdateAsync(existing, ct);
                    return ServiceResult<ExpenseDto>.Success(Map(existing), "Expense updated.");
                }

                var number = await _repo.GetNextNumberAsync(companyId, ct);
                var expense = new Expense
                {
                    CompanyId = companyId,
                    VendorId = dto.VendorId,
                    ExpenseNumber = number,
                    Category = dto.Category,
                    Description = dto.Description?.Trim(),
                    Amount = dto.Amount,
                    TaxRate = dto.TaxRate,
                    TaxAmount = tax,
                    Total = total,
                    ExpenseDate = dto.ExpenseDate,
                    PaymentMethod = dto.PaymentMethod,
                    Status = (ExpenseStatus)dto.Status,
                    Notes = dto.Notes?.Trim(),
                    IsRecurring = dto.IsRecurring,
                    RecurrencePeriod = dto.RecurrencePeriod,
                    CreatedBy = userId
                };
                await _repo.AddAsync(expense, ct);
                return ServiceResult<ExpenseDto>.Success(Map(expense), "Expense recorded.");
            }
            catch (Exception ex) { _log.LogError(ex, "SaveExpense"); return ServiceResult<ExpenseDto>.Failure($"Save failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null) return ServiceResult.Failure("Expense not found.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Expense deleted.");
        }

        private static ExpenseDto Map(Expense e) => new()
        {
            Id = e.Id,
            ExpenseNumber = e.ExpenseNumber,
            VendorId = e.VendorId,
            VendorName = e.Vendor?.Name,
            Category = e.Category,
            Description = e.Description,
            Amount = e.Amount,
            TaxRate = e.TaxRate,
            TaxAmount = e.TaxAmount,
            Total = e.Total,
            ExpenseDate = e.ExpenseDate,
            PaymentMethod = e.PaymentMethod,
            Status = (int)e.Status,
            StatusName = e.Status.ToString(),
            ReceiptUrl = e.ReceiptUrl,
            Notes = e.Notes,
            IsRecurring = e.IsRecurring,
            CreatedAt = e.CreatedAt
        };
    }
}
