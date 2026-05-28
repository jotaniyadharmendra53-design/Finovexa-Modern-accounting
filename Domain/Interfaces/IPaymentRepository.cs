using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Payment>> GetByCompanyAsync(Guid companyId, PaymentFilterDto filter, CancellationToken ct = default);
        Task AddAsync(Payment payment, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default);
        Task<decimal> GetTotalInboundAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default);
        Task<decimal> GetTotalOutboundAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default);
    }
}
