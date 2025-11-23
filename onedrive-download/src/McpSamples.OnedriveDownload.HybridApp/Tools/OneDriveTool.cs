using System.ComponentModel;
using System.Text;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;
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

            // 1. 저장할 경로 (Bicep에서 마운트한 경로)
            // 로컬 테스트를 위해 기본값도 설정
            string mountPath = Environment.GetEnvironmentVariable("DOWNLOAD_DIR") ?? "/app/wwwroot/downloads";

            // 폴더가 없으면 생성 (로컬 테스트용)
            if (!Directory.Exists(mountPath)) Directory.CreateDirectory(mountPath);

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
            string saveFilePath = Path.Combine(mountPath, fileName);

            Logger.LogInformation($"Saving to: {saveFilePath}");

            // 3. 파일 저장 (그냥 파일 시스템에 씁니다)
            // Bicep이 Azure Storage랑 연결해놨기 때문에, 여기에 쓰면 자동으로 클라우드에 영구 저장됩니다.
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            using (var fileStream = File.Create(saveFilePath))
            {
                await contentStream.CopyToAsync(fileStream);
            }

            // 4. ★ URL 생성 ★
            // Bicep에서 '/app/wwwroot/downloads' -> URL상 '/downloads' 로 매핑됨
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
