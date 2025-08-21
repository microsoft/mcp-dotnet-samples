using Microsoft.Graph.Users.Item.SendMail;

namespace McpSamples.OutlookEmail.HybridApp.Models;

/// <summary>
/// This represents the result of an Outlook email operation.
/// </summary>
public class OutlookEmailResult
{
    /// <summary>
    /// Gets or sets the request body for the email.
    /// </summary>
    public SendMailPostRequestBody? RequestBody { get; set; }

    /// <summary>
    /// Gets or sets the error message if any error occurs.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
