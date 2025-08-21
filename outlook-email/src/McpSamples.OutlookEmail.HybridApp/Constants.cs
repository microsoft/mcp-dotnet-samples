namespace McpSamples.OutlookEmail.HybridApp;

/// <summary>
/// This represents the entity containing all the magic numbers and strings.
/// </summary>
public class Constants
{
    /// <summary>
    /// The default scope for Microsoft Graph API.
    /// </summary>
    public const string DefaultScope = "https://graph.microsoft.com/.default";

    /// <summary>
    /// The environment variable key for Azure Client ID.
    /// </summary>
    public const string AzureClientIdEnvironmentKey = "AZURE_CLIENT_ID";

    /// <summary>
    /// The environment variable key for Azure Functions Custom Handler Port.
    /// </summary>
    public const string AzureFunctionsCustomHandlerPortEnvironmentKey = "FUNCTIONS_CUSTOMHANDLER_PORT";

    /// <summary>
    /// The default port for the custom handler.
    /// </summary>
    public const int DefaultAppPort = 5260;

    /// <summary>
    /// The default URL for the application.
    /// </summary>
    public const string DefaultAppUrl = "http://0.0.0.0:{0}";
}
