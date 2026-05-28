using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Common
{
    /// <summary>
    /// Base entity with GUID primary key and full audit trail.
    /// All domain entities inherit from this class.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
