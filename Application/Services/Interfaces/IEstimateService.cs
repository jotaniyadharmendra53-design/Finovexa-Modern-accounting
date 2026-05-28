using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IEstimateService
    {
        Task<ServiceResult<IEnumerable<EstimateDto>>> GetByCompanyAsync(Guid companyId, EstimateFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<EstimateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<EstimateDto>> SaveAsync(SaveEstimateDto dto, Guid companyId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> SendAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<Guid>> ConvertToInvoiceAsync(Guid estimateId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    }
}
