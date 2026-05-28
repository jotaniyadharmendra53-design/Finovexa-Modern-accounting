using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Enums;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
//using System.Net;
//using System.Net.Mail;
using System.Threading.Channels;

using MailKit.Net.Smtp;
using MailKit.Security;

namespace InvoiceSaaS.Infrastructure.Email;

// ═══════════════════════════════════════════════════════════
//  SMTP Settings (bound from appsettings.json)
// ═══════════════════════════════════════════════════════════
public class SmtpSettings
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FromEmail { get; set; } = default!;
    public string FromName { get; set; } = "Finovexa";
    public bool EnableSsl { get; set; } = true;
}

// ═══════════════════════════════════════════════════════════
//  Email Service
//  - SendAsync  → sends immediately (blocking, use sparingly)
//  - QueueAsync → drops into Channel<EmailMessage> for background processing
// ═══════════════════════════════════════════════════════════
public class EmailService : IEmailService
{
    private readonly Channel<EmailMessage> _queue;
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        Channel<EmailMessage> queue,
        IOptions<SmtpSettings> settings,
        ILogger<EmailService> logger)
    {
        _queue = queue;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Queue for background send ────────────────────────────
    public async Task QueueAsync(EmailMessage message, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(message, ct);
        _logger.LogDebug("Email queued for {To}: {Subject}", message.ToEmail, message.Subject);
    }

    //── Send immediately(sync SMTP) ─────────────────────────
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            var mime = BuildMimeMessage(message);

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            //Console.WriteLine("HOST: " + _settings.Host);
            //Console.WriteLine("PORT: " + _settings.Port);

            await smtp.ConnectAsync(
                _settings.Host,
                _settings.Port
              //  MailKit.Security.SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _settings.Username,
                _settings.Password
            );

            await smtp.SendAsync(mime, ct);
            await smtp.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {To}", message.ToEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email failed");
            throw;
        }
    }


    //internal SmtpClient BuildSmtpClient() => new(_settings.Host, _settings.Port)
    //{
    //    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
    //    EnableSsl = _settings.EnableSsl,
    //    DeliveryMethod = SmtpDeliveryMethod.Network
    //};

    //internal MailMessage BuildMailMessage(EmailMessage msg)
    //{
    //    var mail = new MailMessage
    //    {
    //        From = new MailAddress(_settings.FromEmail, _settings.FromName),
    //        Subject = msg.Subject,
    //        Body = msg.Body,
    //        IsBodyHtml = msg.IsHtml
    //    };
    //    mail.To.Add(new MailAddress(msg.ToEmail, msg.ToName ?? msg.ToEmail));
    //    return mail;
    //}

    internal MimeMessage BuildMimeMessage(EmailMessage msg)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        mime.To.Add(new MailboxAddress(msg.ToName ?? msg.ToEmail, msg.ToEmail));
        mime.Subject = msg.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = msg.IsHtml ? msg.Body : null,
            TextBody = msg.IsHtml ? null : msg.Body
        }.ToMessageBody();
        return mime;
    }


}

// ═══════════════════════════════════════════════════════════
//  Email Background Service
//  Runs as a hosted service, reads from Channel<EmailMessage>
//  and sends emails via SMTP. Logs result to EmailLogs table.
// ═══════════════════════════════════════════════════════════
public class EmailBackgroundService : BackgroundService
{
    private readonly Channel<EmailMessage> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        Channel<EmailMessage> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //{
    //    Console.WriteLine("🔥 Background Service Started");

    //    _logger.LogInformation("Email background service started.");

    //    await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
    //    {
    //        await ProcessEmailAsync(message, stoppingToken);
    //    }

