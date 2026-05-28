using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaveEstimateDto
    {
        public Guid? Id { get; set; }
        public Guid ClientId { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Discount { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public List<EstimateItemDto> Items { get; set; } = new();
    }
}
