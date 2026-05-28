using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface ISaleService
    {
        Task<ServiceResult<IEnumerable<SaleDto>>> GetByCompanyAsync(Guid companyId, SaleFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<SaleDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<SaleDto>> SaveAsync(SaveSaleDto dto, Guid companyId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> RefundAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    }
}
