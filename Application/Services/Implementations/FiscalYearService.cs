using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.FiscalYear;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Enums;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class FiscalYearService : IFiscalYearService
    {
        private readonly IFiscalYearRepository _repo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IExpenseRepository _expenseRepo;
        private readonly ILogger<FiscalYearService> _log;

        public FiscalYearService(
            IFiscalYearRepository repo,
            IInvoiceRepository invoiceRepo,
            IExpenseRepository expenseRepo,
            ILogger<FiscalYearService> log)
        {
            _repo = repo;
            _invoiceRepo = invoiceRepo;
            _expenseRepo = expenseRepo;
            _log = log;
        }

        // ── GetAllAsync ───────────────────────────────────────────
        public async Task<ServiceResult<IEnumerable<FiscalYearDto>>> GetAllAsync(
       Guid companyId, CancellationToken ct = default)
        {
            try
            {
                var list = await _repo.GetByCompanyAsync(companyId, ct);
                return ServiceResult<IEnumerable<FiscalYearDto>>.Success(list.Select(Map));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetAllFiscalYears");
                return ServiceResult<IEnumerable<FiscalYearDto>>.Failure("Failed to load fiscal years.");
            }
        }

        // ── GetCurrentAsync ───────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> GetCurrentAsync(
            Guid companyId, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetCurrentAsync(companyId, ct);
                if (fy is null)
                    return ServiceResult<FiscalYearDto>.Failure("No active fiscal year found.");
                return ServiceResult<FiscalYearDto>.Success(Map(fy));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetCurrentFiscalYear");
                return ServiceResult<FiscalYearDto>.Failure("Failed to load current fiscal year.");
            }
        }


        // ── GetByIdAsync ──────────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> GetByIdAsync(
            Guid id, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetByIdAsync(id, ct);
                if (fy is null) return ServiceResult<FiscalYearDto>.Failure("Fiscal year not found.");
                return ServiceResult<FiscalYearDto>.Success(Map(fy));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetFiscalYearById");
                return ServiceResult<FiscalYearDto>.Failure("Failed to load fiscal year.");
            }
        }

        // ── GetForDateAsync ───────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto?>> GetForDateAsync(
            Guid companyId, DateTime date, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetForDateAsync(companyId, date, ct);
                return ServiceResult<FiscalYearDto?>.Success(fy is null ? null : Map(fy));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetFiscalYearForDate");
                return ServiceResult<FiscalYearDto?>.Success(null); // non-blocking
            }
        }

        // ── GetLockViolationAsync (used by other services) ────────
        public async Task<string?> GetLockViolationAsync(
            Guid companyId, DateTime transactionDate, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetForDateAsync(companyId, transactionDate, ct);
                if (fy is null) return null;   // no FY found — allow (open period)
                if (fy.IsLocked)
                    return $"The fiscal year {fy.Label} is locked. " +
                           $"You cannot create or edit transactions in a closed period " +
                           $"({fy.PeriodDisplay}).";
                return null;  // safe to proceed
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetLockViolation");
                return null;  // fail-open: don't block transactions on error
            }
        }


        // ── PreCloseCheckAsync ────────────────────────────────────
        public async Task<ServiceResult<FiscalYearCloseCheckDto>> PreCloseCheckAsync(
       Guid fiscalYearId, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetByIdAsync(fiscalYearId, ct);
                if (fy is null)
                    return ServiceResult<FiscalYearCloseCheckDto>.Failure("Fiscal year not found.");

                if (fy.IsLocked)
                    return ServiceResult<FiscalYearCloseCheckDto>.Failure(
                        $"{fy.Label} is already locked.");

                var check = new FiscalYearCloseCheckDto
                {
                    FiscalYearLabel = fy.Label,
                    CanClose = true
                };

                // Check unpaid invoices in this FY
                var fyFilter = new InvoiceFilterDto
                {
                    DateFrom = fy.StartDate,
                    DateTo = fy.EndDate,
                    PageSize = 500
                };
                var invoices = (await _invoiceRepo.GetByCompanyAsync(fy.CompanyId, fyFilter, ct)).ToList();

                check.DraftInvoiceCount = invoices.Count(i => i.Status == InvoiceStatus.Draft);
                check.UnpaidInvoiceCount = invoices.Count(i =>
                    i.Status == InvoiceStatus.Sent ||
                    i.Status == InvoiceStatus.Overdue ||
                    i.Status == InvoiceStatus.PartiallyPaid);

                // Check unpaid expenses in this FY
                var expFilter = new ExpenseFilterDto { PageSize = 500 };
                var allExpenses = await _expenseRepo.GetByCompanyAsync(fy.CompanyId, expFilter, ct);
                check.UnpaidExpenseCount = allExpenses.Count(e =>
                    e.Status == ExpenseStatus.Unpaid &&
                    e.ExpenseDate >= fy.StartDate &&
                    e.ExpenseDate <= fy.EndDate);

                // Blocking issues — prevent close
                if (check.UnpaidInvoiceCount > 0)
                {
                    check.CanClose = false;
                    check.BlockingIssues.Add(
                        $"{check.UnpaidInvoiceCount} invoice(s) are still unpaid. " +
                        "Mark them as paid or write them off before closing.");
                }

                // Warnings — allow close but warn
                if (check.DraftInvoiceCount > 0)
                    check.Warnings.Add(
                        $"{check.DraftInvoiceCount} draft invoice(s) will be discarded.");

                if (check.UnpaidExpenseCount > 0)
                    check.Warnings.Add(
                        $"{check.UnpaidExpenseCount} unpaid expense(s) are outstanding.");

                return ServiceResult<FiscalYearCloseCheckDto>.Success(check);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "PreCloseCheck");
                return ServiceResult<FiscalYearCloseCheckDto>.Failure("Pre-close check failed.");
            }
        }


        // ── OpenFirstAsync ────────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> OpenFirstAsync(
            Guid companyId, int startMonth, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var existing = await _repo.GetByCompanyAsync(companyId, ct);
                if (existing.Any())
                    return ServiceResult<FiscalYearDto>.Failure(
                        "Fiscal years already exist for this company.");

                var fy = FiscalYear.CreateFirst(companyId, startMonth, userId);
                await _repo.AddAsync(fy, ct);
                return ServiceResult<FiscalYearDto>.Success(Map(fy),
                    $"Fiscal year {fy.Label} opened successfully.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OpenFirstFiscalYear");
                return ServiceResult<FiscalYearDto>.Failure("Failed to open fiscal year.");
            }
        }

        // ── OpenNextAsync ─────────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> OpenNextAsync(
            Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var current = await _repo.GetOpenAsync(companyId, ct);
                if (current is null)
                    return ServiceResult<FiscalYearDto>.Failure(
                        "No open fiscal year found to extend from.");

                // Check if next FY already exists
                var nextStart = current.EndDate.AddDays(1);
                var overlap = await _repo.HasOverlapAsync(companyId,
                    nextStart, nextStart.AddMonths(12).AddDays(-1), null, ct);
                if (overlap)
                    return ServiceResult<FiscalYearDto>.Failure(
                        "The next fiscal year already exists.");

                var next = FiscalYear.CreateNext(current, userId);
                await _repo.AddAsync(next, ct);
                return ServiceResult<FiscalYearDto>.Success(Map(next),
                    $"Fiscal year {next.Label} created.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OpenNextFiscalYear");
                return ServiceResult<FiscalYearDto>.Failure("Failed to create next fiscal year.");
            }
        }


        // ── CloseAsync ────────────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> CloseAsync(
            CloseFiscalYearDto dto, Guid userId, CancellationToken ct = default)
        {
            try
            {
                // Run pre-close check
                var check = await PreCloseCheckAsync(dto.FiscalYearId, ct);
                if (!check.Succeeded)
                    return ServiceResult<FiscalYearDto>.Failure(check.Errors);

                if (!check.Data!.CanClose)
                    return ServiceResult<FiscalYearDto>.Failure(
                        check.Data.BlockingIssues.FirstOrDefault()
                        ?? "Cannot close fiscal year — blocking issues exist.");

                var fy = await _repo.GetByIdAsync(dto.FiscalYearId, ct);
                if (fy is null)
                    return ServiceResult<FiscalYearDto>.Failure("Fiscal year not found.");

                // Lock the FY
                fy.Status = FiscalYearStatus.Locked;
                fy.ClosedAt = DateTime.UtcNow;
                fy.ClosedBy = userId;
                fy.IsDefault = false;
                fy.Notes = dto.Notes ?? fy.Notes;
                fy.UpdatedAt = DateTime.UtcNow;
                fy.UpdatedBy = userId;
                await _repo.UpdateAsync(fy, ct);

                // Auto-open next FY if requested
                if (dto.OpenNextYear)
                {
                    var nextStart = fy.EndDate.AddDays(1);
                    var hasNext = await _repo.HasOverlapAsync(fy.CompanyId,
                        nextStart, nextStart.AddMonths(12).AddDays(-1), null, ct);

                    if (!hasNext)
                    {
                        var next = FiscalYear.CreateNext(fy, userId);
                        next.Status = FiscalYearStatus.Open;
                        next.IsDefault = true;
                        await _repo.ClearDefaultAsync(fy.CompanyId, ct);
                        await _repo.AddAsync(next, ct);
                    }
                    else
                    {
                        // Activate the existing next FY
                        var existingNext = await _repo.GetForDateAsync(
                            fy.CompanyId, nextStart, ct);
                        if (existingNext is not null)
                        {
                            await _repo.ClearDefaultAsync(fy.CompanyId, ct);
                            existingNext.Status = FiscalYearStatus.Open;
                            existingNext.IsDefault = true;
                            existingNext.UpdatedAt = DateTime.UtcNow;
                            existingNext.UpdatedBy = userId;
                            await _repo.UpdateAsync(existingNext, ct);
                        }
                    }
                }

                return ServiceResult<FiscalYearDto>.Success(Map(fy),
                    $"{fy.Label} has been locked successfully.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "CloseFiscalYear");
                return ServiceResult<FiscalYearDto>.Failure("Failed to close fiscal year.");
            }
        }


        // ── UpdateNotesAsync ──────────────────────────────────────
        public async Task<ServiceResult<FiscalYearDto>> UpdateNotesAsync(
            UpdateFiscalYearDto dto, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var fy = await _repo.GetByIdAsync(dto.Id, ct);
                if (fy is null) return ServiceResult<FiscalYearDto>.Failure("Fiscal year not found.");

                fy.Notes = dto.Notes;
                fy.UpdatedAt = DateTime.UtcNow;
                fy.UpdatedBy = userId;
                await _repo.UpdateAsync(fy, ct);
                return ServiceResult<FiscalYearDto>.Success(Map(fy), "Notes updated.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "UpdateFiscalYearNotes");
                return ServiceResult<FiscalYearDto>.Failure("Failed to update notes.");
            }
        }

        // ── Mapper ────────────────────────────────────────────────
        private static FiscalYearDto Map(FiscalYear fy) => new()
        {
            Id = fy.Id,
            Label = fy.Label,
            StartDate = fy.StartDate,
            EndDate = fy.EndDate,
            Status = (int)fy.Status,
            StatusName = fy.Status.ToString(),
            IsDefault = fy.IsDefault,
            PeriodDisplay = fy.PeriodDisplay,
            Notes = fy.Notes,
            ClosedAt = fy.ClosedAt,
            CreatedAt = fy.CreatedAt
        };


    }


}
