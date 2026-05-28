using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class FiscalYear : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public string Label { get; set; } = default!;  // "FY 2026-27"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;
        public bool IsDefault { get; set; } = false;
        public string? Notes { get; set; }
        public DateTime? ClosedAt { get; set; }
        public Guid? ClosedBy { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;

        // ── Computed helpers ──────────────────────────────────────
        public bool IsOpen => Status == FiscalYearStatus.Open;
        public bool IsLocked => Status == FiscalYearStatus.Locked;
        public bool IsFuture => Status == FiscalYearStatus.Future;

        public bool ContainsDate(DateTime date)
            => date.Date >= StartDate.Date && date.Date <= EndDate.Date;

        public string PeriodDisplay
            => $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}";

        // ── Factory — build next FY from this one ─────────────────
        public static FiscalYear CreateNext(FiscalYear current, Guid createdBy)
        {
            var nextStart = current.EndDate.AddDays(1);
            var nextEnd = nextStart.AddMonths(12).AddDays(-1);
            var nextYear = nextStart.Year;
            return new FiscalYear
            {
                CompanyId = current.CompanyId,
                Label = BuildLabel(nextStart),
                StartDate = nextStart,
                EndDate = nextEnd,
                Status = FiscalYearStatus.Future,
                IsDefault = false,
                CreatedBy = createdBy
            };
        }

        public static string BuildLabel(DateTime startDate)
        {
            var endYear = startDate.AddMonths(11).Year;
            return $"FY {startDate.Year}-{endYear % 100:D2}";
        }
    

    public static FiscalYear CreateFirst(Guid companyId, int startMonth, Guid userId)
        {
            var now = DateTime.UtcNow;

            // Calculate start & end date
            var startYear = now.Month >= startMonth ? now.Year : now.Year - 1;

            var startDate = new DateTime(startYear, startMonth, 1);
            var endDate = startDate.AddMonths(12).AddDays(-1);

            var label = $"FY {startDate.Year}-{endDate.Year % 100:D2}";

            return new FiscalYear
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Label = label,
                StartDate = startDate,
                EndDate = endDate,
                Status = FiscalYearStatus.Open,
                IsDefault = true,
                CreatedAt = now,
                CreatedBy = userId
            };
        }

    }
}
