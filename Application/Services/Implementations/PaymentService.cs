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
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _repo;
        private readonly ILogger<PaymentService> _log;
        private readonly IExpenseRepository _expenseRepo;
        private readonly IInvoiceService _invoiceService;
        private readonly IFiscalYearService _fyService;
        public PaymentService(IPaymentRepository repo, IExpenseRepository expenseRepo, IInvoiceService invoiceService, ILogger<PaymentService> log, IFiscalYearService fyService) { _repo = repo; _expenseRepo = expenseRepo; _fyService = fyService; _log = log; _invoiceService = invoiceService; }

        public async Task<ServiceResult<IEnumerable<PaymentDto>>> GetByCompanyAsync(Guid companyId, PaymentFilterDto filter, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, filter, ct);
            return ServiceResult<IEnumerable<PaymentDto>>.Success(items.Select(Map));
        }

        public async Task<ServiceResult<PaymentDto>> CreateAsync(CreatePaymentDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {

                // ── Fiscal year lock check ──────────────────────
                var fyLock = await _fyService.GetLockViolationAsync(companyId, dto.PaymentDate, ct);
                if (fyLock is not null) return ServiceResult<PaymentDto>.Failure(fyLock);



                var number = await _repo.GetNextNumberAsync(companyId, ct);
                var payment = new Payment
                {
                    CompanyId = companyId,
                    PaymentNumber = number,
                    Direction = (PaymentDirection)dto.Direction,
                    Amount = dto.Amount,
                    PaymentDate = dto.PaymentDate,
                    Method = dto.Method?.Trim(),
                    Reference = dto.Reference?.Trim(),
                    InvoiceId = dto.InvoiceId,
                    ExpenseId = dto.ExpenseId,
                    VendorId = dto.VendorId,
                    ClientId = dto.ClientId,
                    Notes = dto.Notes?.Trim(),
                    CreatedBy = userId
                };
                await _repo.AddAsync(payment, ct);

                if (payment.ExpenseId.HasValue && payment.Direction == PaymentDirection.Outbound)
                {
                    await _expenseRepo.MarkAsPaidAsync(payment.ExpenseId.Value, userId, ct);
                }

                // ── Inbound: apply amount to linked invoice ─────────
                if (payment.InvoiceId.HasValue && payment.Direction == PaymentDirection.Inbound)
                {
                    var markResult = await _invoiceService.MarkAsPaidAsync(
                        payment.InvoiceId.Value, payment.Amount, userId, ct);

                    if (!markResult.Succeeded)
                        _log.LogWarning("Invoice mark-paid failed for {InvoiceId}: {Error}",
                            payment.InvoiceId, markResult.Errors);
                }

                return ServiceResult<PaymentDto>.Success(Map(payment), "Payment recorded.");
            }
            catch (Exception ex)
            {     
                     _log.LogError(ex, "CreatePayment"); 
                     return ServiceResult<PaymentDto>.Failure(
                         $"Save failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var p = await _repo.GetByIdAsync(id, ct);
            if (p is null) return ServiceResult.Failure("Payment not found.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Payment deleted.");
        }

        private static PaymentDto Map(Payment p) => new()
        {
            Id = p.Id,
            PaymentNumber = p.PaymentNumber,
            Direction = (int)p.Direction,
            DirectionName = p.Direction.ToString(),
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Method = p.Method,
            Reference = p.Reference,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt
        };
    }
}