    //    _logger.LogInformation("Email background service stopped.");
    //}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email background service started.");

        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                // ✅ Use CancellationToken.None so SMTP isn't cancelled mid-send
                await ProcessEmailAsync(message, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — not an error
            _logger.LogInformation("Email background service stopping.");
        }
    }

    private async Task ProcessEmailAsync(EmailMessage message, CancellationToken ct)
    {
        Console.WriteLine("📤 Background processing email...");
        Console.WriteLine("📧 Sending to: " + message.ToEmail);


        var log = new EmailLog
        {
            ToEmail = message.ToEmail,
            ToName = message.ToName,
            Subject = message.Subject,
            Body = message.Body,
            IsHtml = message.IsHtml,
            RelatedId = message.RelatedId,
            EmailType = message.EmailType,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            var emailLogRepo = scope.ServiceProvider.GetRequiredService<IEmailLogRepository>();


            Console.WriteLine("🚀 Calling SMTP SendAsync...");

            await emailService.SendAsync(message, ct);

            log.IsSuccess = true;
            log.SentAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Background email sent → {To} | Subject: {Subject} | Type: {Type}",
                message.ToEmail, message.Subject, message.EmailType);

            await emailLogRepo.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            log.IsSuccess = false;
            log.ErrorMessage = ex.Message.Length > 1000
                ? ex.Message[..1000]
                : ex.Message;

            _logger.LogError(ex,
                "Background email FAILED → {To} | Subject: {Subject}",
                message.ToEmail, message.Subject);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailLogRepo = scope.ServiceProvider.GetRequiredService<IEmailLogRepository>();
                await emailLogRepo.AddAsync(log, ct);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Could not log failed email to DB");
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════
//  Overdue Invoice Reminder Service
//  Runs daily at midnight UTC — marks overdue, sends reminders
// ═══════════════════════════════════════════════════════════
public class OverdueInvoiceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueInvoiceService> _logger;

    public OverdueInvoiceService(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueInvoiceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //{
    //    while (!stoppingToken.IsCancellationRequested)
    //    {
    //        // Calculate delay to next midnight UTC
    //        var now = DateTime.UtcNow;
    //        var nextRun = now.Date.AddDays(1);
    //        var delay = nextRun - now;

    //        _logger.LogInformation(
    //            "Overdue checker sleeping until {NextRun} ({Delay:h\\:mm} from now)",
    //            nextRun, delay);

    //        await Task.Delay(delay, stoppingToken);

    //        if (stoppingToken.IsCancellationRequested) break;

    //        await CheckOverdueInvoicesAsync(stoppingToken);
    //    }
    //}

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            var delay = nextRun - now;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // ✅ Clean shutdown
            }

            if (stoppingToken.IsCancellationRequested) break;

            // ✅ Don't pass stoppingToken into the work itself
            await CheckOverdueInvoicesAsync(CancellationToken.None);
        }
    }

    private async Task CheckOverdueInvoicesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var overdueInvoices = await invoiceRepo.GetOverdueAsync(ct);
            var count = 0;

            foreach (var invoice in overdueInvoices)
            {
                // Update status to Overdue
                await invoiceRepo.UpdateStatusAsync(
                    invoice.Id,
                    InvoiceStatus.Overdue,
                    Guid.Empty,   // system action
                    ct);

                // Send reminder if client has email
                if (!string.IsNullOrEmpty(invoice.Client?.Email))
                {
                    await emailService.QueueAsync(new EmailMessage
                    {
                        ToEmail = invoice.Client.Email,
                        ToName = invoice.Client.Name,
                        Subject = $"Payment Overdue: Invoice {invoice.InvoiceNumber}",
                        Body = BuildOverdueEmail(invoice),
                        IsHtml = true,
                        RelatedId = invoice.Id,
                        EmailType = "InvoiceOverdue"
                    }, ct);
                    count++;
                }
            }

            _logger.LogInformation("Overdue check complete. Processed {Count} overdue invoices.", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during overdue invoice check");
        }
    }

    private static string BuildOverdueEmail(Invoice invoice) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 0;">
            <table width="600" style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.1);">
              <tr><td style="background:#DC2626;padding:28px 40px;">
                <h1 style="color:#fff;margin:0;font-size:22px;">⚠ Payment Overdue</h1>
              </td></tr>
              <tr><td style="padding:32px 40px;">
                <p style="color:#374151;">Hi <strong>{invoice.Client?.Name}</strong>,</p>
                <p style="color:#374151;line-height:1.6;">
                  This is a reminder that invoice <strong>{invoice.InvoiceNumber}</strong>
                  was due on <strong>{invoice.DueDate:dd MMM yyyy}</strong> and remains unpaid.
                </p>
                <div style="background:#fef2f2;border:1px solid #fecaca;border-radius:8px;padding:20px;margin:20px 0;">
                  <p style="margin:0;color:#7f1d1d;font-size:20px;font-weight:700;">
                    Amount Due: {invoice.Company?.CurrencyCode ?? "USD"} {invoice.Total - invoice.PaidAmount:N2}
                  </p>
                  <p style="margin:8px 0 0;color:#991b1b;font-size:13px;">
                    Invoice #{invoice.InvoiceNumber} — Due {invoice.DueDate:dd MMM yyyy}
                  </p>
                </div>
                <p style="color:#6b7280;font-size:13px;">cd
                  Please contact us immediately if you have any questions regarding this invoice.
                </p>
              </td></tr>
              <tr><td style="background:#f8fafc;padding:20px 40px;text-align:center;">
                <p style="color:#94a3b8;font-size:12px;margin:0;">
                  © {DateTime.UtcNow.Year} {invoice.Company?.Name}. Powered by Finovexa — an AllUpNext product.
                </p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;
}

