using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Invoices
{
    public class UpdateInvoiceDto : CreateInvoiceDto
    {
        public Guid Id { get; set; }
        public InvoiceStatus Status { get; set; }

        public string? EditRemark { get; set; }
    }
}
