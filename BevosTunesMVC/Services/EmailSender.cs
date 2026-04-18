using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace BevosTunesMVC.Services;

/// <summary>
/// Sends mail via SMTP using <c>Email:*</c> configuration (see appsettings / env vars).
/// </summary>
public sealed class EmailSender
{
    private readonly IConfiguration _config;

    public EmailSender(IConfiguration config) => _config = config;

    public string SubjectPrefix =>
        _config["Email:SubjectPrefix"]?.Trim() ?? "Team 7:";

    public void TrySend(string to, string subject, string body)
    {
        var from = _config["Email:FromAddress"]?.Trim();
        var appPassword = _config["Email:AppPassword"]?.Trim();
        var host = _config["Email:SmtpHost"]?.Trim() ?? "smtp.gmail.com";
        var port = _config.GetValue("Email:SmtpPort", 587);
        var gradingRaw = _config["Email:GradingInbox"]?.Trim();

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(appPassword))
        {
            Debug.WriteLine("Email skipped: Email:FromAddress or Email:AppPassword not configured.");
            return;
        }

        try
        {
            using var msg = new MailMessage(from, to, subject, body);
            if (!string.IsNullOrEmpty(gradingRaw))
            {
                foreach (var addr in gradingRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (addr.Length > 0 && !string.Equals(addr, to, StringComparison.OrdinalIgnoreCase))
                        msg.Bcc.Add(addr);
                }
            }
            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(from, appPassword)
            };
            smtp.Send(msg);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Email send failed: {ex}");
            Console.WriteLine($"Email send failed: {ex.Message}");
        }
    }
}
