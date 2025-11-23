using System.ComponentModel;
using System.Text;
using Azure; // Response<T> 사용을 위해 필요
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models; // ★ 이 줄이 추가되었습니다! (ShareFileDownloadInfo)
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
    public string? DownloadPath { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IOneDriveTool
{
    Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(string sharingUrl, string? destinationPath = null);
}

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

    [McpServerTool(Name = "download_file_from_onedrive_url", Title = "Download File from OneDrive URL")]
    [Description("Downloads a file from a given OneDrive sharing URL using authenticated user's delegated credentials and saves it to a shared location.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL")] string sharingUrl,
        [Description("Optional: Specific local path to save the file.")] string? destinationPath = null)
    {
        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync started ===");
            Logger.LogInformation("Sharing URL: {SharingUrl}", sharingUrl);
            Logger.LogInformation("Requested Destination: {Destination}", destinationPath ?? "(Default: generated folder)");

            // Step 1: URL 인코딩
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

            // Step 2: Graph API 준비
            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();

            // Step 3: 메타데이터 조회
            var driveItem = await graphClient.Shares[encodedUrl].DriveItem.Request().GetAsync();

            if (driveItem.Folder != null)
            {
                return new OneDriveDownloadResult { ErrorMessage = "공유 링크가 파일이 아닌 폴더입니다." };
            }

            string originalFileName = driveItem.Name;

            // Step 4: Azure File Share 준비
            var connectionString = Configuration["AZURE_STORAGE_CONNECTION_STRING"]
                                   ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                return new OneDriveDownloadResult { ErrorMessage = "Azure Storage 연결 문자열 누락" };
            }

            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();
            var directoryClient = shareClient.GetRootDirectoryClient();

            string safeFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{driveItem.Name}";
            var fileClient = directoryClient.GetFileClient(safeFileName);

            // Step 5: Azure에 업로드 (백업)
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            {
                await fileClient.CreateAsync(driveItem.Size ?? 0);
                await fileClient.UploadAsync(contentStream);
            }

            // =========================================================
            // [Step 6]: 로컬 저장 (다운로드)
            // =========================================================
            string finalLocalPath = DetermineFinalLocalPath(destinationPath, originalFileName);
            Logger.LogInformation("로컬 저장 시작: {LocalPath}", finalLocalPath);

            // Azure에서 다운로드 (Response<T> 래퍼 처리)
            Response<ShareFileDownloadInfo> downloadResponse = await fileClient.DownloadAsync();
            ShareFileDownloadInfo download = downloadResponse.Value; // ★ 여기서 Value를 꺼내야 함

            using (FileStream stream = System.IO.File.OpenWrite(finalLocalPath))
            {
                await download.Content.CopyToAsync(stream);
                await stream.FlushAsync();
            }

            Logger.LogInformation("✅ 로컬 저장 완료!");

            return new OneDriveDownloadResult
            {
                FileName = originalFileName,
                DownloadPath = finalLocalPath,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "다운로드 실패");
            return new OneDriveDownloadResult { ErrorMessage = ex.Message };
        }
    }

    private string DetermineFinalLocalPath(string? requestedPath, string fileName)
    {
        // 1. 경로 미지정 -> generated 폴더
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            string currentDir = System.IO.Directory.GetCurrentDirectory();
            string generatedDir = System.IO.Path.Combine(currentDir, "generated");

            if (!System.IO.Directory.Exists(generatedDir))
            {
                System.IO.Directory.CreateDirectory(generatedDir);
            }
            return System.IO.Path.Combine(generatedDir, fileName);
        }

        // 2. 폴더 경로인 경우
        if (System.IO.Directory.Exists(requestedPath))
        {
            return System.IO.Path.Combine(requestedPath, fileName);
        }

        // 3. 파일 경로인 경우 (부모 폴더 생성)
        string? parentDir = System.IO.Path.GetDirectoryName(requestedPath);
        if (!string.IsNullOrEmpty(parentDir) && !System.IO.Directory.Exists(parentDir))
        {
            System.IO.Directory.CreateDirectory(parentDir);
        }

        return requestedPath;
    }
}
