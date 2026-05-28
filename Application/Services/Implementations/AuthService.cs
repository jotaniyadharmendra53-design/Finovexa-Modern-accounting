using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Auth;
using InvoiceSaaS.Application.DTOs.Users;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IPasswordResetTokenRepository _resetTokenRepo;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly ICompanyRepository _companyRepo;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IPasswordResetTokenRepository resetTokenRepo,
            IJwtService jwtService,
            IEmailService emailService,
            ICompanyRepository companyRepo,
            ILogger<AuthService> logger)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _resetTokenRepo = resetTokenRepo;
            _jwtService = jwtService;
            _emailService = emailService;
            _companyRepo = companyRepo;
            _logger = logger;
        }

        public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto, string ipAddress, CancellationToken ct = default)
        {
            try
            {
                // 1. Find user by email
                var user = await _userRepo.GetByEmailAsync(dto.Email, ct);
                if (user is null)
                    return ServiceResult<LoginResponseDto>.Failure("Invalid email or password.");

                // 2. Check if active
                if (!user.IsActive)
                    return ServiceResult<LoginResponseDto>.Failure("Your account has been deactivated. Please contact support.");

                // 3. Verify password
                if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                    return ServiceResult<LoginResponseDto>.Failure("Invalid email or password.");

                // 4. Get user permissions
                var permissions = await _userRepo.GetUserPermissionsAsync(user.Id, ct);

                // 5. Get user's company
                var company = await _companyRepo.GetByUserAsync(user.Id, ct);
                // 5b. Block login if the company is inactive
                //      //     SuperAdmin has no company (company == null) → always allowed
                     if (company is not null && !company.IsActive)
                          return ServiceResult<LoginResponseDto>.Failure(
                              "Your company account has been deactivated. Please contact your system administrator.");

                // 6. Generate JWT
                var payload = new JwtPayload(
                    UserId: user.Id,
                    Email: user.Email,
                    FullName: user.FullName,
                    RoleName: user.Role?.Name ?? string.Empty,
                    CompanyId: company?.Id,
                    Permissions: permissions
                );
                var accessToken = _jwtService.GenerateAccessToken(payload);
                var refreshToken = _jwtService.GenerateRefreshToken();

                // 7. Save refresh token
                await _refreshTokenRepo.AddAsync(new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiresAt = dto.RememberMe
                                    ? DateTime.UtcNow.AddDays(30)
                                    : DateTime.UtcNow.AddDays(7),
                    CreatedByIp = ipAddress
                }, ct);

                // 8. Update last login
                await _userRepo.UpdateLastLoginAsync(user.Id, ct);

                _logger.LogInformation("User {Email} logged in from {Ip}", dto.Email, ipAddress);

                return ServiceResult<LoginResponseDto>.Success(new LoginResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        RoleId = user.RoleId,
                        RoleName = user.Role?.Name ?? string.Empty,
                        CompanyId = company?.Id,
                        CompanyName = company?.Name,
                        Permissions = permissions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for {Email}", dto.Email);
                //return ServiceResult<LoginResponseDto>.Failure("An error occurred during login. Please try again.");
                return ServiceResult<LoginResponseDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default)
        {
            await _refreshTokenRepo.RevokeAsync(refreshToken, ct);
            return ServiceResult.Success("Logged out successfully.");
        }

        public async Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
        {
            try
            {
                var user = await _userRepo.GetByEmailAsync(dto.Email, ct);

                // Always return success to prevent email enumeration
                if (user is null)
                    return ServiceResult.Success("If that email is registered, a reset link has been sent.");

                // Invalidate any existing tokens
                await _resetTokenRepo.InvalidateAllForUserAsync(user.Id, ct);

                // Generate secure token
                var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                          + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                token = token.Replace("+", "-").Replace("/", "_").Replace("=", "");

                await _resetTokenRepo.AddAsync(new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                }, ct);

                // Queue email (non-blocking)
                await _emailService.QueueAsync(new EmailMessage
                {
                    ToEmail = user.Email,
                    ToName = user.FullName,
                    Subject = "Reset Your Password — Finovexa",
                    Body = BuildPasswordResetEmail(user.FullName, token),
                    IsHtml = true,
                    RelatedId = user.Id,
                    EmailType = "ForgotPassword"
                }, ct);

                _logger.LogInformation("Password reset email queued for {Email}", dto.Email);
                return ServiceResult.Success("If that email is registered, a reset link has been sent.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword failed for {Email}", dto.Email);
                return ServiceResult.Success("If that email is registered, a reset link has been sent.");
            }
        }

        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
        {
            try
            {
                var resetToken = await _resetTokenRepo.GetByTokenAsync(dto.Token, ct);
                if (resetToken is null || !resetToken.IsValid)
                    return ServiceResult.Failure("This password reset link is invalid or has expired. Please request a new one.");

                var user = await _userRepo.GetByIdAsync(resetToken.UserId, ct);
                if (user is null)
                    return ServiceResult.Failure("User not found.");

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepo.UpdateAsync(user, ct);

                // Mark token used & revoke all refresh tokens (force re-login)
                await _resetTokenRepo.MarkUsedAsync(dto.Token, ct);
                await _refreshTokenRepo.RevokeAllForUserAsync(user.Id, ct);

                _logger.LogInformation("Password reset successfully for UserId {Id}", user.Id);
                return ServiceResult.Success("Your password has been reset. You can now log in.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPassword failed for token {Token}", dto.Token[..10]);
                return ServiceResult.Failure("An error occurred. Please try again.");
            }
        }

        public async Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(userId, ct);
                if (user is null)
                    return ServiceResult.Failure("User not found.");

                if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                    return ServiceResult.Failure("Current password is incorrect.");

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = userId;
                await _userRepo.UpdateAsync(user, ct);

                // Revoke all refresh tokens → force re-login on other devices
                await _refreshTokenRepo.RevokeAllForUserAsync(userId, ct);

                return ServiceResult.Success("Password changed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePassword failed for UserId {Id}", userId);
                return ServiceResult.Failure("An error occurred. Please try again.");
            }
        }

        public async Task<ServiceResult<LoginResponseDto>> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken ct = default)
        {
            try
            {
                var token = await _refreshTokenRepo.GetByTokenAsync(refreshToken, ct);
                if (token is null || !token.IsActive)
                    return ServiceResult<LoginResponseDto>.Failure("Invalid or expired refresh token. Please log in again.");

                var user = await _userRepo.GetWithRoleAndPermissionsAsync(token.UserId, ct);
                if (user is null || !user.IsActive)
                    return ServiceResult<LoginResponseDto>.Failure("User not found or deactivated.");

                var permissions = await _userRepo.GetUserPermissionsAsync(user.Id, ct);
                var company = await _companyRepo.GetByUserAsync(user.Id, ct);

                var payload = new JwtPayload(
                    UserId: user.Id,
                    Email: user.Email,
                    FullName: user.FullName,
                    RoleName: user.Role?.Name ?? string.Empty,
                    CompanyId: company?.Id,
                    Permissions: permissions
                );

                var newAccessToken = _jwtService.GenerateAccessToken(payload);
                var newRefreshToken = _jwtService.GenerateRefreshToken();

                // Rotate refresh token
                await _refreshTokenRepo.RevokeAsync(refreshToken, ct);
                await _refreshTokenRepo.AddAsync(new RefreshToken
                {
                    UserId = user.Id,
                    Token = newRefreshToken,
                    ExpiresAt = token.ExpiresAt,  // keep original expiry
                    CreatedByIp = ipAddress
                }, ct);

                return ServiceResult<LoginResponseDto>.Success(new LoginResponseDto
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        RoleId = user.RoleId,
                        RoleName = user.Role?.Name ?? string.Empty,
                        CompanyId = company?.Id,
                        CompanyName = company?.Name,
                        Permissions = permissions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken failed");
                return ServiceResult<LoginResponseDto>.Failure("Token refresh failed. Please log in again.");
            }
        }

        // ── Email Template ────────────────────────────────────────
        private static string BuildPasswordResetEmail(string name, string token)
        {
            var resetUrl = $"{{BASE_URL}}/Account/ResetPassword?token={token}";
            return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family: Arial, sans-serif; background:#f4f4f4; margin:0; padding:0;">
              <table width="100%" cellpadding="0" cellspacing="0">
                <tr><td align="center" style="padding:40px 0;">
                  <table width="600" style="background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.1);">
                    <tr><td style="background:#4F46E5; padding:32px 40px;">
                      <h1 style="color:#fff; margin:0; font-size:24px;">Finovexa</h1>
                    </td></tr>
                    <tr><td style="padding:40px;">
                      <h2 style="color:#1e293b; margin-top:0;">Reset Your Password</h2>
                      <p style="color:#475569; line-height:1.6;">Hi {name},</p>
                      <p style="color:#475569; line-height:1.6;">
                        We received a request to reset your password. Click the button below to set a new password.
                        This link will expire in <strong>24 hours</strong>.
                      </p>
                      <div style="text-align:center; margin:32px 0;">
                        <a href="{resetUrl}" 
                           style="background:#4F46E5; color:#fff; padding:14px 32px; border-radius:6px;
                                  text-decoration:none; font-weight:bold; font-size:16px; display:inline-block;">
                          Reset Password
                        </a>
                      </div>
                      <p style="color:#94a3b8; font-size:13px;">
                        If you did not request this, please ignore this email. Your password will remain unchanged.
                      </p>
                      <p style="color:#94a3b8; font-size:12px; margin-top:24px;">
                        Or copy this link: <a href="{resetUrl}" style="color:#4F46E5;">{resetUrl}</a>
                      </p>
                    </td></tr>
                    <tr><td style="background:#f8fafc; padding:20px 40px; text-align:center;">
                      <p style="color:#94a3b8; font-size:12px; margin:0;">
                        © {DateTime.UtcNow.Year} Finovexa — an AllUpNext product. All rights reserved.
                      </p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        }
    }
}
