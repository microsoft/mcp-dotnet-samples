using System.ComponentModel;
using System.Text;
using Microsoft.Graph;
using ModelContextProtocol.Server;
using McpSamples.OnedriveDownload.HybridApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Sas;

namespace McpSamples.OnedriveDownload.HybridApp.Tools;

public class OneDriveDownloadResult
{
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SavedLocation { get; set; }
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
    private IConfiguration? _configuration;

    private ILogger<OneDriveTool> Logger => _logger ??= serviceProvider.GetRequiredService<ILogger<OneDriveTool>>();
    private IUserAuthenticationService UserAuthService => _userAuthService ??= serviceProvider.GetRequiredService<IUserAuthenticationService>();
    private IConfiguration Configuration => _configuration ??= serviceProvider.GetRequiredService<IConfiguration>();

    [McpServerTool(Name = "download_file_from_onedrive_url", Title = "Download File from OneDrive URL")]
    [Description("Downloads a file from OneDrive, saves to Azure Storage, and returns a public download link (SAS).")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL")] string sharingUrl)
    {
        Console.WriteLine("@@@ ONEDRIVETOOL (SAS Mode) STARTED @@@");

        try
        {
            // 1. 연결 문자열 확인
            var connectionString = Configuration["AZURE_STORAGE_CONNECTION_STRING"];
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING 환경 변수가 없습니다.");

            // 2. 인증 및 GraphClient
            var (graphClient, authError) = await UserAuthService.GetPersonalOneDriveGraphClientAsync();
            if (graphClient == null)
                return new OneDriveDownloadResult { ErrorMessage = authError ?? "Auth Error" };

            // 3. 메타데이터 조회
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

            var driveItem = await graphClient.Shares[encodedUrl].DriveItem.Request().GetAsync();
            string fileName = driveItem.Name;
            long fileSize = driveItem.Size ?? 0;

            // 4. Azure File Share 업로드 준비
            string shareName = "downloads";
            var shareClient = new ShareClient(connectionString, shareName);
            await shareClient.CreateIfNotExistsAsync();

            var directoryClient = shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient(fileName);

            // 5. 업로드 (있으면 덮어쓰기)
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            {
                await fileClient.CreateAsync(fileSize); // 파일 크기 할당
                await fileClient.UploadAsync(contentStream); // 내용 전송
            }

            Console.WriteLine($"✅ Uploaded to Azure: {fileName}");

            // ------------------------------------------------------------------
            // [수정] APIM 말고 Function App 진짜 주소로 링크 만들기
            // ------------------------------------------------------------------

            // 1. Function App의 실제 호스트명 가져오기 (Azure 환경 변수)
            string funcHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
                                  ?? "localhost:7071"; // 로컬 테스트용 기본값

            // 2. HTTPS 프로토콜 붙이기
            string funcBaseUrl = $"https://{funcHostname}";

            // 3. 다운로드 링크 조합 (이제 APIM 주소가 아니라 func 주소가 됩니다)
            // 예: https://func-onedrive-download-....azurewebsites.net/download?file=abc.pdf
            string sasUri = $"{funcBaseUrl}/download?file={Uri.EscapeDataString(fileName)}";

            Console.WriteLine($"🔗 Created Direct Function Link: {sasUri}");

            return new OneDriveDownloadResult
            {
                FileName = fileName,
                DownloadUrl = sasUri,
                SavedLocation = $"Azure File Share: {shareName}/{fileName}",
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            Console.WriteLine($"❌ Error: {msg}");
            return new OneDriveDownloadResult { ErrorMessage = msg };
        }
    }
}
