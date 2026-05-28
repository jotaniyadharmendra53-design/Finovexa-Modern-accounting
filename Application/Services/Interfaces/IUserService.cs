using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IUserService
    {
        Task<ServiceResult<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<UserListItemDto>>> GetAllAsync(Guid? companyId = null, CancellationToken ct = default);
        Task<ServiceResult<UserDto>> CreateAsync(CreateUserDto dto, Guid createdBy, CancellationToken ct = default);
        Task<ServiceResult<UserDto>> UpdateAsync(UpdateUserDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<ServiceResult> ToggleActiveAsync(Guid id, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(CancellationToken ct = default);
       
    }
}
