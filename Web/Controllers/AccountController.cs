using InvoiceSaaS.Application.DTOs.Auth;
using InvoiceSaaS.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AccountController> _logger;

    // Cookie settings
    private const string AccessTokenCookie = "jwt-access-token";
    private const string RefreshTokenCookie = "jwt-refresh-token";

    public AccountController(IAuthService authService, ILogger<AccountController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // ── GET /Account/Login ───────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // Already logged in → go to dashboard
        //if (Request.Cookies.ContainsKey(AccessTokenCookie))
        //    return RedirectToLocal(returnUrl);
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // ── POST /Account/Login  (jQuery Ajax) ──────────────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { success = false, errors });
        }

        var ipAddress = GetIpAddress();
        var result = await _authService.LoginAsync(dto, ipAddress);

        if (!result.Succeeded)
            return Json(new { success = false, message = result.Errors.FirstOrDefault() ?? "Login failed." });

        var tokenData = result.Data!;

        // Set HttpOnly cookie for access token
        SetCookie(AccessTokenCookie, tokenData.AccessToken, dto.RememberMe
            ? DateTime.UtcNow.AddDays(7)
            : (DateTime?)null);

        // Set HttpOnly cookie for refresh token
        SetCookie(RefreshTokenCookie, tokenData.RefreshToken, dto.RememberMe
            ? DateTime.UtcNow.AddDays(30)
            : DateTime.UtcNow.AddDays(7));

        _logger.LogInformation("User {Email} authenticated successfully", dto.Email);

        return Json(new
        {
            success = true,
            message = "Login successful. Redirecting...",
            redirectUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/Dashboard"
        });
    }

    // ── POST /Account/Logout ─────────────────────────────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(refreshToken))
            await _authService.LogoutAsync(refreshToken);

        DeleteCookie(AccessTokenCookie);
        DeleteCookie(RefreshTokenCookie);

        return RedirectToAction("Login");
    }

    // ── GET /Account/ForgotPassword ──────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View();

    // ── POST /Account/ForgotPassword  (jQuery Ajax) ──────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { success = false, errors });
        }

        var result = await _authService.ForgotPasswordAsync(dto);
        // Always return success to prevent email enumeration
        return Json(new
        {
            success = true,
            message = result.Message ?? "If that email is registered, a reset link has been sent."
        });
    }

    // ── GET /Account/ResetPassword?token=xxx ────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Invalid password reset link.";
            return RedirectToAction("Login");
        }
        ViewBag.Token = token;
        return View();
    }

    // ── POST /Account/ResetPassword  (jQuery Ajax) ───────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { success = false, errors });
        }

        var result = await _authService.ResetPasswordAsync(dto);
        return Json(new
        {
            success = result.Succeeded,
            message = result.Succeeded
            ? result.Message
            : result.Errors.FirstOrDefault()
        });
    }

    // ── GET /Account/ChangePassword ──────────────────────────
    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View();

    // ── POST /Account/ChangePassword  (jQuery Ajax) ──────────
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return Json(new { success = false, errors });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Json(new { success = false, message = "Session expired. Please log in again." });

        var result = await _authService.ChangePasswordAsync(userId.Value, dto);
        if (result.Succeeded)
        {
            // Force re-login after password change
            DeleteCookie(AccessTokenCookie);
            DeleteCookie(RefreshTokenCookie);
        }

        return Json(new
        {
            success = result.Succeeded,
            message = result.Succeeded ? result.Message : result.Errors.FirstOrDefault(),
            redirectUrl = result.Succeeded ? "/Account/Login" : null
        });
    }

    // ── POST /Account/RefreshToken ───────────────────────────
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
            return Json(new { success = false, message = "No refresh token." });

        var result = await _authService.RefreshTokenAsync(refreshToken, GetIpAddress());
        if (!result.Succeeded)
        {
            DeleteCookie(AccessTokenCookie);
            DeleteCookie(RefreshTokenCookie);
            return Json(new { success = false, message = "Session expired. Please log in again." });
        }

        SetCookie(AccessTokenCookie, result.Data!.AccessToken, null);
        SetCookie(RefreshTokenCookie, result.Data.RefreshToken, DateTime.UtcNow.AddDays(7));
        return Json(new { success = true });
    }

    // ── GET /Account/AccessDenied ────────────────────────────
    [HttpGet]
    [Authorize]
    public IActionResult AccessDenied() => View();

    // ── Private helpers ──────────────────────────────────────
    private void SetCookie(string name, string value, DateTime? expires)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };
        if (expires.HasValue) opts.Expires = expires.Value;
        Response.Cookies.Append(name, value, opts);
    }

    private void DeleteCookie(string name)
        => Response.Cookies.Delete(name, new CookieOptions { Path = "/", SameSite = SameSiteMode.Strict });

    private string GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : null;
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
}