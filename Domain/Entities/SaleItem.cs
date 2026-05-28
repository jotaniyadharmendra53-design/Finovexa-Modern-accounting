using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class SaleItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SaleId { get; set; }
        public Guid? ProductId { get; set; }
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Amount { get; set; }
        public int SortOrder { get; set; }

        // Navigation
        public Sale Sale { get; set; } = default!;
        public Product? Product { get; set; }
    }
}
