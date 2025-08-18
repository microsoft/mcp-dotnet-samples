using System.ComponentModel;

using McpSamples.OutlookEmail.HybridApp.Configurations;

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
    /// <returns>The result of the email sending operation.</returns>
    Task<object> SendEmailAsync(string title, string body, IEnumerable<string> recipients);
}

/// <summary>
/// This represents the tool entity for Outlook email.
/// </summary>
/// <param name="settings"><see cref="OutlookEmailAppSettings"/> instance.</param>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
[McpServerToolType]
public class OutlookEmailTool(OutlookEmailAppSettings settings, ILogger<OutlookEmailTool> logger) : IOutlookEmailTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "send_email", Title = "Send Email")]
    [Description("Sends an email.")]
    public async Task<object> SendEmailAsync(
        [Description("The email title")] string title,
        [Description("The email body")] string body,
        [Description("The email recipients")] IEnumerable<string> recipients)
    {
        throw new NotImplementedException("This method is not implemented yet.");
    }
}
