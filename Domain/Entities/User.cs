using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  User
    // ═══════════════════════════════════════════════════════════
    public class User : BaseEntity
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public Guid RoleId { get; set; }
        public string? Phone { get; set; }
        public string? ProfilePicture { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public DateTime? LastLoginAt { get; set; }
        //// Indicates that the user must change their password on first login.
        //// Default is false for existing users; provisioning code should set true
        //// for newly created admin users when required.
        //public bool IsFirstLogin { get; set; } = false;

        public bool IsFirstLogin { get; set; } = true;

        // Navigation
        public Role Role { get; set; } = default!;
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public ICollection<PasswordResetToken> ResetTokens { get; set; } = new List<PasswordResetToken>();
    }
}
