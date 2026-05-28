using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Services;

// ═══════════════════════════════════════════════════════════
//  JWT Middleware
//  Reads the JWT access token from the HttpOnly cookie,
//  validates it, and sets HttpContext.User so [Authorize]
//  and ClaimsPrincipal work as normal throughout the app.
// ═══════════════════════════════════════════════════════════
public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtSettings _settings;

    public JwtMiddleware(RequestDelegate next, IOptions<JwtSettings> settings)
    {
        _next = next;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Try cookie first, then Authorization header (for API clients)
        var token = context.Request.Cookies["jwt-access-token"]
                 ?? ExtractBearerToken(context.Request.Headers["Authorization"]);

        if (!string.IsNullOrEmpty(token))
        {
            var principal = ValidateToken(token);
            if (principal is not null)
            {
                context.User = principal;
            }
        }

        await _next(context);
    }

    private string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader)) return null;
        return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader[7..]
            : null;
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            return principal;
        }
        catch { return null; }
    }
}

// ═══════════════════════════════════════════════════════════
//  Permission Middleware
//  Checks [HasPermission("...")] attributes on controllers/actions.
//  Must run AFTER JwtMiddleware sets HttpContext.User.
// ═══════════════════════════════════════════════════════════
//public class PermissionMiddleware
//{
//    private readonly RequestDelegate _next;

//    public PermissionMiddleware(RequestDelegate next) => _next = next;

//    public async Task InvokeAsync(HttpContext context)
//    {
//        var endpoint = context.GetEndpoint();
//        if (endpoint is null)
//        {
//            await _next(context);
//            return;
//        }

//        var permAttrs = endpoint.Metadata
//            .GetOrderedMetadata<HasPermissionAttribute>()
//            .ToList();

//        if (!permAttrs.Any())
//        {
//            await _next(context);
//            return;
//        }

//        // Must be authenticated first
//        if (!context.User.Identity?.IsAuthenticated ?? true)
//        {
//            context.Response.StatusCode = 401;
//            if (IsAjaxRequest(context.Request))
//                await context.Response.WriteAsJsonAsync(new { success = false, message = "Unauthorized. Please log in." });
//            else
//                context.Response.Redirect("/Account/Login");
//            return;
//        }

//        // Super Admin bypasses all permission checks
//        var roleName = context.User.FindFirstValue("roleName") ?? string.Empty;
//        if (roleName.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
//        {
//            await _next(context);
//            return;
//        }

//        // Check each required permission (ALL must be present)
//        var userPerms = context.User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
//        foreach (var attr in permAttrs)
//        {
//            if (!userPerms.Contains(attr.Code))
//            {
//                context.Response.StatusCode = 403;
//                if (IsAjaxRequest(context.Request))
//                    await context.Response.WriteAsJsonAsync(new
//                    {
//                        success = false,
//                        message = $"You do not have permission to perform this action. Required: '{attr.Code}'"
//                    });
//                else
//                    context.Response.Redirect("/Home/AccessDenied");
//                return;
//            }
//        }

//        await _next(context);
//    }

//    private static bool IsAjaxRequest(HttpRequest req)
//        => req.Headers["X-Requested-With"] == "XMLHttpRequest"
//        || req.ContentType?.Contains("application/json") == true;
//}


public class PermissionMiddleware
{
    private readonly RequestDelegate _next;

    // Controllers SuperAdmin IS allowed to access.
    // Everything else is company-scoped data → 403 for SuperAdmin.
    private static readonly HashSet<string> _superAdminAllowed = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Dashboard",
        "SuperAdminCompany",  // /SuperAdmin/Company/*
        "Account",            // Login, Logout, ChangePassword
        "Home",               // Error, AccessDenied
    };

    public PermissionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null) { await _next(context); return; }
        //
        // If the endpoint allows anonymous access (e.g. Login page), skip permission checks
        if (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null)
        {
            await _next(context);
            return;
        }
        //
        // Must be authenticated first
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await HandleUnauthorized(context);
            return;
        }

        var roleName = context.User.FindFirstValue("roleName") ?? string.Empty;
        var isSuperAdmin = roleName.Equals("Super Admin", StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin)
        {
            // ── SuperAdmin isolation check ────────────────────
            // Determine the controller name from route data
            var routeValues = context.GetRouteData()?.Values;
            var controllerName = routeValues?["controller"]?.ToString() ?? string.Empty;

            if (!_superAdminAllowed.Contains(controllerName))
            {
                // SuperAdmin tried to access a company-scoped page
                await HandleSuperAdminIsolation(context, controllerName);
                return;
            }

            // SuperAdmin is on an allowed page — skip permission checks
            await _next(context);
            return;
        }

        // ── Regular permission check for company users ────────
        var permAttrs = endpoint.Metadata
            .GetOrderedMetadata<HasPermissionAttribute>()
            .ToList();

        if (!permAttrs.Any()) { await _next(context); return; }

        var userPerms = context.User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var attr in permAttrs)
        {
            if (!userPerms.Contains(attr.Code))
            {
                await HandleForbidden(context, attr.Code);
                return;
            }
        }

        await _next(context);
    }

    private static Task HandleUnauthorized(HttpContext ctx)
    {
        if (IsAjax(ctx.Request))
        {
            ctx.Response.StatusCode = 401;
            return ctx.Response.WriteAsJsonAsync(new
            { success = false, message = "Unauthorized. Please log in." });
        }
        ctx.Response.Redirect("/Account/Login");
        return Task.CompletedTask;
    }

    private static Task HandleForbidden(HttpContext ctx, string permCode)
    {
        ctx.Response.StatusCode = 403;
        if (IsAjax(ctx.Request))
            return ctx.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = $"You do not have permission to perform this action. Required: '{permCode}'"
            });
        ctx.Response.Redirect("/Home/AccessDenied");
        return Task.CompletedTask;
    }

    private static Task HandleSuperAdminIsolation(HttpContext ctx, string controller)
    {
        ctx.Response.StatusCode = 403;
        if (IsAjax(ctx.Request))
            return ctx.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "SuperAdmin cannot access company data. " +
                          "Use the Companies page to manage company accounts."
            });
        // Redirect to SuperAdmin dashboard with a banner message
        ctx.Response.Redirect("/Dashboard?sa_blocked=1");
        return Task.CompletedTask;
    }

    private static bool IsAjax(HttpRequest req)
        => req.Headers["X-Requested-With"] == "XMLHttpRequest"
        || req.ContentType?.Contains("application/json") == true;
}



// ═══════════════════════════════════════════════════════════
//  Tenant Middleware
//  Sets the CompanyId in HttpContext.Items for easy access
//  in controllers without reading claims each time.
// ═══════════════════════════════════════════════════════════
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var companyIdClaim = context.User.FindFirstValue("companyId");
        if (companyIdClaim is not null && Guid.TryParse(companyIdClaim, out var companyId))
        {
            context.Items["CompanyId"] = companyId;
        }
        await _next(context);
    }
}

// ═══════════════════════════════════════════════════════════
//  Global Exception Middleware
// ═══════════════════════════════════════════════════════════
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        Microsoft.Extensions.Logging.ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (isAjax)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "An unexpected error occurred. Please try again."
            });
        }
        else
        {
            context.Response.Redirect("/Home/Error");
        }
    }
}