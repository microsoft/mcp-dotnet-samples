using System.ComponentModel;
using System.Text;
using Azure.Storage.Files.Shares;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;

namespace McpSamples.OnedriveDownload.HybridApp.Tools;

/// <summary>
/// Represents the result of a OneDrive file download operation.
/// </summary>
public class OneDriveDownloadResult
{
    /// <summary>
    /// Gets or sets the path of the downloaded file in the file share.
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// Gets or sets the name of the downloaded file.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the error message if the download failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// This provides interfaces for the OneDrive tool.
/// </summary>
public interface IOneDriveTool
{
    /// <summary>
    /// Downloads a file from a OneDrive sharing URL and saves it to a file share.
    /// </summary>
    /// <param name="sharingUrl">The OneDrive sharing URL.</param>
    /// <param name="userId">Optional: User ID to use cached OAuth token.</param>
    /// <returns>Returns <see cref="OneDriveDownloadResult"/> instance.</returns>
    Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(string sharingUrl, string? userId = null);
}

/// <summary>
/// This represents the tool entity for OneDrive file operations.
/// </summary>
[McpServerToolType]
public class OneDriveTool(IServiceProvider serviceProvider) : IOneDriveTool
{
    private GraphServiceClient? _graphServiceClient;
    private IConfiguration? _configuration;
    private ILogger<OneDriveTool>? _logger;
    private ITokenStorage? _tokenStorage;
    private IHttpContextAccessor? _httpContextAccessor;

    private GraphServiceClient GraphServiceClient => _graphServiceClient ??= serviceProvider.GetRequiredService<GraphServiceClient>();
    private IConfiguration Configuration => _configuration ??= serviceProvider.GetRequiredService<IConfiguration>();
    private ILogger<OneDriveTool> Logger => _logger ??= serviceProvider.GetRequiredService<ILogger<OneDriveTool>>();
    private ITokenStorage TokenStorage => _tokenStorage ??= serviceProvider.GetRequiredService<ITokenStorage>();
    private IHttpContextAccessor HttpContextAccessor => _httpContextAccessor ??= serviceProvider.GetRequiredService<IHttpContextAccessor>();

    private const string FileShareName = "downloads";
    /// <inheritdoc />
    [McpServerTool(Name = "download_file_from_onedrive_url", Title = "Download File from OneDrive URL")]
    [Description("Downloads a file from a given OneDrive sharing URL and saves it to a shared location.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL for the file to download.")] string sharingUrl,
        [Description("Optional: User ID to use cached OAuth token. If not provided, will use current session user.")] string? userId = null)
    {
        var result = new OneDriveDownloadResult();

        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync called ===");
            Logger.LogInformation("sharingUrl: {SharingUrl}", sharingUrl);
            Logger.LogInformation("userId: {UserId}", userId ?? "null");

            // 1. Validate the sharing URL.
            if (string.IsNullOrWhiteSpace(sharingUrl) || !Uri.TryCreate(sharingUrl, UriKind.Absolute, out var uri))
            {
                result.ErrorMessage = "Invalid URL format. Please provide a valid OneDrive sharing URL.";
                Logger.LogWarning("Invalid URL format provided: {SharingUrl}", sharingUrl);
                return result;
            }

            string host = uri.Host.ToLowerInvariant();
            if (!host.EndsWith("1drv.ms") && !host.EndsWith("onedrive.live.com") && !host.EndsWith("sharepoint.com"))
            {
                result.ErrorMessage = "The provided URL is not a recognized OneDrive or SharePoint sharing URL.";
                Logger.LogWarning("Unrecognized host for sharing URL: {Host}", host);
                return result;
            }
            // 2. Convert the sharing URL to a sharing token for the Graph API.
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string sharingToken = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
            Logger.LogInformation("Generated sharing token for URL");

            // 3. Get the DriveItem using the sharing token.
            var driveItem = await GraphServiceClient.Shares[sharingToken].DriveItem.Request().GetAsync();

            if (driveItem == null || driveItem.File == null)
            {
                result.ErrorMessage = "Could not retrieve file information from the sharing URL. The URL might be invalid or the file may have been removed.";
                Logger.LogWarning("DriveItem not found or is not a file for sharing URL: {SharingUrl}", sharingUrl);
                return result;
            }

            Logger.LogInformation("Successfully retrieved DriveItem: {FileName}", driveItem.Name);

            // 4. Download the file content using the DriveItem details.
            var driveId = driveItem.ParentReference?.DriveId;
            var itemId = driveItem.Id;

            if (string.IsNullOrEmpty(driveId) || string.IsNullOrEmpty(itemId))
            {
                result.ErrorMessage = "Could not extract necessary identifiers from the shared file.";
                Logger.LogWarning("DriveId or ItemId missing for shared file: {SharingUrl}", sharingUrl);
                return result;
            }

            Logger.LogInformation("DriveId: {DriveId}, ItemId: {ItemId}", driveId, itemId);

            using var contentStream = await GraphServiceClient.Drives[driveId].Items[itemId].Content.Request().GetAsync();
            if (contentStream == null)
            {
                result.ErrorMessage = $"Could not retrieve content for file: {driveItem.Name}";
                Logger.LogWarning("Content stream is null for file: {FileName}", driveItem.Name);
                return result;
            }

            Logger.LogInformation("Successfully retrieved content stream");

            // 5. Process the stream and upload to Azure File Share.
            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reset stream position for upload

            var connectionString = Configuration["FileShareConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                result.ErrorMessage = "File share connection string is not configured.";
                Logger.LogError("FileShareConnectionString is not found in configuration.");
                return result;
            }

            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();

            var fileClient = shareClient.GetRootDirectoryClient().GetFileClient(driveItem.Name);
            await fileClient.UploadAsync(memoryStream);

            result.FileName = driveItem.Name;
            result.DownloadPath = fileClient.Path;

            Logger.LogInformation("File '{FileName}' downloaded and uploaded to '{FilePath}' successfully.", result.FileName, result.DownloadPath);
        }
        catch (ServiceException ex)
        {
            Logger.LogError(ex, "Error downloading file from OneDrive sharing URL: {ErrorMessage}", ex.Message);
            if ((System.Net.HttpStatusCode)ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                result.ErrorMessage = $"File not found or sharing URL is invalid: {sharingUrl}";
            }
            else if ((System.Net.HttpStatusCode)ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                result.ErrorMessage = $"Access denied to file via sharing URL. Ensure the logged-in user has permissions or the link is public: {sharingUrl}";
            }
            else
            {
                result.ErrorMessage = $"OneDrive API error: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred while downloading file from sharing URL: {ErrorMessage}", ex.Message);
            result.ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }

        return result;
    }
}
