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

    public EmailService(string smtpHost, int smtpPort, string smtpUsername, string smtpPassword, string fromEmail, string fromName)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUsername = smtpUsername;
        _smtpPassword = smtpPassword;
        _fromEmail = fromEmail;
        _fromName = fromName;
    }

    public async Task SendMagicLinkEmailAsync(string toEmail, string magicLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "Your Volunteer Check-in Admin Login Link";

        var bodyBuilder = new BodyBuilder
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

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
