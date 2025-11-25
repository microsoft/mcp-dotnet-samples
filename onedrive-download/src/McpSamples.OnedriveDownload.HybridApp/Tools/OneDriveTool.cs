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

            // 6. SAS URL 생성 (인코딩된 파일명으로 직접 URL 구성)
            // fileClient의 경로는 원본 파일명이므로, Share 레벨 SAS를 사용하고 URL에 인코딩된 파일명 추가
            var sasBuilder = new ShareSasBuilder(ShareFileSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1))
            {
                Protocol = SasProtocol.Https
            };

            // Share 레벨 SAS 토큰 생성 (FilePath 없음)
            string sasToken = shareClient.GenerateSasUri(sasBuilder).Query;

            // 인코딩된 파일명으로 전체 URL 구성
            string encodedFileName = Uri.EscapeDataString(fileName);
            string baseUri = shareClient.Uri.AbsoluteUri;
            string sasUri = $"{baseUri}/{encodedFileName}{sasToken}";

            Console.WriteLine($"✅ Generated SAS URL: {sasUri}");

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
