using MailKit.Net.Smtp;
using MimeKit;

namespace VolunteerCheckin.Functions.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;

    /// <summary>
    /// Protected constructor for testing/mocking purposes.
    /// </summary>
    protected EmailService()
    {
        _smtpHost = string.Empty;
        _smtpPort = 0;
        _smtpUsername = string.Empty;
        _smtpPassword = string.Empty;
        _fromEmail = string.Empty;
        _fromName = string.Empty;
    }

    public EmailService(string smtpHost, int smtpPort, string smtpUsername, string smtpPassword, string fromEmail, string fromName)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUsername = smtpUsername;
        _smtpPassword = smtpPassword;
        _fromEmail = fromEmail;
        _fromName = fromName;
    }

    public virtual async Task SendMagicLinkEmailAsync(string toEmail, string magicLink)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "Your Volunteer Check-in Admin Login Link";

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = $@"
                <html>
                <body>
                    <h2>Volunteer Check-in Admin Login</h2>
                    <p>Click the link below to log in to the Volunteer Check-in admin portal:</p>
                    <p><a href='{magicLink}'>Login to Admin Portal</a></p>
                    <p>This link will expire in 15 minutes.</p>
                    <p>If you did not request this link, please ignore this email.</p>
                </body>
                </html>
            ",
            TextBody = $@"
                Volunteer Check-in Admin Login

                Click the link below to log in to the Volunteer Check-in admin portal:
                {magicLink}

                This link will expire in 15 minutes.

                If you did not request this link, please ignore this email.
            "
        };

        message.Body = bodyBuilder.ToMessageBody();

        using SmtpClient client = new();
        await client.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public virtual async Task SendMarshalMagicLinkEmailAsync(string toEmail, string marshalName, string eventName, string magicLink)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(marshalName, toEmail));
        message.Subject = $"Your Marshal Login Link for {eventName}";

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = $@"
                <html>
                <body>
                    <h2>Marshal Login for {eventName}</h2>
                    <p>Hi {marshalName},</p>
                    <p>Click the link below to access your marshal dashboard:</p>
                    <p><a href='{magicLink}' style='display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 4px;'>Open Marshal Dashboard</a></p>
                    <p>Or copy and paste this link into your browser:</p>
                    <p style='word-break: break-all;'>{magicLink}</p>
                    <p>This link is unique to you - please don't share it with others.</p>
                </body>
                </html>
            ",
            TextBody = $@"
                Marshal Login for {eventName}

                Hi {marshalName},

                Click the link below to access your marshal dashboard:
                {magicLink}

                This link is unique to you - please don't share it with others.
            "
        };

        message.Body = bodyBuilder.ToMessageBody();

        using SmtpClient client = new();
        await client.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
