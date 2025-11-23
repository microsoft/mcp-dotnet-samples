using System.ComponentModel;
using System.Text;
using Azure.Storage.Files.Shares;
using Azure.Storage.Sas;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
    /// Downloads a file from a OneDrive sharing URL using authenticated user's credentials.
    /// Saves to Azure File Share (backup), then to specified local path or default 'generated' folder.
    /// </summary>
    /// <param name="sharingUrl">The OneDrive sharing URL (e.g., https://1drv.ms/u/s!ABC...).</param>
    /// <param name="destinationPath">
    /// Optional: Specific local path to save the file.
    /// - If null/empty: Saves to '{project_root}/generated/{filename}'
    /// - If directory path exists: Saves to '{destinationPath}/{filename}'
    /// - If file path: Saves to exact path (creates parent directories if needed)
    /// </param>
    /// <returns>Returns <see cref="OneDriveDownloadResult"/> instance.</returns>
    Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(string sharingUrl, string? destinationPath = null);
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
    [Description("Downloads a file from a given OneDrive sharing URL. If destination_path is provided, saves it there. Otherwise, saves to 'generated' folder in project root.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL (e.g., https://1drv.ms/u/s!ABC... or https://onedrive.live.com/embed?resid=...)")] string sharingUrl,
        [Description("Optional: Specific local path to save the file (e.g., 'C:/Users/Me/Desktop' or 'C:/MyFolder/file.txt'). If not provided, saves to '{project_root}/generated/' folder.")] string? destinationPath = null)
    {
        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync started ===");
            Logger.LogInformation("Sharing URL: {SharingUrl}", sharingUrl);
            Logger.LogInformation("Requested Destination: {Destination}", destinationPath ?? "(Default: generated folder)");

            // Step 1-3: URL 인코딩 & 메타데이터 조회
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();
            var driveItem = await graphClient.Shares[encodedUrl].DriveItem.Request().GetAsync();

            if (driveItem.Folder != null)
            {
                return new OneDriveDownloadResult { ErrorMessage = "공유 링크가 파일이 아닌 폴더입니다." };
            }

            string originalFileName = driveItem.Name;
            Logger.LogInformation("✓ File Info: {FileName}, Size: {FileSize} bytes", originalFileName, driveItem.Size);

            // Step 4: Azure File Share 준비
            var connectionString = Configuration["AZURE_STORAGE_CONNECTION_STRING"]
                                   ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                Logger.LogError("AZURE_STORAGE_CONNECTION_STRING is not configured");
                return new OneDriveDownloadResult { ErrorMessage = "Azure Storage 연결 문자열이 구성되지 않았습니다." };
            }

            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();
            var directoryClient = shareClient.GetRootDirectoryClient();

            // Azure용 파일명 (중복 방지 타임스탬프)
            string azureSafeFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{originalFileName}";
            var fileClient = directoryClient.GetFileClient(azureSafeFileName);

            Logger.LogInformation("Azure File Name: {FileName}", azureSafeFileName);

            // Step 5-6: Azure File Share에 업로드 (백업/감사용)
            // 중요: Graph 스트림은 한 번 읽으면 끝이므로, MemoryStream에 복사하여 Azure와 로컬 양쪽에 저장합니다.
            Logger.LogInformation("Downloading from OneDrive & uploading to Azure File Share...");

            byte[] fileContent;
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            {
                using (var memoryStream = new MemoryStream())
                {
                    await contentStream.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }
            }

            // Azure에 업로드
            await fileClient.CreateAsync(fileContent.Length);
            using (var uploadStream = new MemoryStream(fileContent))
            {
                await fileClient.UploadAsync(uploadStream);
            }
            Logger.LogInformation("✓ Azure File Share upload completed");

            // =========================================================
            // ★★★ NEW: 로컬 저장 로직 ★★★
            // =========================================================
            string finalLocalPath = DetermineFinalLocalPath(destinationPath, originalFileName);
            Logger.LogInformation("Local save path: {LocalPath}", finalLocalPath);

            // 로컬에 저장
            using (var fileStream = File.OpenWrite(finalLocalPath))
            {
                await fileStream.WriteAsync(fileContent, 0, fileContent.Length);
                await fileStream.FlushAsync();
            }
            Logger.LogInformation("✓ Local file save completed");

            // Step 7: SAS URL 생성 (Azure에서도 접근 가능하도록)
            string sasUrl = GenerateSasUrl(connectionString, fileClient);

            Logger.LogInformation("=== Download completed successfully ===");
            return new OneDriveDownloadResult
            {
                FileName = originalFileName,
                DownloadPath = finalLocalPath, // 로컬 경로 반환
                ErrorMessage = null
            };
        }
        catch (ServiceException svEx)
        {
            Logger.LogError(svEx, "Graph API Error: {StatusCode} - {ErrorMessage}", svEx.StatusCode, svEx.Message);
            return new OneDriveDownloadResult { ErrorMessage = $"OneDrive Error: {svEx.Message}" };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Download failed: {ErrorMessage}", ex.Message);
            return new OneDriveDownloadResult { ErrorMessage = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// 최종 저장 경로를 결정합니다.
    /// </summary>
    private string DetermineFinalLocalPath(string? requestedPath, string fileName)
    {
        try
        {
            // 1. 경로가 지정되지 않은 경우 -> Root/generated 폴더 사용
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                string generatedDir = Path.Combine(Directory.GetCurrentDirectory(), "generated");

                if (!Directory.Exists(generatedDir))
                {
                    Directory.CreateDirectory(generatedDir);
                    Logger.LogInformation("[Path] Created 'generated' folder: {Path}", generatedDir);
                }

                return Path.Combine(generatedDir, fileName);
            }

            // 2. 경로가 지정된 경우
            // 2-1. 기존에 존재하는 "폴더" 경로인 경우 -> 그 안에 파일명으로 저장
            if (Directory.Exists(requestedPath))
            {
                Logger.LogInformation("[Path] Using existing directory: {Path}", requestedPath);
                return Path.Combine(requestedPath, fileName);
            }

            // 2-2. 폴더가 없는데 확장자가 있는 파일 경로처럼 보이는 경우 (예: C:\data\myfile.pdf)
            string? parentDir = Path.GetDirectoryName(requestedPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                // 부모 폴더가 없으면 생성 (사용자 편의)
                Directory.CreateDirectory(parentDir);
                Logger.LogInformation("[Path] Created parent directory: {Path}", parentDir);
            }

            // 입력된 경로를 그대로 파일 경로로 사용
            Logger.LogInformation("[Path] Using requested file path: {Path}", requestedPath);
            return requestedPath;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[Path] Error determining path, falling back to generated folder");
            // 오류 발생 시 generated 폴더로 폴백
            string generatedDir = Path.Combine(Directory.GetCurrentDirectory(), "generated");
            if (!Directory.Exists(generatedDir))
            {
                Directory.CreateDirectory(generatedDir);
            }
            return Path.Combine(generatedDir, fileName);
        }
    }

    /// <summary>
    /// Azure File Share에 저장된 파일의 SAS URL을 생성합니다.
    /// </summary>
    private string GenerateSasUrl(string connectionString, ShareFileClient fileClient)
    {
        try
        {
            var accountName = ExtractAccountNameFromConnectionString(connectionString);
            var accountKey = ExtractAccountKeyFromConnectionString(connectionString);

            if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
            {
                Logger.LogWarning("[SAS] Cannot extract account info, returning file URI");
                return fileClient.Uri.AbsoluteUri;
            }

            var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);
            var sasBuilder = new ShareSasBuilder
            {
                ShareName = FileShareName,
                FilePath = fileClient.Name,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
            };
            sasBuilder.SetPermissions(ShareFileSasPermissions.Read);

            string sasUrl = $"{fileClient.Uri}?{sasBuilder.ToSasQueryParameters(credential)}";
            Logger.LogInformation("[SAS] Generated SAS URL: {SasUrl}", sasUrl);
            return sasUrl;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[SAS] Failed to generate SAS URL");
            return fileClient.Uri.AbsoluteUri;
        }
    }

    /// <summary>
    /// 연결 문자열에서 스토리지 계정 이름 추출
    /// </summary>
    private static string? ExtractAccountNameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring("AccountName=".Length);
            }
        }
        return null;
    }

    /// <summary>
    /// 연결 문자열에서 스토리지 계정 키 추출
    /// </summary>
    private static string? ExtractAccountKeyFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring("AccountKey=".Length);
            }
        }
        return null;
    }

}
