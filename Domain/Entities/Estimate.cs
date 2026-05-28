using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class Estimate : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ClientId { get; set; }
        public string EstimateNumber { get; set; } = default!;
        public EstimateStatus Status { get; set; } = EstimateStatus.Draft;
        public DateTime IssueDate { get; set; }   
        public DateTime ExpiryDate { get; set; }   
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public Guid? ConvertedInvoiceId { get; set; }
        public DateTime? SentAt { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;
        public Client Client { get; set; } = default!;
        public ICollection<EstimateItem> EstimateItems { get; set; } = new List<EstimateItem>();
    }
}
