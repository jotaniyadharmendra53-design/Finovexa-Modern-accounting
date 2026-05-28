using InvoiceSaaS.Domain.Common;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Data
{
    // ═══════════════════════════════════════════════════════════
    //  Dapper Connection Factory
    // ═══════════════════════════════════════════════════════════
    public interface IDapperContext
    {
        IDbConnection CreateConnection();
    }

    public class DapperContext : IDapperContext
    {
        private readonly string _connectionString;

        public DapperContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public IDbConnection CreateConnection()
            => new SqlConnection(_connectionString);
    }

    // ═══════════════════════════════════════════════════════════
    //  Generic Base Repository
    //  CUD  → EF Core DbContext (change tracking, transactions)
    //  Read → Dapper (raw SQL, performance)
    // ═══════════════════════════════════════════════════════════
    public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _db;
        protected readonly IDapperContext _dapper;

        protected BaseRepository(ApplicationDbContext db, IDapperContext dapper)
        {
            _db = db;
            _dapper = dapper;
        }

        // ── Write via EF Core ────────────────────────────────────
        public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        {
            await _db.Set<T>().AddAsync(entity, ct);
            await _db.SaveChangesAsync(ct);
        }

        public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
        {
            _db.Set<T>().Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public virtual async Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            var entity = await _db.Set<T>().FindAsync(new object[] { id }, ct);
            if (entity is null) return;

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = deletedBy;
            await _db.SaveChangesAsync(ct);
        }

        // ── Read via Dapper ──────────────────────────────────────
        public abstract Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
        public abstract Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    }
}
