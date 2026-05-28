using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        Guid? CompanyId { get; }
        string? Email { get; }
        string? FullName { get; }
        string? RoleName { get; }
        bool IsSuperAdmin { get; }
        bool IsAuthenticated { get; }
        IEnumerable<string> Permissions { get; }

        bool HasPermission(string permissionCode);
    }
}
