using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace McpSamples.OutlookEmail.HybridApp.Services;

/// <summary>
/// This provides interfaces for Outlook email service operations.
/// </summary>
public interface IOutlookEmailService
{
    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="title">The email title.</param>
    /// <param name="body">The email body.</param>
    /// <param name="sender">The email sender.</param>
    /// <param name="recipients">The email recipients separated by a comma or semicolon.</param>
    /// <returns>The result of the email sending operation.</returns>
    Task<SendMailPostRequestBody> SendEmailAsync(string title, string body, string sender, string recipients);
}

/// <summary>
/// This represents the service entity for Outlook email.
/// </summary>
/// <param name="settings"></param>
/// <param name="logger"></param>
public class OutlookEmailService(GraphServiceClient graph, ILogger<OutlookEmailService> logger) : IOutlookEmailService
{
    /// <inheritdoc />
    public async Task<SendMailPostRequestBody> SendEmailAsync(string title, string body, string sender, string recipients)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentException.ThrowIfNullOrWhiteSpace(body, nameof(body));
        ArgumentException.ThrowIfNullOrWhiteSpace(sender, nameof(sender));
        ArgumentException.ThrowIfNullOrWhiteSpace(recipients, nameof(recipients));
        var recipientList = recipients.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (recipientList.Length == 0)
        {
            throw new ArgumentException("At least one recipient is required", nameof(recipients));
        }

        var req = BuildMailRequest(title, body, recipientList);

        try
        {
            var user = graph.Users[sender];
            await user.SendMail.PostAsync(req);

            logger.LogInformation("Email sent successfully to {Recipients} with subject: {Subject} from {Sender}.", string.Join(", ", recipientList), title, sender);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipients} with subject: {Subject} from {Sender}.", string.Join(", ", recipientList), title, sender);
            throw;
        }

        return req;
    }

    private static SendMailPostRequestBody BuildMailRequest(string title, string body, IEnumerable<string> recipients)
    {
        var message = new Message
        {
            Subject = title,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = body
            },
            ToRecipients = [.. recipients.Select(r => new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = r
                }
            })]
        };

        var req = new SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = true
        };

        return req;
    }
}
