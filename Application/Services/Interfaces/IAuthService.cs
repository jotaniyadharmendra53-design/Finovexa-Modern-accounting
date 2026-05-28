using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto, string ipAddress, CancellationToken ct = default);
        Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default);
        Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
        Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default);
        Task<ServiceResult<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken ct = default);
    }
}
