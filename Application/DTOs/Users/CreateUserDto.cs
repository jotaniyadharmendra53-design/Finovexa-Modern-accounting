using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Users
{
    public class CreateUserDto
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string ConfirmPassword { get; set; } = default!;
        public string? Phone { get; set; }
        public Guid RoleId { get; set; }
        public Guid? CompanyId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool SendWelcomeEmail { get; set; } = true;
    }
}
