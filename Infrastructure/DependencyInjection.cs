using FluentValidation;
using InvoiceSaaS.Application.Services.Implementations;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Application.Validators.AuthValidator;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Email;
using InvoiceSaaS.Infrastructure.Identity;
using InvoiceSaaS.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Channels;



namespace InvoiceSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. EF Core DbContext ──────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.CommandTimeout(30)
                          .EnableRetryOnFailure(3)));

        // ── 2. Dapper Context ─────────────────────────────────
        services.AddSingleton<IDapperContext, DapperContext>();

        // ── 3. Repositories ───────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailLogRepository, EmailLogRepository>();

        // ── Accounting Repositories ──────────────────────────────────
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IEstimateRepository, EstimateRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();



        // ── 4. Application Services ───────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IClientService, ClientService>();
        //services.AddScoped<IInvoiceService, InvoiceService>();
        // REPLACE WITH this:
        services.AddScoped<IInvoiceService>(sp =>
            new InvoiceService(
                sp.GetRequiredService<IInvoiceRepository>(),
                sp.GetRequiredService<IClientRepository>(),
                sp.GetRequiredService<ICompanyRepository>(),
                sp.GetRequiredService<IEmailService>(),
                sp.GetRequiredService<ILogger<InvoiceService>>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IFiscalYearService>(),
                sp.GetRequiredService<IWebHostEnvironment>().WebRootPath
            ));
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICompanyService, CompanyService>();

        // ── Accounting Services ──────────────────────────────────────
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IEstimateService, EstimateService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAccountingReportService, AccountingReportService>();

        //-- Fiscal Year Service is used by multiple other services, so register it early (before the controllers)
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();


        services.AddScoped<ICompanyService, CompanyService>();

        //ExchangeRateService
        services.AddHttpClient<IExchangeRateService, ExchangeRateService>();



        // ── 5. Identity / JWT ─────────────────────────────────
        // Configure<T>(IConfigurationSection) is the CORRECT overload — no lambda needed.
        // This requires FrameworkReference in the .csproj (not a NuGet package).
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();

        var jwtSection = configuration.GetSection("JwtSettings");
        var secretKey = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

        // AddAuthentication + AddJwtBearer come from Microsoft.AspNetCore.Authentication.JwtBearer
        // which is included via the FrameworkReference — no separate NuGet needed.
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var cookieToken = ctx.Request.Cookies["jwt-access-token"];
                    if (!string.IsNullOrEmpty(cookieToken))
                        ctx.Token = cookieToken;
                    return Task.CompletedTask;
                },
                OnChallenge = ctx =>
                {
                    if (ctx.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                    {
                        ctx.HandleResponse();
                        ctx.Response.Redirect("/Account/Login");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        // ── 6. Email / SMTP ────────────────────────────────────
        services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));

        var emailChannel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        services.AddSingleton(emailChannel);
        //services.AddScoped<EmailService>();
        //services.AddScoped<IEmailService>(sp => sp.GetRequiredService<EmailService>());
        services.AddSingleton<EmailService>(); 
        services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<EmailService>());

        services.AddHostedService<EmailBackgroundService>();
        services.AddHostedService<OverdueInvoiceService>();

        // ── 7. FluentValidation ───────────────────────────────
        // FIXED TYPO: was "InvoiceSaa" (missing S), now "InvoiceSaaS"
        // LoginDtoValidator is imported via: using InvoiceSaaS.Application.Validators;
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();

        // ── 8. Memory Cache ───────────────────────────────────
        services.AddMemoryCache();

        return services;
    }
}
