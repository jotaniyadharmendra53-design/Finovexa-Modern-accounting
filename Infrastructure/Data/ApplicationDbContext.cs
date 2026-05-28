using InvoiceSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;


namespace InvoiceSaaS.Infrastructure.Data;

/// <summary>
/// EF Core DbContext — used ONLY for Insert, Update, Delete operations.
/// All SELECT queries are handled by Dapper in repositories.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ── DbSets ───────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();


    // ── Accounting DbSets ────────────────────────────────────────
    public DbSet<Product> Products { get; set; }
    public DbSet<Vendor> Vendors { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<Estimate> Estimates { get; set; }
    public DbSet<EstimateItem> EstimateItems { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<Payment> Payments { get; set; }


    public DbSet<FiscalYear> FiscalYears { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── Permission ───────────────────────────────────────
        mb.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Module).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Module);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Role ─────────────────────────────────────────────
        //mb.Entity<Role>(e =>
        //{
        //    e.ToTable("Roles");
        //    e.HasKey(x => x.Id);
        //    e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
        //    e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        //    e.Property(x => x.Description).HasMaxLength(300);
        //    e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        //    e.HasIndex(x => x.Name).IsUnique();
        //    e.HasIndex(x => x.IsDeleted);
        //    e.HasQueryFilter(x => !x.IsDeleted);
        //});

        mb.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // CompanyId is nullable — NULL means global/system role
            e.Property(x => x.CompanyId).IsRequired(false);

            // Name must be unique per company (NULL companyId = global)
            // Enforced in service layer; no DB unique index here because
            // SQL Server filtered unique indexes on nullable columns
            // are complex — we handle this in NameExistsAsync instead.

            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);

            // Navigation: a role optionally belongs to a company
            //e.HasOne(x => x.Company)
            // .WithMany()
            // .HasForeignKey(x => x.CompanyId)
            // .OnDelete(DeleteBehavior.Restrict)
            // .IsRequired(false);
        });


        // ── RolePermission (composite PK) ───────────────────
        mb.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.Property(x => x.AssignedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(x => x.Role)
             .WithMany(r => r.RolePermissions)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(x => x.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── User ─────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.ProfilePicture).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);

            e.HasOne(x => x.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RefreshToken ─────────────────────────────────────
        mb.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Token).HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedByIp).HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.ExpiresAt);

            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PasswordResetToken ───────────────────────────────
        mb.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("PasswordResetTokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Token).HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.Token).IsUnique();

            e.HasOne(x => x.User)
             .WithMany(u => u.ResetTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Company ──────────────────────────────────────────
        mb.Entity<Company>(e =>
        {
            e.ToTable("Companies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Website).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.State).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(100);
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.Logo).HasMaxLength(500);
            e.Property(x => x.TaxNumber).HasMaxLength(100);
            e.Property(x => x.CurrencyCode).HasMaxLength(3).HasDefaultValue("USD");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.FiscalYearStartMonth).HasDefaultValue(4);
            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);

            e.HasOne(x => x.Owner)
             .WithMany()
             .HasForeignKey(x => x.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── UserCompany (composite PK) ───────────────────────
        mb.Entity<UserCompany>(e =>
        {
            e.ToTable("UserCompanies");
            e.HasKey(x => new { x.UserId, x.CompanyId });
            e.Property(x => x.JoinedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(x => x.User)
             .WithMany(u => u.UserCompanies)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Company)
             .WithMany(c => c.UserCompanies)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Client ───────────────────────────────────────────
        mb.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.State).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(100);
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.TaxNumber).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);

            e.HasOne(x => x.Company)
             .WithMany(c => c.Clients)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Invoice ──────────────────────────────────────────
        mb.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Terms).HasMaxLength(2000);
            e.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxRate).HasColumnType("decimal(5,2)");
            e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Discount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);

            e.HasOne(x => x.Company)
             .WithMany(c => c.Invoices)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Client)
             .WithMany(c => c.Invoices)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InvoiceItem ──────────────────────────────────────
        mb.Entity<InvoiceItem>(e =>
        {
            e.ToTable("InvoiceItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Quantity).HasColumnType("decimal(10,2)");
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.InvoiceId);

            e.HasOne(x => x.Invoice)
             .WithMany(i => i.InvoiceItems)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EmailLog ─────────────────────────────────────────
        mb.Entity<EmailLog>(e =>
        {
            e.ToTable("EmailLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.ToEmail).HasMaxLength(200).IsRequired();
            e.Property(x => x.ToName).HasMaxLength(150);
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            e.Property(x => x.Body).IsRequired();
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);
            e.Property(x => x.EmailType).HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ── InvoiceNumberSequence ─────────────────────────────
        mb.Entity<InvoiceNumberSequence>(e =>
        {
            e.ToTable("InvoiceNumberSequences");
            e.HasKey(x => x.CompanyId);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(x => x.Company)
             .WithOne()
             .HasForeignKey<InvoiceNumberSequence>(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AuditLog ─────────────────────────────────────────
        mb.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.TableName).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });


        // ── Products ─────────────────────────────────────────────────
        mb.Entity<Product>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Type).HasConversion<byte>();
        });

        // ── Vendors ──────────────────────────────────────────────────
        mb.Entity<Vendor>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ── Expenses ─────────────────────────────────────────────────
        mb.Entity<Expense>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Status).HasConversion<byte>();
        });

        // ── Estimates ─────────────────────────────────────────────────
        mb.Entity<Estimate>(e =>
        {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Status).HasConversion<byte>();
        });


        mb.Entity<EstimateItem>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.HasOne(x => x.Estimate).WithMany(x => x.EstimateItems).HasForeignKey(x => x.EstimateId);
        });

        // ── Sales ─────────────────────────────────────────────────────
        mb.Entity<Sale>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Status).HasConversion<byte>();
        });

        mb.Entity<SaleItem>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.HasOne(x => x.Sale).WithMany(x => x.SaleItems).HasForeignKey(x => x.SaleId);
        });

        // ── Payments ──────────────────────────────────────────────────
        mb.Entity<Payment>(e => {
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Direction).HasConversion<byte>();
        });

        // ---- FiscalYear ─────────────────────────────────────────────────

        mb.Entity<FiscalYear>(e => {
            e.ToTable("FiscalYears");
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.Status).HasConversion<byte>();
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Company)
             .WithMany()
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Restrict);
        });


    }


}