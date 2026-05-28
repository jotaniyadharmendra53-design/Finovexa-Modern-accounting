using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Common
{
    public class SelectItemDto
    {
        public string Value { get; set; } = default!;
        public string Text { get; set; } = default!;

        public string Extra { get; set; } = default!;
    }
}
