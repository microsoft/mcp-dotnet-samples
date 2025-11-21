using System.ComponentModel;
using System.Text;
using Azure.Storage.Files.Shares;
using Azure.Storage.Sas;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
/// Optimized for streaming downloads directly from Graph API.
/// </summary>
[McpServerToolType]
public class OneDriveTool(IServiceProvider serviceProvider) : IOneDriveTool
{
    private IConfiguration? _configuration;
    private ILogger<OneDriveTool>? _logger;
    private IUserAuthenticationService? _userAuthService;

    private IConfiguration Configuration => _configuration ??= serviceProvider.GetRequiredService<IConfiguration>();
    private ILogger<OneDriveTool> Logger => _logger ??= serviceProvider.GetRequiredService<ILogger<OneDriveTool>>();
    private IUserAuthenticationService UserAuthService => _userAuthService ??= serviceProvider.GetRequiredService<IUserAuthenticationService>();

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
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync started ===");
            Logger.LogInformation("Sharing URL: {SharingUrl}", sharingUrl);

            // Step 1: OneDrive 공유 URL 인코딩 (u!...)
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
            Logger.LogInformation("Encoded URL: {EncodedUrl}", encodedUrl);

            // Step 2: Personal OneDrive 토큰 획득
            Logger.LogInformation("Acquiring Personal OneDrive access token");
            var accessToken = await UserAuthService.GetPersonalOneDriveAccessTokenAsync();

            if (string.IsNullOrEmpty(accessToken))
            {
                result.ErrorMessage = "Failed to acquire Personal OneDrive token. Please ensure PERSONAL_365_REFRESH_TOKEN is set.";
                Logger.LogError("Personal OneDrive token acquisition failed");
                return result;
            }
            Logger.LogInformation("✓ Access token acquired");

            // Step 3: Graph API를 사용하여 드라이브 아이템 메타데이터 및 다운로드 URL 획득
            Logger.LogInformation("Fetching drive item metadata from Graph API");
            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();

            var driveItem = await graphClient
                .Shares[encodedUrl]
                .DriveItem
                .Request()
                .GetAsync();

            Logger.LogInformation("✓ Drive item retrieved: {FileName}", driveItem.Name);

            // Step 4: @microsoft.graph.downloadUrl 직접 다운로드 URL 추출
            if (!driveItem.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var downloadUrlObj) ||
                downloadUrlObj is not string directDownloadUrl)
            {
                result.ErrorMessage = "OneDrive download URL not found in response.";
                Logger.LogError("Failed to extract download URL from drive item");
                return result;
            }
            Logger.LogInformation("✓ Direct download URL obtained");

            // Step 5: Graph API 제공 URL로부터 직접 스트림 다운로드 (메모리 로드 없음)
            Logger.LogInformation("Starting file download stream from OneDrive");
            using var httpClient = new HttpClient();
            using var fileStream = await httpClient.GetStreamAsync(directDownloadUrl);
            Logger.LogInformation("✓ File stream opened successfully");

            // Step 6: Azure File Share에 스트리밍으로 업로드
            Logger.LogInformation("Uploading file to Azure File Share: {FileName}", driveItem.Name);
            var uploadResult = await UploadToFileShareAsync(fileStream, driveItem.Name, driveItem.Size ?? 0);

            if (!uploadResult.Success)
            {
                result.ErrorMessage = uploadResult.ErrorMessage;
                Logger.LogError("File Share upload failed: {Error}", uploadResult.ErrorMessage);
                return result;
            }

            Logger.LogInformation("✓ File uploaded successfully");
            result.FileName = uploadResult.FileName;
            result.DownloadPath = uploadResult.SasUrl;
            Logger.LogInformation("=== Download complete. SAS URL: {SasUrl}", uploadResult.SasUrl);

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Download failed with exception: {ErrorMessage}", ex.Message);
            result.ErrorMessage = $"Error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Azure File Share에 스트림을 직접 업로드 (메모리에 로드하지 않음)
    /// </summary>
    private async Task<(bool Success, string FileName, string SasUrl, string? ErrorMessage)> UploadToFileShareAsync(
        Stream fileStream,
        string fileName,
        long? fileSize)
    {
        try
        {
            Logger.LogInformation("=== Starting File Share Upload ===");
            Logger.LogInformation("File: {FileName}, Size: {FileSize} bytes", fileName, fileSize);

            var connectionString = Configuration["FileShareConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                Logger.LogError("FileShareConnectionString is not configured");
                return (false, fileName, "", "File share connection string is not configured.");
            }

            // ShareClient 생성 및 Share 존재 여부 확인
            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();
            Logger.LogInformation("✓ File share ready");

            // 루트 디렉토리 클라이언트
            var directoryClient = shareClient.GetRootDirectoryClient();

            // 파일명 정규화 (시간 추가하여 중복 방지)
            string safeFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}";
            var fileClient = directoryClient.GetFileClient(safeFileName);

            // 기존 파일 삭제 시도
            try
            {
                await fileClient.DeleteAsync();
                Logger.LogInformation("Deleted existing file");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Logger.LogInformation("File does not exist, creating new");
            }

            // 파일 생성 (크기 지정)
            Logger.LogInformation("Creating file with size: {FileSize}", fileSize ?? 0);
            await fileClient.CreateAsync(fileSize ?? 0);

            // 스트림으로 직접 업로드 (메모리 버퍼링 없음)
            Logger.LogInformation("Uploading file stream directly");
            await fileClient.UploadAsync(fileStream);
            Logger.LogInformation("✓ File uploaded successfully");

            // SAS URI 생성 (24시간 유효)
            try
            {
                // Azure.Storage.StorageSharedKeyCredential를 사용하여 SAS 생성
                var accountName = GetAccountNameFromConnectionString(connectionString);
                var accountKey = GetAccountKeyFromConnectionString(connectionString);
                var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);

                // ShareFileSasBuilder 사용
                var sasBuilder = new Azure.Storage.Files.Shares.ShareFileSasBuilder
                {
                    ShareName = FileShareName,
                    FilePath = safeFileName,
                    Resource = "f", // file
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
                };
                sasBuilder.SetPermissions(Azure.Storage.Files.Shares.ShareFileSasPermissions.Read);

                Uri sasUri = fileClient.GenerateSasUri(sasBuilder);
                Logger.LogInformation("✓ SAS URL generated");

                return (true, safeFileName, sasUri.ToString(), null);
            }
            catch (Exception saEx)
            {
                Logger.LogWarning(saEx, "Failed to generate SAS URL, returning file URL without SAS");
                return (true, safeFileName, fileClient.Uri.AbsoluteUri, null);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "File Share upload failed: {ErrorMessage}", ex.Message);
            return (false, fileName, "", $"Failed to upload file: {ex.Message}");
        }
    }

    /// <summary>
    /// 연결 문자열에서 계정 이름 추출
    /// </summary>
    private string GetAccountNameFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName="))
                return part.Substring("AccountName=".Length);
        }
        return "";
    }

    /// <summary>
    /// 연결 문자열에서 계정 키 추출
    /// </summary>
    private string GetAccountKeyFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountKey="))
                return part.Substring("AccountKey=".Length);
        }
        return "";
    }
}
