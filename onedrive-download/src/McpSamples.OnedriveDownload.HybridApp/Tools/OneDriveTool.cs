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
    private IConfiguration? _configuration;
    private ILogger<OneDriveTool>? _logger;

    private IConfiguration Configuration => _configuration ??= serviceProvider.GetRequiredService<IConfiguration>();
    private ILogger<OneDriveTool> Logger => _logger ??= serviceProvider.GetRequiredService<ILogger<OneDriveTool>>();

    private const string FileShareName = "downloads";

    /// <inheritdoc />
    [McpServerTool(Name = "download_file_from_onedrive_url", Title = "Download File from OneDrive URL")]
    [Description("Downloads a file from a given OneDrive sharing URL using authenticated user's delegated credentials and saves it to a shared location.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL (e.g., https://1drv.ms/u/s!ABC... or https://onedrive.live.com/embed?resid=...)")] string sharingUrl)
    {
        var result = new OneDriveDownloadResult();

        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync called ===");
            Logger.LogInformation("sharingUrl: {SharingUrl}", sharingUrl);

            // Step 1: Azure 자격증명에서 토큰 자동 획득 (azd auth login 포함)
            var userAuthService = serviceProvider.GetRequiredService<IUserAuthenticationService>();
            var accessToken = await userAuthService.GetCurrentUserAccessTokenAsync();
            Logger.LogInformation("User token status: {TokenStatus}", string.IsNullOrEmpty(accessToken) ? "NOT FOUND" : "FOUND");

            // 사용자 토큰 확인
            if (string.IsNullOrEmpty(accessToken))
            {
                result.ErrorMessage = "User not authenticated. Please run 'azd auth login' first.";
                Logger.LogWarning("No user token found from Azure credentials");
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

            // Step 2.5: 파일 경로 확인
            if (isFilePath)
            {
                result.ErrorMessage = "File path access is not yet supported. Please use OneDrive sharing URL.";
                Logger.LogWarning("File path not supported: {FilePath}", sharingUrl);
                return result;
            }

            // Step 3: Graph API를 통한 파일 다운로드
            Stream? fileContent = null;
            string fileName = "unknown_file";

            try
            {
                Logger.LogInformation("Attempting to download via Graph API with user token");

                // 공유 URL에서 itemId 추출
                var itemId = ExtractItemIdFromUrl(sharingUrl);

                if (string.IsNullOrEmpty(itemId))
                {
                    result.ErrorMessage = "Could not extract file ID from sharing URL.";
                    Logger.LogWarning("Failed to extract item ID from URL: {SharingUrl}", sharingUrl);
                    return result;
                }

                Logger.LogInformation("Accessing file via Graph API with user token");

                // /content 엔드포인트로 파일 다운로드 (사용자 토큰 포함)
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // 1drv.ms URL인 경우 직접 사용, 아니면 /me/drive/items/{itemId}/content 사용
                string contentUrl;
                if (itemId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // itemId가 실제로 URL인 경우 (1drv.ms)
                    contentUrl = itemId;
                    Logger.LogInformation("Using direct sharing URL: {ContentUrl}", contentUrl);
                }
                else
                {
                    // itemId를 추출한 경우 Graph API 사용
                    contentUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{itemId}/content";
                    Logger.LogInformation("Downloading from Graph API endpoint: {ContentUrl}", contentUrl);
                }

                var response = await httpClient.GetAsync(contentUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Failed to download file content. Status: {StatusCode}", response.StatusCode);
                    var errorContent = await response.Content.ReadAsStringAsync();

                    if (errorContent.Contains("SPO license") || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        result.ErrorMessage = "Tenant does not have a SharePoint Online (SPO) license. " +
                            "Please verify your license or use a public sharing link.";
                    }
                    else
                    {
                        result.ErrorMessage = $"Failed to download file. HTTP Status: {response.StatusCode}";
                    }
                    return result;
                }

                // 파일명 추출 (Content-Disposition 헤더 또는 생성)
                fileName = ExtractFileName(response, uri!);
                Logger.LogInformation("Final filename: {FileName}", fileName);

                // 파일 내용 다운로드
                fileContent = new MemoryStream();
                await response.Content.CopyToAsync(fileContent);
                fileContent.Position = 0;

                Logger.LogInformation("Successfully downloaded file via Graph API with user token. File name: {FileName}", fileName);
            }
            catch (Exception ex) when (ex.Message.Contains("BadRequest") || ex.Message.Contains("SPO license"))
            {
                Logger.LogError(ex, "SPO license error: {ErrorMessage}", ex.Message);
                result.ErrorMessage = "Tenant does not have a SharePoint Online (SPO) license. " +
                    "Please acquire SPO license or ensure it's properly assigned.";
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to download file via Graph API: {ErrorMessage}", ex.Message);
                result.ErrorMessage = $"Failed to download file: {ex.Message}";
                return result;
            }

            // Step 4: 파일 내용 확인
            if (fileContent == null)
            {
                result.ErrorMessage = "Failed to download file. File content is empty or file does not exist.";
                Logger.LogWarning("File content is null after Graph API download");
                return result;
            }

            Logger.LogInformation("Successfully retrieved file content via Graph API. File name: {FileName}", fileName);

            // Step 5: Azure File Share에 업로드 (선택사항)
            var uploadResult = await UploadToFileShareAsync(fileContent, fileName);
            if (uploadResult.Success)
            {
                result.FileName = uploadResult.FileName;
                result.DownloadPath = uploadResult.Path;
                Logger.LogInformation("File '{FileName}' successfully downloaded and uploaded to File Share", fileName);
            }
            else
            {
                // File Share 업로드 실패해도 파일 다운로드는 성공한 것으로 반환
                // (File Share가 선택사항이므로)
                result.FileName = fileName;
                result.DownloadPath = $"File downloaded successfully but File Share upload failed: {uploadResult.ErrorMessage}";
                Logger.LogWarning("File Share upload failed but download succeeded: {Error}", uploadResult.ErrorMessage);
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

            Logger.LogInformation("Attempting to upload file to File Share: {FileShareName}/{FileName}", FileShareName, fileName);
            Logger.LogInformation("ConnectionString validation: {IsEmpty}", string.IsNullOrEmpty(connectionString));

            var shareClient = new ShareClient(connectionString, FileShareName);

            // Check if share exists
            bool shareExists = false;
            try
            {
                shareExists = await shareClient.ExistsAsync();
                Logger.LogInformation("File Share '{FileShareName}' exists: {Exists}", FileShareName, shareExists);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to check if File Share exists: {ErrorMessage}", ex.Message);
                throw;
            }

            if (!shareExists)
            {
                Logger.LogInformation("Creating File Share: {FileShareName}", FileShareName);
                try
                {
                    await shareClient.CreateAsync();
                    Logger.LogInformation("File Share created successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to create File Share: {ErrorMessage}", ex.Message);
                    throw;
                }
            }

            var rootDirClient = shareClient.GetRootDirectoryClient();
            var fileClient = rootDirClient.GetFileClient(fileName);

            // Upload file - need to set max size first, then upload
            fileStream.Position = 0;
            long fileSize = fileStream.Length;

            // Create the file with the correct size
            await fileClient.CreateAsync(fileSize);
            Logger.LogInformation("File created with size: {FileSize}", fileSize);

            // Upload the file content
            fileStream.Position = 0;
            await fileClient.UploadAsync(fileStream);

            Logger.LogInformation("File uploaded to File Share. Path: {Path}", fileClient.Path);
            return (true, fileName, fileClient.Path, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upload file to File Share: {ErrorMessage}", ex.Message);
            return (false, fileName, "", $"Failed to upload file: {ex.Message}");
        }
    }

    /// <summary>
    /// 공유 URL에서 Item ID 추출 또는 직접 다운로드
    /// </summary>
    private string? ExtractItemIdFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);

            // 1drv.ms 단축 URL 처리 - URL 자체를 직접 사용
            if (uri.Host.EndsWith("1drv.ms", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Detected 1drv.ms shortened URL, will use direct download");
                // 1drv.ms는 ?download=1을 추가하여 직접 다운로드 가능
                return $"{url}?download=1";
            }

            // resid 파라미터에서 Item ID 추출
            // https://onedrive.live.com/embed?resid=ABC123!567 → 567
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var resid = query.Get("resid");

            if (!string.IsNullOrEmpty(resid))
            {
                // resid는 보통 ID!itemId 형식
                Logger.LogInformation("Extracted itemId from resid parameter");
                return resid.Split('!').LastOrDefault();
            }

            Logger.LogWarning("Could not extract item ID from URL");
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
    /// URL에서 파일명 추출
    /// </summary>
    private string ExtractFileNameFromUrl(Uri uri)
    {
        var path = uri.AbsolutePath;
        var lastSegment = path.Split('/').LastOrDefault();

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
