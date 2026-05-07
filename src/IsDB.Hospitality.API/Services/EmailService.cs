using IsDB.Hospitality.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IsDB.Hospitality.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendDepartureConfirmationAsync(
        string toEmail, string toName,
        string hotelName, string roomNumber,
        string pickupDay, string pickupHour,
        string manageUrl,
        CancellationToken cancellationToken = default)
    {
        var subject = "Your Departure Shuttle Registration — IsDB Annual Meetings";
        var body = BuildConfirmationBody(toName, hotelName, roomNumber, pickupDay, pickupHour, manageUrl, isUpdate: false);
        await SendAsync(toEmail, toName, subject, body, cancellationToken);
    }

    public async Task SendDepartureUpdateAsync(
        string toEmail, string toName,
        string hotelName, string roomNumber,
        string pickupDay, string pickupHour,
        string manageUrl,
        CancellationToken cancellationToken = default)
    {
        var subject = "Departure Shuttle Registration Updated — IsDB Annual Meetings";
        var body = BuildConfirmationBody(toName, hotelName, roomNumber, pickupDay, pickupHour, manageUrl, isUpdate: true);
        await SendAsync(toEmail, toName, subject, body, cancellationToken);
    }

    public async Task SendDepartureCancellationAsync(
        string toEmail, string toName,
        string reRegisterUrl,
        CancellationToken cancellationToken = default)
    {
        var subject = "Departure Shuttle Registration Cancelled — IsDB Annual Meetings";
        var body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'></head>
<body style='font-family:Inter,Arial,sans-serif;background:#f9fafb;margin:0;padding:32px 16px;'>
  <div style='max-width:560px;margin:0 auto;background:white;border-radius:16px;border:1px solid #e5e7eb;overflow:hidden;'>
    <div style='background:#1a3c5e;padding:28px 32px;text-align:center;'>
      <p style='color:rgba(255,255,255,0.6);font-size:13px;margin:0;'>IsDB Annual Meetings</p>
      <h1 style='color:white;font-size:20px;margin:8px 0 0;'>Departure Shuttle</h1>
    </div>
    <div style='padding:32px;'>
      <p style='font-size:15px;color:#374151;'>Dear {toName},</p>
      <p style='font-size:14px;color:#6b7280;line-height:1.6;'>
        Your departure shuttle registration has been <strong style='color:#ef4444;'>cancelled</strong>.
      </p>
      <p style='font-size:14px;color:#6b7280;line-height:1.6;'>
        If this was a mistake, you can re-register at any time using the link below.
      </p>
      <div style='text-align:center;margin:28px 0;'>
        <a href='{reRegisterUrl}' style='display:inline-block;background:#3aaa35;color:white;text-decoration:none;padding:12px 28px;border-radius:10px;font-size:14px;font-weight:600;'>
          Re-Register for Shuttle
        </a>
      </div>
    </div>
    <div style='padding:16px 32px;border-top:1px solid #f3f4f6;text-align:center;'>
      <p style='font-size:11px;color:#9ca3af;margin:0;'>IsDB Annual Meetings · Hospitality Platform</p>
    </div>
  </div>
</body>
</html>";
        await SendAsync(toEmail, toName, subject, body, cancellationToken);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static string BuildConfirmationBody(
        string name, string hotel, string room,
        string day, string hour, string manageUrl, bool isUpdate)
    {
        var verb = isUpdate ? "updated" : "confirmed";
        var heading = isUpdate ? "Registration Updated" : "You're Registered!";
        var intro = isUpdate
            ? "Your departure shuttle registration has been <strong>updated</strong>. Here are your new details:"
            : "Your departure shuttle seat has been <strong>reserved</strong>. Here are your details:";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'></head>
<body style='font-family:Inter,Arial,sans-serif;background:#f9fafb;margin:0;padding:32px 16px;'>
  <div style='max-width:560px;margin:0 auto;background:white;border-radius:16px;border:1px solid #e5e7eb;overflow:hidden;'>
    <div style='background:#1a3c5e;padding:28px 32px;text-align:center;'>
      <p style='color:rgba(255,255,255,0.6);font-size:13px;margin:0;'>IsDB Annual Meetings</p>
      <h1 style='color:white;font-size:20px;margin:8px 0 0;'>Departure Shuttle</h1>
    </div>
    <div style='padding:32px;'>
      <div style='text-align:center;margin-bottom:24px;'>
        <div style='width:64px;height:64px;background:#edf7ec;border-radius:50%;display:inline-flex;align-items:center;justify-content:center;font-size:28px;'>✅</div>
        <h2 style='font-size:20px;color:#111827;margin:12px 0 4px;'>{heading}</h2>
      </div>
      <p style='font-size:14px;color:#374151;'>Dear {name},</p>
      <p style='font-size:14px;color:#6b7280;line-height:1.6;'>{intro}</p>
      <table style='width:100%;border-collapse:collapse;margin:20px 0;border-radius:10px;overflow:hidden;border:1px solid #e5e7eb;'>
        <tr style='background:#f9fafb;'><td style='padding:10px 16px;font-size:13px;color:#6b7280;border-bottom:1px solid #f3f4f6;'>Hotel</td><td style='padding:10px 16px;font-size:13px;font-weight:500;color:#111827;border-bottom:1px solid #f3f4f6;'>{hotel}</td></tr>
        <tr><td style='padding:10px 16px;font-size:13px;color:#6b7280;border-bottom:1px solid #f3f4f6;'>Room</td><td style='padding:10px 16px;font-size:13px;font-weight:500;color:#111827;border-bottom:1px solid #f3f4f6;'>{room}</td></tr>
        <tr style='background:#f9fafb;'><td style='padding:10px 16px;font-size:13px;color:#6b7280;border-bottom:1px solid #f3f4f6;'>Pickup Day</td><td style='padding:10px 16px;font-size:13px;font-weight:500;color:#111827;border-bottom:1px solid #f3f4f6;'>{day}</td></tr>
        <tr><td style='padding:10px 16px;font-size:13px;color:#6b7280;'>Pickup Time</td><td style='padding:10px 16px;font-size:14px;font-weight:700;color:#3aaa35;'>{hour}</td></tr>
      </table>
      <div style='background:#fffbeb;border:1px solid #fde68a;border-radius:10px;padding:14px 16px;margin-bottom:24px;'>
        <p style='font-size:13px;color:#92400e;margin:0;'>⏰ <strong>Reminder:</strong> Please be in the hotel lobby <strong>15 minutes before</strong> your scheduled pickup time.</p>
      </div>
      <p style='font-size:13px;color:#6b7280;margin-bottom:16px;'>Need to modify or cancel your registration? Use the link below:</p>
      <div style='text-align:center;margin-bottom:8px;'>
        <a href='{manageUrl}' style='display:inline-block;background:#1a3c5e;color:white;text-decoration:none;padding:12px 28px;border-radius:10px;font-size:14px;font-weight:600;'>
          Manage My Registration
        </a>
      </div>
      <p style='font-size:11px;color:#9ca3af;text-align:center;'>Or copy this link: {manageUrl}</p>
    </div>
    <div style='padding:16px 32px;border-top:1px solid #f3f4f6;text-align:center;'>
      <p style='font-size:11px;color:#9ca3af;margin:0;'>IsDB Annual Meetings · Hospitality Platform</p>
    </div>
  </div>
</body>
</html>";
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var smtpSection = _config.GetSection("Smtp");
        var host = smtpSection["Host"];

        if (string.IsNullOrEmpty(host))
        {
            _logger.LogWarning("SMTP not configured. Skipping email to {Email} — Subject: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                smtpSection["FromName"] ?? "IsDB Hospitality",
                smtpSection["FromEmail"] ?? "noreply@isdb.org"));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                host,
                int.Parse(smtpSection["Port"] ?? "587"),
                SecureSocketOptions.StartTls,
                ct);

            var user = smtpSection["Username"];
            var pass = smtpSection["Password"];
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {Email} — {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
