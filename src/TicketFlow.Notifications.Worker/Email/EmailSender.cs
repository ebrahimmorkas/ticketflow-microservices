using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TicketFlow.Notifications.Worker.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody);

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; init; } = "tickets@ticketflow.dev";

    public string FromName { get; init; } = "TicketFlow";

    /// <summary>SMTP endpoint, e.g. <c>smtp://localhost:1025</c>. Filled from the Aspire MailPit connection string.</summary>
    public Uri SmtpEndpoint { get; set; } = new("smtp://localhost:1025");
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpEndpoint.Host, _options.SmtpEndpoint.Port, MailKit.Security.SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
