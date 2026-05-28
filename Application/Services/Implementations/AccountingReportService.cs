using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Accounting;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class AccountingReportService : IAccountingReportService
    {
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IExpenseRepository _expenseRepo;
        private readonly ISaleRepository _saleRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly ILogger<AccountingReportService> _log;

        public AccountingReportService(IInvoiceRepository invoiceRepo, IExpenseRepository expenseRepo,
            ISaleRepository saleRepo, IPaymentRepository paymentRepo, ILogger<AccountingReportService> log)
        {
            _invoiceRepo = invoiceRepo; _expenseRepo = expenseRepo;
            _saleRepo = saleRepo; _paymentRepo = paymentRepo; _log = log;
        }

        public async Task<ServiceResult<PnLReportDto>> GetPnLAsync(Guid companyId, AccountingFilterDto filter, CancellationToken ct = default)
        {
            try
            {
                var (from, to) = GetDateRange(filter);
             
                var invoiceRevenue = await _invoiceRepo.GetStatsAsync(companyId, ct);
                var saleRevenue = await _saleRepo.GetTotalByCompanyAsync(companyId, from, to, ct);
            
                var totalRevenue = invoiceRevenue.TotalRevenue + saleRevenue;

                // Expenses
                var totalExpenses = await _expenseRepo.GetTotalByCompanyAsync(companyId, from, to, ct);

                var report = new PnLReportDto
                {
                    TotalRevenue = totalRevenue,
                    TotalExpenses = totalExpenses,
                    GrossProfit = totalRevenue - totalExpenses,
                    NetProfit = totalRevenue - totalExpenses,
                    PeriodLabel = filter.Month.HasValue
                        ? $"{new DateTime(filter.Year, filter.Month.Value, 1):MMMM yyyy}"
                        : filter.Year.ToString(),
                    RevenueLines = new List<PnLLineDto>
                {
                    new() { Label = "Invoice payments received", Amount = invoiceRevenue.TotalRevenue },
                    new() { Label = "Direct sales",              Amount = saleRevenue }
                },
                    ExpenseLines = new List<PnLLineDto>
                {
                    new() { Label = "Total expenses", Amount = totalExpenses }
                }
                };
                return ServiceResult<PnLReportDto>.Success(report);
            }
            catch (Exception ex) { _log.LogError(ex, "GetPnL"); return ServiceResult<PnLReportDto>.Failure($"Report error: {ex.Message}"); }
        }

        public async Task<ServiceResult<CashFlowDto>> GetCashFlowAsync(Guid companyId, AccountingFilterDto filter, CancellationToken ct = default)
        {
            try
            {
                var (from, to) = GetDateRange(filter);
          

                var inbound = await _paymentRepo.GetTotalInboundAsync(companyId, from, to, ct);
                var outbound = await _paymentRepo.GetTotalOutboundAsync(companyId, from, to, ct);
                var payments = await _paymentRepo.GetByCompanyAsync(companyId, new PaymentFilterDto { DateFrom = from, DateTo = to, PageSize = 200 }, ct);
            

                decimal balance = 0;
                var rows = payments.OrderBy(p => p.PaymentDate).Select(p => {
                    if (p.Direction == PaymentDirection.Inbound) balance += p.Amount;
                    else balance -= p.Amount;
                    return new CashFlowRowDto
                    {
                        Date = p.PaymentDate.ToString("dd MMM yyyy"),
                        Reference = p.PaymentNumber,
                        Type = p.Direction.ToString(),
                        Inbound = p.Direction == PaymentDirection.Inbound ? p.Amount : 0,
                        Outbound = p.Direction == PaymentDirection.Outbound ? p.Amount : 0,
                        Balance = balance
                    };
                }).ToList();

                return ServiceResult<CashFlowDto>.Success(new CashFlowDto
                {
                    OpeningBalance = 0,
                    TotalInbound = inbound,
                    TotalOutbound = outbound,
                    ClosingBalance = inbound - outbound,
                    Rows = rows
                });
            }
            catch (Exception ex) { _log.LogError(ex, "GetCashFlow"); return ServiceResult<CashFlowDto>.Failure($"Report error: {ex.Message}"); }
        }

        private static (DateTime from, DateTime to) GetDateRange(AccountingFilterDto f)
        {
            if (f.DateFrom.HasValue && f.DateTo.HasValue)
                return (f.DateFrom.Value, f.DateTo.Value);
            if (f.Month.HasValue)
            {
                var start = new DateTime(f.Year, f.Month.Value, 1);
                return (start, start.AddMonths(1).AddDays(-1));
            }
            return (new DateTime(f.Year, 1, 1), new DateTime(f.Year, 12, 31));
        }


        // ── Accounts Receivable Aging ─────────────────────────
        public async Task<ServiceResult<IEnumerable<AgingReportDto>>> GetReceivablesAgingAsync(
            Guid companyId, CancellationToken ct = default)
        {
            try
            {
                var filter = new InvoiceFilterDto { PageSize = 500 };
                var invoices = await _invoiceRepo.GetByCompanyAsync(companyId, filter, ct);
                var today = DateTime.Today;

                var rows = invoices
                    .Where(i => i.BalanceDue > 0)
                    .GroupBy(i => i.Client?.Name ?? "Unknown")
                    .Select(g => new AgingReportDto
                    {
                        Name = g.Key,
                        Current = g.Where(i => (today - i.DueDate).TotalDays <= 0)
                                     .Sum(i => i.BalanceDue),
                        Days1_30 = g.Where(i => (today - i.DueDate).TotalDays is > 0 and <= 30)
                                     .Sum(i => i.BalanceDue),
                        Days31_60 = g.Where(i => (today - i.DueDate).TotalDays is > 30 and <= 60)
                                     .Sum(i => i.BalanceDue),
                        Over60 = g.Where(i => (today - i.DueDate).TotalDays > 60)
                                     .Sum(i => i.BalanceDue),
                        Total = g.Sum(i => i.BalanceDue)
                    })
                    .Where(r => r.Total > 0)
                    .OrderByDescending(r => r.Total)
                    .ToList();

                return ServiceResult<IEnumerable<AgingReportDto>>.Success(rows);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetReceivablesAging");
                return ServiceResult<IEnumerable<AgingReportDto>>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<ServiceResult<IEnumerable<AgingReportDto>>> GetPayablesAgingAsync(
           Guid companyId, CancellationToken ct = default)
        {
            try
            {
                var filter = new ExpenseFilterDto { PageSize = 500 };
                var expenses = await _expenseRepo.GetByCompanyAsync(companyId, filter, ct);
                var today = DateTime.Today;

                var rows = expenses
                    .Where(e => e.Status == ExpenseStatus.Unpaid)
                    .GroupBy(e => e.Vendor?.Name ?? "(No vendor)")
                    .Select(g => new AgingReportDto
                    {
                        Name = g.Key,
                        Current = g.Where(e => (today - e.ExpenseDate).TotalDays <= 30)
                                     .Sum(e => e.Total),
                        Days1_30 = g.Where(e => (today - e.ExpenseDate).TotalDays is > 30 and <= 60)
                                     .Sum(e => e.Total),
                        Days31_60 = g.Where(e => (today - e.ExpenseDate).TotalDays is > 60 and <= 90)
                                     .Sum(e => e.Total),
                        Over60 = g.Where(e => (today - e.ExpenseDate).TotalDays > 90)
                                     .Sum(e => e.Total),
                        Total = g.Sum(e => e.Total)
                    })
                    .Where(r => r.Total > 0)
                    .OrderByDescending(r => r.Total)
                    .ToList();

                return ServiceResult<IEnumerable<AgingReportDto>>.Success(rows);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetPayablesAging");
                return ServiceResult<IEnumerable<AgingReportDto>>.Failure($"Error: {ex.Message}");
            }
        }


    }

}
