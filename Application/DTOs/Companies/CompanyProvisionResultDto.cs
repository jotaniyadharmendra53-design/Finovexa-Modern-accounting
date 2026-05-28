using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class CompanyProvisionResultDto
    {
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = default!;
        public Guid AdminUserId { get; set; }
        public string AdminEmail { get; set; } = default!;
        public Guid AdminRoleId { get; set; }
        public string FiscalYearLabel { get; set; } = default!;
        public string Message { get; set; } = default!;
    }
}
