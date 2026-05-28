using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public string? ProfilePicture { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = default!;
        public bool RoleIsSystem { get; set; }
        public Guid? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public IEnumerable<string> Permissions { get; set; } = Enumerable.Empty<string>();
    }
}
