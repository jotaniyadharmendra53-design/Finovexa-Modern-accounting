using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  UserCompany  (join: Users belong to Companies)
    // ═══════════════════════════════════════════════════════════
    public class UserCompany
    {
        public Guid UserId { get; set; }
        public Guid CompanyId { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = default!;
        public Company Company { get; set; } = default!;
    }
}
