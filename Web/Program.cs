//using InvoiceSaaS.Infrastructure;
//using InvoiceSaaS.Infrastructure.Services;

//var builder = WebApplication.CreateBuilder(args);

//// ═══════════════════════════════════════════════════════════
////  1. SERVICE REGISTRATIONS
//// ═══════════════════════════════════════════════════════════

//// MVC with Views
//builder.Services.AddControllersWithViews();
////builder.Services.AddControllersWithViews(options =>
////{
////    // Global anti-forgery filter (CSRF protection on all POST/PUT/DELETE)
////    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
////});

//// Infrastructure layer — registers everything (DB, repos, services, JWT, Email, SMTP, FluentValidation)
//builder.Services.AddInfrastructure(builder.Configuration);

//// Session (for flash messages / toasts)
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
//});

//// Response compression
//builder.Services.AddResponseCompression();

//// CORS (for API clients if needed)
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowSameOrigin", policy =>
//        policy.WithOrigins(builder.Configuration["AppUrl"] ?? "https://localhost:7001")
//              .AllowAnyHeader()
//              .AllowAnyMethod()
//              .AllowCredentials());
//});

//// Logging
//builder.Logging.ClearProviders();
//builder.Logging.AddConsole();
//builder.Logging.AddDebug();

//// ═══════════════════════════════════════════════════════════
////  2. BUILD APPLICATION
//// ═══════════════════════════════════════════════════════════
//var app = builder.Build();

//// ═══════════════════════════════════════════════════════════
////  3. MIDDLEWARE PIPELINE  (ORDER IS CRITICAL)
//// ═══════════════════════════════════════════════════════════

//// 3.1 Global exception handler (must be first)
//app.UseMiddleware<GlobalExceptionMiddleware>();

//if (app.Environment.IsDevelopment())
//{
//    app.UseDeveloperExceptionPage();
//}
//else
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}

//// 3.2 HTTPS redirect
//app.UseHttpsRedirection();

//// 3.3 Static files (wwwroot)
//app.UseStaticFiles(new StaticFileOptions
//{
//    OnPrepareResponse = ctx =>
//    {
//        // Cache static assets 7 days in production
//        if (!app.Environment.IsDevelopment())
//            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
//    }
//});
//builder.Services.AddAntiforgery();

//// 3.4 Response compression
//app.UseResponseCompression();

//// 3.5 Routing
//app.UseRouting();

//// 3.6 CORS
//app.UseCors("AllowSameOrigin");

//// 3.7 Session
//app.UseSession();

//// 3.8 JWT Middleware — MUST be before UseAuthentication
////     Reads HttpOnly cookie → sets HttpContext.User
//app.UseMiddleware<JwtMiddleware>();

//// 3.9 Authentication + Authorization
//app.UseAuthentication();
//app.UseAuthorization();

//// 3.10 Permission middleware — MUST be after UseAuthorization
//app.UseMiddleware<PermissionMiddleware>();

//// 3.11 Tenant middleware — sets CompanyId in HttpContext.Items
//app.UseMiddleware<TenantMiddleware>();

//// ═══════════════════════════════════════════════════════════
////  4. ROUTE CONFIGURATION
//// ═══════════════════════════════════════════════════════════
//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

//// ═══════════════════════════════════════════════════════════
////  5. START APPLICATION
//// ═══════════════════════════════════════════════════════════
//app.Logger.LogInformation("Finovexa starting on {Env}", app.Environment.EnvironmentName);
//app.Run();

using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure;
using InvoiceSaaS.Infrastructure.Email;
using InvoiceSaaS.Infrastructure.Services;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);


// ── Services ──────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    // ASP.NET Core will accept the token from EITHER:
    //   1. Form field:      __RequestVerificationToken  (normal form posts)
    //   2. Request header:  RequestVerificationToken    (jQuery Ajax)
    options.HeaderName = "RequestVerificationToken";
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddResponseCompression();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowSameOrigin", p =>
        p.WithOrigins(builder.Configuration["AppUrl"] ?? "https://localhost:7001")
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHostedService<EmailBackgroundService>();

//builder.Services.AddSingleton(Channel.CreateUnbounded<EmailMessage>());
//builder.Services.AddSingleton<EmailService>();
//builder.Services.AddSingleton<IEmailService, EmailService>();
//builder.Services.AddHostedService<EmailBackgroundService>();

// ── Build ─────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────
//app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseResponseCompression();
app.UseRouting();
app.UseCors("AllowSameOrigin");
app.UseSession();
app.UseMiddleware<JwtMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PermissionMiddleware>();
app.UseMiddleware<TenantMiddleware>();

// ── Routes ────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "superadmin",
    pattern: "SuperAdmin/{controller=Company}/{action=Index}/{id?}");

Console.WriteLine("🚀 APP STARTED");

app.Run();
