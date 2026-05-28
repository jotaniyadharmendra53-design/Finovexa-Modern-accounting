using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class Sale : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public Guid? ClientId { get; set; }
        public string SaleNumber { get; set; } = default!;
        public DateTime SaleDate { get; set; }   
        public SaleStatus Status { get; set; } = SaleStatus.Draft;
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;
        public Client? Client { get; set; }
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
