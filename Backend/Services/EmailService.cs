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

    public virtual async Task SendMagicLinkEmailAsync(string toEmail, string magicLink, string loginCode)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "Your OnTheDayApp Login Code";

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #1a1a2e;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color: #1a1a2e; padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' style='max-width: 420px; background: linear-gradient(180deg, #1e1e2e 0%, #252538 100%); border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.1); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 32px 32px 24px; text-align: center;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;'>OnTheDayApp</h1>
                        </td>
                    </tr>
                    <!-- Code Section -->
                    <tr>
                        <td style='padding: 0 32px 24px; text-align: center;'>
                            <p style='margin: 0 0 16px; color: rgba(255, 255, 255, 0.85); font-size: 15px;'>Your login code is:</p>
                            <div style='background: rgba(102, 126, 234, 0.15); border: 2px solid rgba(102, 126, 234, 0.3); border-radius: 12px; padding: 20px; margin: 0 auto; display: inline-block;'>
                                <span style='font-size: 36px; font-weight: 700; letter-spacing: 8px; color: #667eea; font-family: monospace;'>{loginCode}</span>
                            </div>
                            <p style='margin: 16px 0 0; color: rgba(255, 255, 255, 0.6); font-size: 14px;'>Enter this code in the app to log in</p>
                        </td>
                    </tr>
                    <!-- Divider -->
                    <tr>
                        <td style='padding: 0 32px;'>
                            <div style='border-top: 1px solid rgba(255, 255, 255, 0.1); margin: 8px 0;'></div>
                        </td>
                    </tr>
                    <!-- Link Section -->
                    <tr>
                        <td style='padding: 24px 32px; text-align: center;'>
                            <p style='margin: 0 0 16px; color: rgba(255, 255, 255, 0.6); font-size: 14px;'>Or click the button below:</p>
                            <a href='{magicLink}' style='display: inline-block; padding: 14px 32px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; font-weight: 600; font-size: 15px; border-radius: 50px; box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);'>Login to OnTheDayApp</a>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='padding: 24px 32px 32px; text-align: center;'>
                            <p style='margin: 0; color: rgba(255, 255, 255, 0.4); font-size: 12px;'>This code expires in 15 minutes.<br>If you didn't request this, you can safely ignore this email.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
            ",
            TextBody = $@"
OnTheDayApp Login

Your login code is: {loginCode}

Enter this code in the app to log in.

Or use this link:
{magicLink}

This code expires in 15 minutes.

