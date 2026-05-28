using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Dashboard;
using InvoiceSaaS.Application.DTOs.Invoices;
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
    // ═══════════════════════════════════════════════════════════
    //  DashboardService
    // ═══════════════════════════════════════════════════════════
    public class DashboardService : IDashboardService
    {
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IClientRepository _clientRepo;
        private readonly ICompanyRepository _companyRepo;
        private readonly IUserRepository _userRepo;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(IInvoiceRepository invoiceRepo, IClientRepository clientRepo, ICompanyRepository companyRepo, IUserRepository userRepo,
            ILogger<DashboardService> logger)
        {
            _invoiceRepo = invoiceRepo;
            _clientRepo = clientRepo;
            _companyRepo = companyRepo;
            _userRepo = userRepo;
            _logger = logger;
        }

        public async Task<ServiceResult<DashboardStatsDto>> GetStatsAsync(Guid? companyId, CancellationToken ct = default)
        {
            try
            {
                if (!companyId.HasValue)
                    return ServiceResult<DashboardStatsDto>.Success(new DashboardStatsDto());

                var stats = await _invoiceRepo.GetStatsAsync(companyId.Value, ct);
                var clients = await _clientRepo.GetByCompanyAsync(companyId.Value, null, null, ct);
                var filter = new Domain.Interfaces.InvoiceFilterDto { PageSize = 5, SortDesc = true };
                var recent = await _invoiceRepo.GetByCompanyAsync(companyId.Value, filter, ct);
                var overdueFilter = new Domain.Interfaces.InvoiceFilterDto
                { Status = InvoiceStatus.Overdue, PageSize = 5, SortDesc = true };
                var overdue = await _invoiceRepo.GetByCompanyAsync(companyId.Value, overdueFilter, ct);


                var currencyStats = await _invoiceRepo.GetCurrencyStatsAsync(companyId.Value, ct);
                var rawMonthly = await _invoiceRepo.GetMonthlyRevenueAsync(companyId.Value, ct);

                var monthly = Enumerable.Range(-11, 12).Select(offset =>
                {
                    var date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                                    .AddMonths(offset);
                    var label = date.ToString("MMM yy");
                    var found = rawMonthly.FirstOrDefault(m => m.Year == date.Year && m.Month == date.Month);
                    return new MonthlyRevenueDto { Label = label, Amount = found?.Amount ?? 0 };
                }).ToList();

                var symbols = new Dictionary<string, string>
                {
                    ["INR"] = "₹",
                    ["USD"] = "$",
                    ["EUR"] = "€",
                    ["GBP"] = "£",
                    ["AUD"] = "A$",
                    ["CAD"] = "C$",
                    ["SGD"] = "S$",
                    ["AED"] = "AED",
                    ["JPY"] = "¥",
                    ["CNY"] = "¥"
                };



                return ServiceResult<DashboardStatsDto>.Success(new DashboardStatsDto
                {
                    TotalInvoices = stats.TotalInvoices,
                    DraftCount = stats.DraftCount,
                    SentCount = stats.SentCount,
                    PaidCount = stats.PaidCount,
                    OverdueCount = stats.OverdueCount,
                    TotalRevenue = stats.TotalRevenue,
                    PendingAmount = stats.PendingAmount,
                    OverdueAmount = stats.OverdueAmount,
                    ThisMonthTotal = stats.ThisMonthTotal,
                    TotalClients = clients.Count(),
                    RecentInvoices = recent.Select(MapToListDto).ToList(),
                    OverdueInvoices = overdue.Select(MapToListDto).ToList(),
                    MonthlyRevenue = monthly,

                    // ✅ ADD THIS BLOCK
                    CurrencyBreakdown = currencyStats.Select(r => new CurrencyRevenueDto
                    {
                         CurrencyCode = r.CurrencyCode,
                         Revenue = r.Revenue,
                         Pending = r.Pending,
                         BaseRevenue = r.BaseRevenue,
                         InvoiceCount = r.InvoiceCount,
                         CurrencySymbol = symbols.GetValueOrDefault(r.CurrencyCode, r.CurrencyCode)
                    }).ToList()

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard stats");
                return ServiceResult<DashboardStatsDto>.Failure("Error loading dashboard data.");
            }
        }

        // ── SuperAdmin platform dashboard ─────────────────────
        // Uses existing repo methods — no Dapper context needed.
        public async Task<ServiceResult<SuperAdminStatsDto>> GetSuperAdminStatsAsync(
            CancellationToken ct = default)
        {
            try
            {
                var allCompanies = (await _companyRepo.GetAllAsync(ct)).ToList();
                var allUsers = (await _userRepo.GetAllWithRolesAsync(null, ct)).ToList();
                //var recentCompanies = allCompanies
                //    .OrderByDescending(c => c.CreatedAt)
                //    .Take(10)
                //    .Select(c => new RecentCompanyDto
                //    {
                //        Id = c.Id,
                //        Name = c.Name,
                //        Email = c.Email,
                //        IsActive = c.IsActive,
                //        CreatedAt = c.CreatedAt
                //    }).ToList();
                var recentCompanies = new List<RecentCompanyDto>();
                foreach (var c in allCompanies.OrderByDescending(c => c.CreatedAt).Take(10))
                {
                    string? adminEmail = null;
                    if (c.OwnerId != Guid.Empty)
                    {
                        var owner = await _userRepo.GetByIdAsync(c.OwnerId, ct);
                        adminEmail = owner?.Email;
                    }
                    recentCompanies.Add(new RecentCompanyDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Email = c.Email,
                        AdminEmail = adminEmail,   // ✅ ADD THIS
                        IsActive = c.IsActive,
                        CreatedAt = c.CreatedAt
                    });
                }


                // Platform-wide invoice stats — sum per company
                // We get stats for each company then aggregate.
                // (Avoids needing Dapper access in Application layer.)
                int totalInvoices = 0;
                decimal totalRevenue = 0;
                foreach (var co in allCompanies)
                {
                    try
                    {
                        var s = await _invoiceRepo.GetStatsAsync(co.Id, ct);
                        totalInvoices += s.TotalInvoices;
                        totalRevenue += s.TotalRevenue;
                    }
                    catch { /* skip company on error */ }
                }

                return ServiceResult<SuperAdminStatsDto>.Success(new SuperAdminStatsDto
                {
                    TotalCompanies = allCompanies.Count,
                    ActiveCompanies = allCompanies.Count(c => c.IsActive),
                    TotalUsers = allUsers.Count,
                    TotalInvoices = totalInvoices,
                    TotalRevenue = totalRevenue,
                    RecentCompanies = recentCompanies
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SuperAdmin platform stats");
                return ServiceResult<SuperAdminStatsDto>.Failure("Error loading platform data.");
            }
        }


        private static InvoiceListItemDto MapToListDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            ClientName = i.Client?.Name ?? string.Empty,
            IssueDate = i.IssueDate,
            DueDate = i.DueDate,
            Status = i.Status,
            StatusName = i.Status.ToString(),
            StatusBadge = i.Status switch
            {
                InvoiceStatus.Draft => "bg-secondary",
                InvoiceStatus.Sent => "bg-primary",
                InvoiceStatus.Paid => "bg-success",
                InvoiceStatus.Overdue => "bg-danger",
                InvoiceStatus.Cancelled => "bg-dark",
                InvoiceStatus.PartiallyPaid => "bg-warning text-dark",
                _ => "bg-secondary"
            },
            Total = i.Total,
            BalanceDue = i.Total - i.PaidAmount,
            CurrencyCode = i.Company?.CurrencyCode ?? "USD",
            IsOverdue = i.IsOverdue,
            CreatedAt = i.CreatedAt
        };
    }
}
