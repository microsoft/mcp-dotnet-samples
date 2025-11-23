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

            // 1. 환경변수에서 경로 가져오기 (Bicep에서 /home/mounts/downloads로 설정됨)
            // ★ 주의: 마운트 경로는 Azure가 만들어주는 것이지, 우리가 만드는 게 아님
            string mountPath = Environment.GetEnvironmentVariable("DOWNLOAD_DIR");

            if (string.IsNullOrEmpty(mountPath))
            {
                return new OneDriveDownloadResult { ErrorMessage = "DOWNLOAD_DIR environment variable is not set." };
            }

            Logger.LogInformation($"Mount path: {mountPath}");

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
            string saveFilePath = System.IO.Path.Combine(mountPath, fileName);

            Logger.LogInformation($"Saving to: {saveFilePath}");

            // 3. 파일 저장
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            using (var fileStream = System.IO.File.Create(saveFilePath))
            {
                await contentStream.CopyToAsync(fileStream);
            }

            Logger.LogInformation($"File downloaded successfully: {fileName}");

            return new OneDriveDownloadResult
            {
                FileName = fileName,
                DownloadUrl = null,
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