If you didn't request this, you can safely ignore this email.
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
        await SendMarshalMagicLinkWithDetailsEmailAsync(toEmail, marshalName, eventName, magicLink, null, null, null);
    }

    public virtual async Task SendMarshalMagicLinkWithDetailsEmailAsync(
        string toEmail,
        string marshalName,
        string eventName,
        string magicLink,
        DateTime? eventStartTime,
        string? checkpointTerm,
        List<Models.CheckpointEmailInfo>? checkpoints)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress(marshalName, toEmail));
        message.Subject = $"Your Login Link for {eventName}";

        // Build event details section if provided
        string eventDetailsHtml = "";
        string eventDetailsText = "";

        if (eventStartTime.HasValue)
        {
            eventDetailsHtml = $@"
                    <!-- Event details -->
                    <tr>
                        <td style='padding: 0 32px 16px;'>
                            <div style='background: rgba(102, 126, 234, 0.1); border-radius: 12px; padding: 16px 20px;'>
                                <h3 style='margin: 0 0 8px; color: rgba(255, 255, 255, 0.9); font-size: 15px; font-weight: 600;'>Event details</h3>
                                <p style='margin: 0; color: rgba(255, 255, 255, 0.7); font-size: 14px;'>Starts: {eventStartTime.Value:dddd, MMMM d, yyyy 'at' h:mm tt}</p>
                            </div>
                        </td>
                    </tr>";
            eventDetailsText = $"\n\nEvent details:\nStarts: {eventStartTime.Value:dddd, MMMM d, yyyy 'at' h:mm tt}\n";
        }

        // Build checkpoint details section if provided
        string checkpointDetailsHtml = "";
        string checkpointDetailsText = "";
        string checkpointTermDisplay = checkpointTerm ?? "Checkpoint";

        if (checkpoints != null && checkpoints.Count > 0)
        {
            string checkpointTermPlural = checkpoints.Count == 1 ? checkpointTermDisplay.ToLower() : $"{checkpointTermDisplay.ToLower()}s";

            checkpointDetailsHtml = $@"
                    <!-- Your checkpoints -->
                    <tr>
                        <td style='padding: 0 32px 16px;'>
                            <div style='background: rgba(102, 126, 234, 0.1); border-radius: 12px; padding: 16px 20px;'>
                                <h3 style='margin: 0 0 12px; color: rgba(255, 255, 255, 0.9); font-size: 15px; font-weight: 600;'>Your {checkpointTermPlural}</h3>";

            checkpointDetailsText = $"\nYour {checkpointTermPlural}:\n";

            foreach (Models.CheckpointEmailInfo checkpoint in checkpoints)
            {
                string arrivalHtml = checkpoint.ArrivalTime.HasValue && (!eventStartTime.HasValue || checkpoint.ArrivalTime != eventStartTime)
                    ? $" <span style='color: rgba(255, 255, 255, 0.5);'>- arrive by {checkpoint.ArrivalTime.Value:h:mm tt}</span>"
                    : "";

                // Name and description - make it a map link if lat/long available
                string nameHtml;
                string descriptionHtml = !string.IsNullOrWhiteSpace(checkpoint.Description)
                    ? $" - {System.Net.WebUtility.HtmlEncode(checkpoint.Description)}"
                    : "";

                if (checkpoint.Latitude.HasValue && checkpoint.Longitude.HasValue)
                {
                    // Create Google Maps link that works on all devices
                    string mapUrl = $"https://www.google.com/maps/search/?api=1&query={checkpoint.Latitude.Value},{checkpoint.Longitude.Value}";
                    nameHtml = $"<a href='{mapUrl}' style='color: #667eea; text-decoration: none;'>{System.Net.WebUtility.HtmlEncode(checkpoint.Name)}{descriptionHtml}</a>";
                }
                else
                {
                    nameHtml = $"{System.Net.WebUtility.HtmlEncode(checkpoint.Name)}{descriptionHtml}";
                }

                checkpointDetailsHtml += $@"
                                <p style='margin: 0 0 6px; color: rgba(255, 255, 255, 0.85); font-size: 14px;'>• {nameHtml}{arrivalHtml}</p>";

                // Add notes for this checkpoint
                if (checkpoint.Notes.Count > 0)
                {
                    foreach (Models.NoteEmailInfo note in checkpoint.Notes)
                    {
                        string priorityStyle = note.Priority switch
                        {
                            "Emergency" => "color: #ef4444;",
                            "Urgent" => "color: #f97316;",
                            "High" => "color: #eab308;",
                            _ => "color: rgba(255, 255, 255, 0.6);"
                        };
                        string pinnedIcon = note.IsPinned ? "📌 " : "";
                        checkpointDetailsHtml += $@"
                                <p style='margin: 0 0 4px 16px; font-size: 13px; {priorityStyle}'>{pinnedIcon}{System.Net.WebUtility.HtmlEncode(note.Title)}: {System.Net.WebUtility.HtmlEncode(note.Content)}</p>";
                    }
                }

                // Plain text version
                string arrivalText = checkpoint.ArrivalTime.HasValue && (!eventStartTime.HasValue || checkpoint.ArrivalTime != eventStartTime)
                    ? $" - arrive by {checkpoint.ArrivalTime.Value:h:mm tt}"
                    : "";
                string textDisplayText = !string.IsNullOrWhiteSpace(checkpoint.Description)
                    ? $"{checkpoint.Name} - {checkpoint.Description}"
                    : checkpoint.Name;

                if (checkpoint.Latitude.HasValue && checkpoint.Longitude.HasValue)
                {
                    string mapUrl = $"https://www.google.com/maps/search/?api=1&query={checkpoint.Latitude.Value},{checkpoint.Longitude.Value}";
                    checkpointDetailsText += $"• {textDisplayText}{arrivalText}\n  Map: {mapUrl}\n";
                }
                else
                {
                    checkpointDetailsText += $"• {textDisplayText}{arrivalText}\n";
                }

                // Add notes to plain text version
                foreach (Models.NoteEmailInfo note in checkpoint.Notes)
                {
                    string pinnedMarker = note.IsPinned ? "[Pinned] " : "";
                    checkpointDetailsText += $"  {pinnedMarker}{note.Title}: {note.Content}\n";
                }
            }

            checkpointDetailsHtml += @"
                                <p style='margin: 12px 0 0; color: rgba(255, 255, 255, 0.5); font-size: 12px; font-style: italic;'>Please note that your allocated checkpoints, times, and other instructions may change, so please use the app to keep up to date.</p>
                            </div>
                        </td>
                    </tr>";

            checkpointDetailsText += "\nPlease note that your allocated checkpoints, times, and other instructions may change, so please use the app to keep up to date.\n";
        }

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #1a1a2e;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color: #1a1a2e; padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' style='max-width: 480px; background: linear-gradient(180deg, #1e1e2e 0%, #252538 100%); border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.1); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 32px 32px 16px; text-align: center;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; color: #667eea;'>OnTheDayApp</h1>
                        </td>
                    </tr>
                    <!-- Greeting and Button -->
                    <tr>
                        <td style='padding: 0 32px 20px; text-align: center;'>
                            <p style='margin: 0; color: rgba(255, 255, 255, 0.85); font-size: 16px;'>Hi {System.Net.WebUtility.HtmlEncode(marshalName)},</p>
                            <p style='margin: 8px 0 16px; color: rgba(255, 255, 255, 0.7); font-size: 15px;'>Here's your login link for <strong>{System.Net.WebUtility.HtmlEncode(eventName)}</strong></p>
                            <a href='{magicLink}' style='display: inline-block; padding: 14px 32px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; font-weight: 600; font-size: 15px; border-radius: 50px; box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);'>Open my dashboard</a>
                            <p style='margin: 12px 0 0; color: rgba(255, 255, 255, 0.4); font-size: 12px;'>This link is unique to you - please don't share it with others.</p>
                        </td>
                    </tr>
                    {eventDetailsHtml}
                    {checkpointDetailsHtml}
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
            ",
            TextBody = $@"
