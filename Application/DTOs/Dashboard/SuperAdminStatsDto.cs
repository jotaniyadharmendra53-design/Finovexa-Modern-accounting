using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Dashboard
{
    public class SuperAdminStatsDto
    {
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int TotalUsers { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }

        // Recent companies for the table
        public List<RecentCompanyDto> RecentCompanies { get; set; } = new();
    }
    public class RecentCompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AdminEmail { get; set; }
    }
}
