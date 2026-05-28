using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Invoices;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<ServiceResult<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<InvoiceListItemDto>>> GetByCompanyAsync(Guid companyId, InvoiceFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<InvoiceDto>> CreateAsync(CreateInvoiceDto dto, Guid companyId, Guid createdBy, CancellationToken ct = default);
        Task<ServiceResult<InvoiceDto>> UpdateAsync(UpdateInvoiceDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<ServiceResult> SendAsync(Guid id, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> MarkAsPaidAsync(Guid id, decimal amount, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> CancelAsync(Guid id, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult<byte[]>> GeneratePdfAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult> WriteOffAsync(Guid id, Guid updatedBy, CancellationToken ct = default);
    }
}
