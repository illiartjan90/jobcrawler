using System.Net;
using System.Net.Mail;
using JobApplicationAgent.Application;
using JobApplicationAgent.Domain;

namespace JobApplicationAgent.Infrastructure;

/// <summary>
/// Versand per SMTP. Standardmässig AUS (Smtp:Enabled=false).
/// Das Passwort kommt nie aus der Konfigurationsdatei, sondern aus der
/// Umgebungsvariable SMTP_PASSWORD (bei Hotmail/Gmail: App-Passwort).
/// </summary>
public sealed class SmtpMailer(IConfiguration cfg, CandidateProfile profile) : IApplicationMailer
{
    string? Password => Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    string? Host => cfg["Smtp:Host"];
    string? From => cfg["Smtp:From"] ?? profile.Email;

    public bool IsEnabled =>
        cfg.GetValue<bool>("Smtp:Enabled") &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(From) &&
        !string.IsNullOrWhiteSpace(Password);

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        IReadOnlyList<MailAttachment> attachments,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
            throw new InvalidOperationException(
                "E-Mail-Versand ist nicht eingerichtet (Smtp:Enabled, Smtp:Host, SMTP_PASSWORD).");

        using var message = new MailMessage
        {
            From = new MailAddress(From!, profile.Name),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(to));
        message.ReplyToList.Add(new MailAddress(From!, profile.Name));

        foreach (var attachment in attachments)
        {
            message.Attachments.Add(new Attachment(
                new MemoryStream(attachment.Bytes),
                attachment.FileName,
                attachment.ContentType));
        }

        using var client = new SmtpClient(Host!, cfg.GetValue<int?>("Smtp:Port") ?? 587)
        {
            EnableSsl = cfg.GetValue<bool?>("Smtp:EnableSsl") ?? true,
            Credentials = new NetworkCredential(cfg["Smtp:User"] ?? From, Password)
        };

        await client.SendMailAsync(message, ct);
    }
}