OnTheDayApp

Hi {marshalName},

Here's your login link for {eventName}

Click the link below to access your dashboard:
{magicLink}
{eventDetailsText}{checkpointDetailsText}
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

    /// <summary>
    /// Send an email notification when a user is added to an event with a specific role.
    /// </summary>
    public virtual async Task SendEventUserAddedEmailAsync(
        string toEmail,
        string? userName,
        string eventName,
        string role,
        string roleDescription,
        string addedByName,
        string loginUrl)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        string displayName = !string.IsNullOrWhiteSpace(userName) ? userName : toEmail;
        message.To.Add(new MailboxAddress(displayName, toEmail));
        message.Subject = $"You've been added to {eventName}";

        // Get role display name (strip "Event" prefix if present)
        string roleDisplay = role switch
        {
            "EventOwner" => "Owner",
            "EventAdministrator" => "Administrator",
            "EventContributor" => "Contributor",
            "EventViewer" => "Viewer",
            _ => role.Replace("Event", "")
        };

        // Get role color for styling
        string roleColor = role switch
        {
            "EventOwner" => "#7c3aed",        // Purple
            "EventAdministrator" => "#2563eb", // Blue
            "EventContributor" => "#059669",   // Green
            "EventViewer" => "#6b7280",        // Gray
            _ => "#667eea"
        };

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #1a1a2e;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color: #1a1a2e; padding: 40px 20px;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' style='max-width: 480px; background: linear-gradient(180deg, #1e1e2e 0%, #252538 100%); border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.1); overflow: hidden;'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 32px 32px 16px; text-align: center;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; color: #667eea;'>OnTheDayApp</h1>
                        </td>
                    </tr>
                    <!-- Greeting -->
                    <tr>
                        <td style='padding: 0 32px 20px; text-align: center;'>
                            <p style='margin: 0; color: rgba(255, 255, 255, 0.85); font-size: 16px;'>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
                            <p style='margin: 12px 0 0; color: rgba(255, 255, 255, 0.7); font-size: 15px;'><strong>{System.Net.WebUtility.HtmlEncode(addedByName)}</strong> has added you to <strong>{System.Net.WebUtility.HtmlEncode(eventName)}</strong></p>
                        </td>
                    </tr>
                    <!-- Role info -->
                    <tr>
                        <td style='padding: 0 32px 24px;'>
                            <div style='background: rgba(102, 126, 234, 0.1); border-radius: 12px; padding: 20px;'>
                                <p style='margin: 0 0 8px; color: rgba(255, 255, 255, 0.6); font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px;'>Your role</p>
                                <div style='display: inline-block; padding: 6px 14px; background: {roleColor}; border-radius: 50px; margin-bottom: 12px;'>
                                    <span style='color: white; font-weight: 600; font-size: 14px;'>{roleDisplay}</span>
                                </div>
                                <p style='margin: 0; color: rgba(255, 255, 255, 0.7); font-size: 14px; line-height: 1.5;'>{System.Net.WebUtility.HtmlEncode(roleDescription)}</p>
                            </div>
                        </td>
                    </tr>
                    <!-- Login button -->
                    <tr>
                        <td style='padding: 0 32px 32px; text-align: center;'>
                            <p style='margin: 0 0 16px; color: rgba(255, 255, 255, 0.6); font-size: 14px;'>Log in to get started:</p>
                            <a href='{loginUrl}' style='display: inline-block; padding: 14px 32px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; font-weight: 600; font-size: 15px; border-radius: 50px; box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);'>Open OnTheDayApp</a>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
            ",
            TextBody = $@"
OnTheDayApp

Hi {displayName},

{addedByName} has added you to {eventName}.

Your role: {roleDisplay}
{roleDescription}

Log in to get started:
{loginUrl}
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
