using System.ComponentModel;

using McpSamples.OutlookEmail.HybridApp.Configurations;
using McpSamples.OutlookEmail.HybridApp.Models;
using McpSamples.OutlookEmail.HybridApp.Services;

using ModelContextProtocol.Server;

namespace McpSamples.OutlookEmail.HybridApp.Tools;

/// <summary>
/// This provides interfaces for the Outlook email tool.
/// </summary>
public interface IOutlookEmailTool
{
    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="title">The email title.</param>
    /// <param name="body">The email body.</param>
    /// <param name="sender">The email sender.</param>
    /// <param name="recipients">The email recipients separated by a comma or semicolon.</param>
    /// <returns>Returns <see cref="OutlookEmailResult"/> instance.</returns>
    Task<OutlookEmailResult> SendEmailAsync(string title, string body, string sender, string recipients);
}

/// <summary>
/// This represents the tool entity for Outlook email.
/// </summary>
/// <param name="settings"><see cref="OutlookEmailAppSettings"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class OutlookEmailTool(IOutlookEmailService service, ILogger<OutlookEmailTool> logger) : IOutlookEmailTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "send_email", Title = "Send an Email")]
    [Description("Sends an email to recipients.")]
    public async Task<OutlookEmailResult> SendEmailAsync(
        [Description("The email title")] string title,
        [Description("The email body")] string body,
        [Description("The email sender")] string sender,
        [Description("The email recipients separated by a comma or semicolon")] string recipients)
    {
        var result = new OutlookEmailResult();
        try
        {
            var requestBody = await service.SendEmailAsync(title, body, sender, recipients).ConfigureAwait(false);

            logger.LogInformation("Email sent successfully to {Recipients} with subject: {Subject} from {Sender}.", recipients, title, sender);

            result.RequestBody = requestBody;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipients} with subject: {Subject} from {Sender}.", recipients, title, sender);

            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}

