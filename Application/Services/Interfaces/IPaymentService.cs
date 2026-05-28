using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResult<IEnumerable<PaymentDto>>> GetByCompanyAsync(Guid companyId, PaymentFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<PaymentDto>> CreateAsync(CreatePaymentDto dto, Guid companyId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    }
}
