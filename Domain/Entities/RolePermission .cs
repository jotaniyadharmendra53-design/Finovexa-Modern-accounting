using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  RolePermission  (join table)
    // ═══════════════════════════════════════════════════════════
    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Guid PermissionId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Guid? AssignedBy { get; set; }

        // Navigation
        public Role Role { get; set; } = default!;
        public Permission Permission { get; set; } = default!;
    }
}
