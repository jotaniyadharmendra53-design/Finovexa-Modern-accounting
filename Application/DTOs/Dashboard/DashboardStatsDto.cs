using InvoiceSaaS.Application.DTOs.Invoices;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Dashboard
{
    public class DashboardStatsDto
    {
        public int TotalInvoices { get; set; }
        public int DraftCount { get; set; }
        public int SentCount { get; set; }
        public int PaidCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal ThisMonthTotal { get; set; }
        public List<CurrencyRevenueDto> CurrencyBreakdown { get; set; } = new();
        public int TotalClients { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<InvoiceListItemDto> RecentInvoices { get; set; } = new();
        public List<InvoiceListItemDto> OverdueInvoices { get; set; } = new();
    }
}
