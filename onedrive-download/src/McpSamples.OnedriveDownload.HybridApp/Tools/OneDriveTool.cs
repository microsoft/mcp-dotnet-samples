using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Sas;
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

                // Always use Graph API for downloads (more reliable)
                // If itemId is a URL (1drv.ms), we'll use it but append ?download=1 for redirect handling
                string contentUrl;
                string? graphItemId = null;

                // Configure HttpClient to follow redirects and handle file downloads properly
                var handler = new System.Net.Http.HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5
                };

                if (itemId != null && itemId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // itemId가 실제로 URL인 경우 (기타 공유 링크)
                    // 직접 이 URL로 요청 (리다이렉트 자동 처리)
                    contentUrl = itemId;
                    Logger.LogInformation("Using direct URL for download: {ContentUrl}", contentUrl);
                }
                else
                {
                    // itemId를 추출한 경우 또는 1drv.ms에서 추출한 경우 Graph API 사용
                    graphItemId = itemId;
                    contentUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{itemId}/content";
                    Logger.LogInformation("Downloading from Graph API endpoint: {ContentUrl}", contentUrl);
                }

                using var downloadClient = new System.Net.Http.HttpClient(handler);
                downloadClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // 1drv.ms URL은 공개 링크이므로 Authorization 헤더 불필요
                // Graph API는 Authorization 헤더 필수
                if (itemId.StartsWith("http", StringComparison.OrdinalIgnoreCase) == false)
                {
                    downloadClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                }
                downloadClient.Timeout = TimeSpan.FromMinutes(5); // Allow more time for large files

                var response = await downloadClient.GetAsync(contentUrl);

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

                // 파일명 추출 시도
                fileName = ExtractFileName(response, uri!);

                // Graph API itemId가 있으면 파일 메타데이터를 조회해서 실제 파일명 가져오기
                if (!string.IsNullOrEmpty(graphItemId))
                {
                    try
                    {
                        var metadataUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{graphItemId}";
                        var metadataResponse = await httpClient.GetAsync(metadataUrl);
                        if (metadataResponse.IsSuccessStatusCode)
                        {
                            var metadataContent = await metadataResponse.Content.ReadAsStringAsync();
                            Logger.LogInformation("File metadata response: {Metadata}", metadataContent);

                            // JSON 파싱으로 name 필드 추출
                            try
                            {
                                using (JsonDocument doc = JsonDocument.Parse(metadataContent))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("name", out JsonElement nameElement))
                                    {
                                        var extractedName = nameElement.GetString();
                                        if (!string.IsNullOrEmpty(extractedName))
                                        {
                                            fileName = System.Net.WebUtility.HtmlDecode(extractedName);
                                            Logger.LogInformation("Extracted filename from metadata: {FileName}", fileName);
                                        }
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                Logger.LogWarning(ex, "Failed to parse metadata JSON, using extracted filename");
                            }
                        }
                        else
                        {
                            Logger.LogWarning("Failed to fetch metadata. Status: {StatusCode}", metadataResponse.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to fetch file metadata, using extracted filename");
                    }
                }

                // If filename still looks like an itemId, use default with extension
                if (fileName.StartsWith("s!", StringComparison.OrdinalIgnoreCase) || fileName.Contains("!"))
                {
                    // Content-Type에서 확장자 추출
                    string extension = ".bin";
                    if (response.Content.Headers.ContentType?.MediaType != null)
                    {
                        extension = GetExtensionFromContentType(response.Content.Headers.ContentType.MediaType);
                    }
                    fileName = $"downloaded_file_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
                    Logger.LogInformation("Filename appears to be itemId, using default with extension: {FileName}", fileName);
                }

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

            // Step 5: Azure File Share에 업로드
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
    /// Azure File Share에 파일 업로드 (Connection String 사용)
    /// </summary>
    private async Task<(bool Success, string FileName, string Path, string? ErrorMessage)> UploadToFileShareAsync(Stream fileStream, string fileName)
    {
        try
        {
            Logger.LogInformation("=== Starting File Share Upload ===");
            Logger.LogInformation("Attempting to upload file to File Share: {FileName}", fileName);

            var connectionString = Configuration["FileShareConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                Logger.LogError("FileShareConnectionString is not configured");
                return (false, fileName, "", "File share connection string is not configured.");
            }

            Logger.LogInformation("Using File Share connection string (masked for security)");

            // Parse connection string to extract account name and key
            var parts = connectionString.Split(';');
            string accountName = "";
            string accountKey = "";
            string endpointSuffix = "core.windows.net";

            foreach (var part in parts)
            {
                if (part.StartsWith("AccountName="))
                    accountName = part.Substring("AccountName=".Length);
                else if (part.StartsWith("AccountKey="))
                    accountKey = part.Substring("AccountKey=".Length);
                else if (part.StartsWith("EndpointSuffix="))
                    endpointSuffix = part.Substring("EndpointSuffix=".Length);
            }

            Logger.LogInformation("Parsed account: {AccountName}", accountName);

            // Create ShareClient from URI and credential
            ShareClient shareClient;
            var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);
            try
            {
                var shareUri = new Uri($"https://{accountName}.file.{endpointSuffix}/downloads");
                shareClient = new ShareClient(shareUri, credential);
                Logger.LogInformation("ShareClient created successfully for URI: {Uri}", shareUri);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating ShareClient: {ErrorMessage}", ex.Message);
                throw;
            }

            // Check if share exists, create if not
            Logger.LogInformation("Checking if file share exists...");
            try
            {
                var shareExists = await shareClient.ExistsAsync();
                Logger.LogInformation("File share exists: {Exists}", shareExists.Value);

                if (!shareExists.Value)
                {
                    Logger.LogInformation("Creating file share...");
                    await shareClient.CreateAsync();
                    Logger.LogInformation("File share created successfully");
                }
                else
                {
                    Logger.LogInformation("File share already exists");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking/creating file share: {ErrorMessage}", ex.Message);
                throw;
            }

            // Get directory reference
            var rootDirClient = shareClient.GetRootDirectoryClient();

            // Upload file
            fileStream.Position = 0;
            long fileSize = fileStream.Length;
            Logger.LogInformation("Uploading file to file share: {FileName}, Size: {FileSize} bytes", fileName, fileSize);
            var fileClient = rootDirClient.GetFileClient(fileName);

            // Delete existing file if it exists
            try
            {
                await fileClient.DeleteAsync();
                Logger.LogInformation("Deleted existing file: {FileName}", fileName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Logger.LogInformation("File does not exist, creating new file: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error deleting existing file, will continue with upload");
            }

            // Upload file - create file first, then upload content
            Logger.LogInformation("Uploading file content, size: {FileSize} bytes", fileSize);

            // Step 1: Create the file with the correct size
            Logger.LogInformation("Step 1: Creating file...");
            try
            {
                await fileClient.CreateAsync(fileSize);
                Logger.LogInformation("File created successfully");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 409)
            {
                Logger.LogInformation("File already exists (409), will overwrite");
                // File already exists, that's okay
            }

            // Step 2: Read entire file into byte array to ensure complete upload
            Logger.LogInformation("Step 2: Reading file from stream...");
            fileStream.Position = 0;
            byte[] fileBytes = new byte[fileSize];
            int bytesRead = await fileStream.ReadAsync(fileBytes, 0, (int)fileSize);
            Logger.LogInformation("Read {BytesRead} bytes from stream", bytesRead);

            if (bytesRead != fileSize)
            {
                Logger.LogWarning("WARNING: Bytes read ({BytesRead}) does not match expected size ({FileSize})", bytesRead, fileSize);
            }

            // Step 3: Upload bytes
            Logger.LogInformation("Step 3: Uploading {ByteCount} bytes to file...", fileBytes.Length);
            using (var uploadStream = new MemoryStream(fileBytes))
            {
                uploadStream.Position = 0;
                await fileClient.UploadAsync(uploadStream);
            }
            Logger.LogInformation("=== File Share upload successful ===");

            // Verify upload by checking file properties
            try
            {
                var properties = await fileClient.GetPropertiesAsync();
                Logger.LogInformation("File verified - Size: {FileSize} bytes", properties.Value.ContentLength);

                if (properties.Value.ContentLength != fileSize)
                {
                    Logger.LogWarning("WARNING: Uploaded file size ({UploadedSize}) does not match original size ({OriginalSize})",
                        properties.Value.ContentLength, fileSize);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to verify file properties");
            }

            // Generate SAS URI for public access (valid for 24 hours)
            try
            {
                var sasBuilder = new Azure.Storage.Sas.ShareSasBuilder
                {
                    ShareName = "downloads",
                    FilePath = fileName,
                    Resource = "f", // "f" for file
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
                };

                // Add permissions: Read
                sasBuilder.SetPermissions(Azure.Storage.Sas.ShareFileSasPermissions.Read);

                var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
                var fileUri = $"{fileClient.Uri.AbsoluteUri}?{sasToken}";
                Logger.LogInformation("File URI with SAS: {FileUri}", fileUri);

                return (true, fileName, fileUri, null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to generate SAS URI, returning file URI without SAS");
                var fileUri = fileClient.Uri.AbsoluteUri;
                Logger.LogInformation("File URI without SAS: {FileUri}", fileUri);
                return (true, fileName, fileUri, null);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "=== Failed to upload file to File Share: {ErrorMessage} ===", ex.Message);
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

            // 1drv.ms 단축 URL 처리 - itemId 추출
            if (uri.Host.EndsWith("1drv.ms", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Detected 1drv.ms shortened URL, extracting itemId");
                // 1drv.ms URL 형식: https://1drv.ms/u/s!ABC123... 또는 https://1drv.ms/f/s!ABC123...
                // s! 다음의 부분이 encoded itemId
                var path = uri.AbsolutePath; // /u/s!ABC123... 또는 /f/s!ABC123...

                if (path.Contains("s!"))
                {
                    var encodedItemId = path.Substring(path.IndexOf("s!"));
                    Logger.LogInformation("Extracted encoded itemId from 1drv.ms URL: {EncodedItemId}", encodedItemId);
                    return encodedItemId; // Graph API에서 사용 가능한 형식
                }
                else
                {
                    Logger.LogWarning("Could not extract itemId pattern from 1drv.ms URL");
                    return url; // fallback: URL 자체 사용
                }
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

        // Try to extract extension from Content-Type header
        string extension = ".bin"; // 기본 확장자
        if (response.Content.Headers.ContentType?.MediaType != null)
        {
            var contentType = response.Content.Headers.ContentType.MediaType;
            extension = GetExtensionFromContentType(contentType);
        }

        // Default filename with extension
        return $"downloaded_file_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
    }

    /// <summary>
    /// MIME type을 파일 확장자로 변환
    /// </summary>
    private string GetExtensionFromContentType(string contentType)
    {
        return contentType?.ToLower() switch
        {
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "audio/mpeg" => ".mp3",
            "application/zip" => ".zip",
            "application/json" => ".json",
            "text/html" => ".html",
            "text/xml" or "application/xml" => ".xml",
            _ => ".bin"
        };
    }
}
