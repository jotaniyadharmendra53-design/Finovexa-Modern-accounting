using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class EstimateItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EstimateId { get; set; }
        public Guid? ProductId { get; set; }
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public int SortOrder { get; set; }

        // Navigation
        public Estimate Estimate { get; set; } = default!;
        public Product? Product { get; set; }
    }
}
