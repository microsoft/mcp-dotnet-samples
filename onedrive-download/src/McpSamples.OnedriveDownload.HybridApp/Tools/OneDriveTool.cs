using System.ComponentModel;
using System.Text;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace McpSamples.OnedriveDownload.HybridApp.Tools;

public class OneDriveDownloadResult
{
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IOneDriveTool
{
    Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(string sharingUrl);
}

[McpServerToolType]
public class OneDriveTool(IServiceProvider serviceProvider) : IOneDriveTool
{
    private ILogger<OneDriveTool>? _logger;
    private IUserAuthenticationService? _userAuthService;

    private ILogger<OneDriveTool> Logger => _logger ??= serviceProvider.GetRequiredService<ILogger<OneDriveTool>>();
    private IUserAuthenticationService UserAuthService => _userAuthService ??= serviceProvider.GetRequiredService<IUserAuthenticationService>();

    [McpServerTool(Name = "download_file_from_onedrive_url", Title = "Download File from OneDrive URL")]
    [Description("Downloads a file from OneDrive and returns a direct download URL.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL")] string sharingUrl)
    {
        try
        {
            Logger.LogInformation("=== Download Request Started ===");

            // 1. 저장할 경로 (환경변수 또는 기본값)
            string mountPath = Environment.GetEnvironmentVariable("DOWNLOAD_DIR") ?? "/app/wwwroot/downloads";

            // ★ 수정: Directory -> System.IO.Directory (모호함 해결)
            if (!System.IO.Directory.Exists(mountPath))
            {
                System.IO.Directory.CreateDirectory(mountPath);
            }

            // 2. Graph API로 파일 정보 가져오기
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();
            var driveItem = await graphClient.Shares[encodedUrl].DriveItem.Request().GetAsync();

            if (driveItem.Folder != null)
            {
                return new OneDriveDownloadResult { ErrorMessage = "Folders are not supported." };
            }

            string fileName = driveItem.Name;
            // ★ 수정: Path -> System.IO.Path
            string saveFilePath = System.IO.Path.Combine(mountPath, fileName);

            Logger.LogInformation($"Saving to: {saveFilePath}");

            // 3. 파일 저장
            // ★ 수정: File -> System.IO.File (모호함 해결)
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            using (var fileStream = System.IO.File.Create(saveFilePath))
            {
                await contentStream.CopyToAsync(fileStream);
            }

            // 4. URL 생성
            string downloadUrl = $"/downloads/{Uri.EscapeDataString(fileName)}";

            Logger.LogInformation($"File ready at: {downloadUrl}");

            return new OneDriveDownloadResult
            {
                FileName = fileName,
                DownloadUrl = downloadUrl,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Download Failed");
            return new OneDriveDownloadResult { ErrorMessage = ex.Message };
        }
    }
}
