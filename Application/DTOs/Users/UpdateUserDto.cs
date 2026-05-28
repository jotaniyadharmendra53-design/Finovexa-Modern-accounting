using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Users
{
    public class UpdateUserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public Guid RoleId { get; set; }
        public Guid? CompanyId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
