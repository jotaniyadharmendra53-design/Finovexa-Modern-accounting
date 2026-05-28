using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = default!;
    }
}
