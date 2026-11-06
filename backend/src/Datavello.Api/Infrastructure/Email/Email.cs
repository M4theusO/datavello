using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Datavello.Api.Infrastructure.Email;

/// <summary>SMTP settings ("Email" section). In development they point to Mailpit (localhost:1025).</summary>
public sealed class EmailSettings
{
    public const string Section = "Email";

    /// <summary>SMTP host. When empty, e-mails are not sent (a warning is logged instead).</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpEnableSsl { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string FromAddress { get; set; } = "nao-responda@datavello.local";
    public string FromName { get; set; } = "Datavello";
}

/// <summary>Public address of the web app ("App:PublicUrl"), used to build links in e-mails. Never derived from request headers.</summary>
public sealed class AppUrlSettings
{
    public const string Section = "App";

    public string PublicUrl { get; set; } = string.Empty;

    public string Link(string pathAndQuery) => PublicUrl.TrimEnd('/') + pathAndQuery;
}

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// Sends through SMTP with the built-in System.Net.Mail client (no extra dependency; enough
/// for Mailpit and for providers that accept STARTTLS on 587).
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            logger.LogWarning("E-mail not sent: Email:SmtpHost is not configured (subject: {Subject}).", message.Subject);
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, System.Text.Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.SmtpEnableSsl };
        if (!string.IsNullOrEmpty(settings.SmtpUser))
        {
            client.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword);
        }

        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// E-mails are queued and sent in the background, so a request never waits for (or fails
/// because of) the SMTP server, and response time does not reveal whether an e-mail was sent.
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Enqueue(EmailMessage message)
    {
        if (!_channel.Writer.TryWrite(message))
        {
            throw new InvalidOperationException("E-mail queue is full.");
        }
    }

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never log the body: it may contain single-use links.
                logger.LogError("Failed to send e-mail '{Subject}': {Error}", message.Subject, ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
