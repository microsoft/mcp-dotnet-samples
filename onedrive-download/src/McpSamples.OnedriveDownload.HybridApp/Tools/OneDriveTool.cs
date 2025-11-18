using System.ComponentModel;
using System.Linq;
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
    /// Downloads a file from a OneDrive sharing URL using authenticated user's credentials and saves it to a file share.
    /// </summary>
    /// <param name="sharingUrl">The OneDrive sharing URL.</param>
    /// <returns>Returns <see cref="OneDriveDownloadResult"/> instance.</returns>
    Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(string sharingUrl);
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
    [Description("Downloads a file from a given OneDrive sharing URL or file path using authenticated user's credentials and saves it to a shared location.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL or file path (e.g., https://1drv.ms/u/s!ABC... or /me/drive/root:/Documents/file.pdf)")] string sharingUrl)
    {
        var result = new OneDriveDownloadResult();

        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync called ===");
            Logger.LogInformation("sharingUrl: {SharingUrl}", sharingUrl);

            // Step 1: 현재 사용자 확인 (OAuth 인증 필수)
            var userId = await TokenStorage.GetCurrentUserIdAsync();
            Logger.LogInformation("Current user: {UserId}", string.IsNullOrEmpty(userId) ? "NULL (not authenticated)" : userId);

            // 사용자 인증 여부 확인
            if (string.IsNullOrEmpty(userId))
            {
                result.ErrorMessage = "User not authenticated. Please login first by visiting /auth/login";
                Logger.LogWarning("User not authenticated for file download");
                return result;
            }

            // Step 2: URL 검증
            if (string.IsNullOrWhiteSpace(sharingUrl))
            {
                result.ErrorMessage = "Invalid URL format. Please provide a valid OneDrive sharing URL.";
                Logger.LogWarning("Empty URL provided");
                return result;
            }

            // URL이 파일 경로인지 공유 URL인지 확인
            bool isFilePath = sharingUrl.StartsWith("/") || sharingUrl.Contains(".itemPath");
            bool isSharingUrl = Uri.TryCreate(sharingUrl, UriKind.Absolute, out var uri);

            if (!isFilePath && !isSharingUrl)
            {
                result.ErrorMessage = "Invalid URL format. Please provide a valid OneDrive sharing URL or file path.";
                Logger.LogWarning("Invalid URL format provided: {SharingUrl}", sharingUrl);
                return result;
            }

            // 공유 URL인 경우 도메인 검증
            if (isSharingUrl && uri != null)
            {
                string host = uri.Host.ToLowerInvariant();
                if (!host.EndsWith("1drv.ms") && !host.EndsWith("onedrive.live.com") &&
                    !host.EndsWith("sharepoint.com") && !host.EndsWith("microsoft.com"))
                {
                    result.ErrorMessage = "The provided URL is not a recognized OneDrive or SharePoint sharing URL.";
                    Logger.LogWarning("Unrecognized host for sharing URL: {Host}", host);
                    return result;
                }
            }

            Logger.LogInformation("URL validated successfully. IsFilePath: {IsFilePath}", isFilePath);

            // Step 3: HTTP를 통한 파일 다운로드
            // 사용자가 이미 인증되었으므로, 공유 링크는 바로 접근 가능
            Logger.LogInformation("Downloading file via HTTP (user authenticated)");

            Stream? fileContent = null;
            string fileName = "unknown_file";

            var httpResult = await DownloadByHttpAsync(sharingUrl);
            fileContent = httpResult.Content;
            fileName = httpResult.FileName;

            // Step 5: 파일 내용 확인
            if (fileContent == null)
            {
                result.ErrorMessage = "Failed to download file. Please ensure the sharing link is valid and you have access to it.";
                Logger.LogWarning("File content is null after download attempts");
                return result;
            }

            Logger.LogInformation("Successfully retrieved file content. File name: {FileName}", fileName);

            // Step 6: Azure File Share에 업로드
            var uploadResult = await UploadToFileShareAsync(fileContent, fileName);
            if (uploadResult.Success)
            {
                result.FileName = uploadResult.FileName;
                result.DownloadPath = uploadResult.Path;
                Logger.LogInformation("File '{FileName}' successfully downloaded and uploaded to File Share", fileName);
            }
            else
            {
                result.ErrorMessage = uploadResult.ErrorMessage;
                Logger.LogError("Failed to upload file to File Share: {Error}", uploadResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            result.ErrorMessage = $"An unexpected error occurred: {ex.Message}";

            // SPO 라이선스 에러 감지
            if (ex.Message.Contains("SPO license") || ex.Message.Contains("BadRequest"))
            {
                result.ErrorMessage = "Tenant does not have a SharePoint Online (SPO) license. " +
                    "Please acquire SPO license or use public sharing links.";
            }
        }

        return result;
    }


    /// <summary>
    /// HTTP를 통해 파일 다운로드 (공유 링크용)
    /// </summary>
    private async Task<(Stream? Content, string FileName)> DownloadByHttpAsync(string sharingUrl)
    {
        try
        {
            Logger.LogInformation("Downloading file via HTTP from: {SharingUrl}", sharingUrl);

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var response = await httpClient.GetAsync(sharingUrl);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("HTTP download failed with status {StatusCode}", response.StatusCode);
                return (null, "unknown_file");
            }

            var fileName = ExtractFileName(response, new Uri(sharingUrl));
            Logger.LogInformation("HTTP download successful. File name: {FileName}", fileName);

            var memoryStream = new MemoryStream();
            await response.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return (memoryStream, fileName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to download file via HTTP");
            return (null, "unknown_file");
        }
    }


    /// <summary>
    /// Azure File Share에 파일 업로드
    /// </summary>
    private async Task<(bool Success, string FileName, string Path, string? ErrorMessage)> UploadToFileShareAsync(Stream fileStream, string fileName)
    {
        try
        {
            var connectionString = Configuration["FileShareConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                Logger.LogError("FileShareConnectionString is not configured");
                return (false, fileName, "", "File share connection string is not configured.");
            }

            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();

            var fileClient = shareClient.GetRootDirectoryClient().GetFileClient(fileName);
            await fileClient.UploadAsync(fileStream);

            Logger.LogInformation("File uploaded to File Share. Path: {Path}", fileClient.Path);
            return (true, fileName, fileClient.Path, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upload file to File Share");
            return (false, fileName, "", $"Failed to upload file: {ex.Message}");
        }
    }

    /// <summary>
    /// 공유 URL에서 Item ID 추출
    /// </summary>
    private string? ExtractItemIdFromUrl(string url)
    {
        try
        {
            // resid 파라미터에서 Item ID 추출
            // https://onedrive.live.com/embed?resid=ABC123 → ABC123
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var resid = query.Get("resid");

            if (!string.IsNullOrEmpty(resid))
            {
                // resid는 보통 ID!123 형식
                return resid.Split('!').LastOrDefault();
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to extract item ID from URL");
            return null;
        }
    }

    /// <summary>
    /// 파일 경로에서 파일명 추출
    /// </summary>
    private string ExtractFileNameFromPath(string filePath)
    {
        var lastSegment = filePath.Split('/').LastOrDefault();
        if (!string.IsNullOrEmpty(lastSegment) && lastSegment.Contains('.'))
        {
            return System.Net.WebUtility.UrlDecode(lastSegment);
        }

        return $"downloaded_file_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    }

    /// <summary>
    /// Extracts the filename from the HTTP response or URL.
    /// </summary>
    private string ExtractFileName(System.Net.Http.HttpResponseMessage response, Uri uri)
    {
        // Try to get filename from Content-Disposition header
        if (response.Content.Headers.ContentDisposition?.FileName != null)
        {
            return response.Content.Headers.ContentDisposition.FileName.Trim('"');
        }

        // Try to get filename from URL path
        string path = uri.AbsolutePath;
        if (!string.IsNullOrEmpty(path))
        {
            string lastSegment = path.Split('/').Last();
            if (!string.IsNullOrEmpty(lastSegment) && !lastSegment.Contains('?'))
            {
                return System.Net.WebUtility.UrlDecode(lastSegment);
            }
        }

        // Default filename
        return $"downloaded_file_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    }
}
