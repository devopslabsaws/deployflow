using Azure.Communication.Email;
using DeployFlow.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var connectionString = _config["Email:AzureCommunicationConnectionString"];
        var from = _config["Email:From"] ?? "DoNotReply@8da3d5d0-6c66-47fd-b57a-393e1f041147.azurecomm.net";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Azure Communication Services connection string not configured. Email to {To} not sent.", to);
            return;
        }

        try
        {
            var emailClient = new EmailClient(connectionString);

            var emailMessage = new EmailMessage(
                senderAddress: from,
                content: new EmailContent(subject)
                {
                    Html = htmlBody
                },
                recipients: new EmailRecipients(new[] { new EmailAddress(to) })
            );

            await emailClient.SendAsync(Azure.WaitUntil.Completed, emailMessage, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} with subject: {Subject}", to, subject);
            // Don't throw — email failure shouldn't break the main flow
        }
    }

    public async Task SendTemplateAsync(
        string to,
        string templateName,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var subject = variables.TryGetValue("subject", out var s) ? s : templateName;
        var body = variables.Aggregate(
            GetDefaultTemplate(templateName),
            (current, kv) => current.Replace($"{{{{{kv.Key}}}}}", kv.Value));

        await SendAsync(to, subject, body, ct);
    }

    private static string GetDefaultTemplate(string templateName) => templateName switch
    {
        "invite" => "<h2>You've been invited</h2><p>{{message}}</p>",
        "password-reset" => "<h2>Password Reset</h2><p>Click here to reset: {{link}}</p>",
        "deployment-success" => "<h2>✅ Deployment Succeeded</h2><p>Project <strong>{{project}}</strong> deployed successfully.</p>",
        "deployment-failed" => "<h2>❌ Deployment Failed</h2><p>Project <strong>{{project}}</strong> deployment failed: {{reason}}</p>",
        _ => "<p>{{message}}</p>"
    };
}
