using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Notifications.Email;

namespace VertexBPMN.Infrastructure.Notifications;

/// <summary>
/// Sends notifications via SMTP email when enabled. If disabled, SendNotificationsAsync is a no-op.
/// Implements the domain INotificationService (batch oriented).
/// </summary>
public sealed class EmailNotificationService : INotificationService
{
    private readonly EmailNotificationOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IOptions<EmailNotificationOptions> options,
                                    ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email notifications disabled. Skipping batch.");
            return;
        }

        var list = notifications as IList<Notification> ?? notifications.ToList();
        if (list.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.From) ||
            string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("Email notification infrastructure not configured (From/SmtpHost missing). Skipping.");
            return;
        }

        // Use one SMTP client per batch (SmtpClient is not reliably thread-safe but reuse inside single thread is fine).
        using var client = new SmtpClient(_options.SmtpHost, _options.Port)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        foreach (var n in list)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipients = ResolveRecipients(n);
            if (recipients.Length == 0)
            {
                _logger.LogDebug("Skipping notification (no recipients). Category={Category} RecipientId={RecipientId}", n.Category, n.RecipientId);
                continue;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_options.From),
                    Subject = BuildSubject(n),
                    Body = BuildBody(n),
                    IsBodyHtml = false
                };

                foreach (var r in recipients.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(r)) continue;
                    message.To.Add(r);
                }

                await Task.Run(() => client.Send(message), cancellationToken);
                _logger.LogInformation("Email notification sent. Category={Category} RecipientId={RecipientId}", n.Category, n.RecipientId);
            }
            catch (Exception ex)
            {
                // Log and continue (email channel is auxiliary).
                _logger.LogError(ex, "Failed to send email notification Category={Category} RecipientId={RecipientId}", n.Category, n.RecipientId);
            }
        }
    }

    private string[] ResolveRecipients(Notification n)
    {
        // Strategy:
        // 1. If DefaultRecipients configured, use them (broadcast / ops style).
        // 2. Else treat RecipientId as an email if it contains '@'.
        if (_options.DefaultRecipients.Length > 0)
            return _options.DefaultRecipients;

        return n.RecipientId.Contains('@', StringComparison.Ordinal)
            ? new[] { n.RecipientId }
            : Array.Empty<string>();
    }

    private static string BuildSubject(Notification n)
        => $"[{(string.IsNullOrWhiteSpace(n.Category) ? "Notification" : n.Category)}] {n.Message}";

    private static string BuildBody(Notification n) =>
        $"""
        Category: {n.Category ?? "(none)"}
        RecipientId: {n.RecipientId}
        Timestamp: {n.Timestamp:O}

        {n.Message}
        """;
}