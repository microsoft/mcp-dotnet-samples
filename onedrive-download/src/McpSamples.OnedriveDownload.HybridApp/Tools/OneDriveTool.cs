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
    /// Downloads a file from a OneDrive sharing URL. If destination_path is provided, saves it there.
    /// </summary>
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
    [Description("Downloads a file from a given OneDrive sharing URL using authenticated user's delegated credentials and saves it to a shared location.")]
    public async Task<OneDriveDownloadResult> DownloadFileFromUrlAsync(
        [Description("The OneDrive sharing URL (e.g., https://1drv.ms/u/s!ABC... or https://onedrive.live.com/embed?resid=...)")] string sharingUrl,
        [Description("Optional: Specific local path to save the file (e.g., 'C:/Users/Me/Desktop' or 'C:/MyFolder/file.txt').")] string? destinationPath = null)
    {
        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync started ===");
            Logger.LogInformation("Sharing URL: {SharingUrl}", sharingUrl);
            Logger.LogInformation("Requested Destination: {Destination}", destinationPath ?? "(Default: generated folder)");

            // Step 1: OneDrive 공유 URL 인코딩 (u!...)
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
            Logger.LogInformation("✓ 인코딩된 URL: {EncodedUrl}", encodedUrl);

            // Step 2: Graph API 클라이언트 준비
            Logger.LogInformation("Graph API 클라이언트 초기화 중");
            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();

            // Step 3: 드라이브 아이템 메타데이터 조회
            Logger.LogInformation("Graph API에서 드라이브 아이템 메타데이터 조회 중");
            var driveItem = await graphClient
                .Shares[encodedUrl]
                .DriveItem
                .Request()
                .GetAsync();

            Logger.LogInformation("✓ 드라이브 아이템 조회 완료: {FileName}", driveItem.Name);

            if (driveItem.Folder != null)
            {
                return new OneDriveDownloadResult
                {
                    ErrorMessage = "공유 링크가 파일이 아닌 폴더입니다."
                };
            }

            string originalFileName = driveItem.Name;

            // Step 4: Azure File Share 준비
            Logger.LogInformation("Azure File Share 준비 중");
            var connectionString = Configuration["AZURE_STORAGE_CONNECTION_STRING"]
                                   ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                Logger.LogError("AZURE_STORAGE_CONNECTION_STRING is not configured");
                return new OneDriveDownloadResult
                {
                    ErrorMessage = "Azure Storage 연결 문자열이 구성되지 않았습니다."
                };
            }

            var shareClient = new ShareClient(connectionString, FileShareName);
            await shareClient.CreateIfNotExistsAsync();
            var directoryClient = shareClient.GetRootDirectoryClient();

            // 파일명 중복 방지를 위해 시간 추가
            string safeFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{driveItem.Name}";
            var fileClient = directoryClient.GetFileClient(safeFileName);

            Logger.LogInformation("파일명: {FileName}, 크기: {FileSize} bytes", safeFileName, driveItem.Size);

            // Step 5: Graph API에서 스트림 요청 및 Azure 업로드 (백업용)
            Logger.LogInformation("Graph API에서 파일 콘텐츠 스트림 요청 중");
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            {
                Logger.LogInformation("✓ 파일 스트림 수신 완료");
                Logger.LogInformation("Azure File Share에 파일 업로드 중: {FileName}", safeFileName);

                await fileClient.CreateAsync(driveItem.Size ?? 0);
                await fileClient.UploadAsync(contentStream);
                Logger.LogInformation("✓ Azure 파일 업로드 완료");
            }

            // =========================================================
            // [NEW Step]: 로컬 저장 경로 결정 및 다운로드 (System.IO 명시)
            // =========================================================
            string finalLocalPath = DetermineFinalLocalPath(destinationPath, originalFileName);

            Logger.LogInformation("로컬 저장 시작: {LocalPath}", finalLocalPath);

            // Azure에서 다시 다운로드하여 로컬에 저장
            ShareFileDownloadInfo download = await fileClient.DownloadAsync();

            // ★ 수정됨: File -> System.IO.File 명시
            using (FileStream stream = System.IO.File.OpenWrite(finalLocalPath))
            {
                await download.Content.CopyToAsync(stream);
                await stream.FlushAsync();
            }

            Logger.LogInformation("✅ 로컬 저장 완료!");

            // SAS URL 생성 (선택 사항)
            string sasUrl = GenerateSasUrl(connectionString, fileClient);

            return new OneDriveDownloadResult
            {
                FileName = originalFileName,
                DownloadPath = finalLocalPath,
                ErrorMessage = null
            };
        }
        catch (ServiceException svEx)
        {
            Logger.LogError(svEx, "Graph API 에러: {StatusCode} - {ErrorMessage}", svEx.StatusCode, svEx.Message);
            return new OneDriveDownloadResult
            {
                ErrorMessage = $"OneDrive 에러: {svEx.Message}"
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "파일 다운로드 실패: {ErrorMessage}", ex.Message);
            return new OneDriveDownloadResult
            {
                ErrorMessage = $"시스템 에러: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 사용자가 입력한 경로와 파일명을 기반으로 최종 저장 경로를 계산합니다.
    /// </summary>
    private string DetermineFinalLocalPath(string? requestedPath, string fileName)
    {
        // ★ 수정됨: Directory -> System.IO.Directory, Path -> System.IO.Path 명시

        // 1. 경로가 지정되지 않은 경우 -> Root/generated 폴더 사용
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            string currentDir = System.IO.Directory.GetCurrentDirectory();
            string generatedDir = System.IO.Path.Combine(currentDir, "generated");

            if (!System.IO.Directory.Exists(generatedDir))
            {
                System.IO.Directory.CreateDirectory(generatedDir);
                Logger.LogInformation("'generated' 폴더 생성됨: {Path}", generatedDir);
            }

            return System.IO.Path.Combine(generatedDir, fileName);
        }

        // 2. 경로가 지정된 경우
        // 2-1. 기존에 존재하는 "폴더" 경로인 경우 -> 그 안에 파일명으로 저장
        if (System.IO.Directory.Exists(requestedPath))
        {
            return System.IO.Path.Combine(requestedPath, fileName);
        }

        // 2-2. 폴더가 없는데 확장자가 있는 파일 경로처럼 보이는 경우
        string? parentDir = System.IO.Path.GetDirectoryName(requestedPath);
        if (!string.IsNullOrEmpty(parentDir) && !System.IO.Directory.Exists(parentDir))
        {
            // 부모 폴더가 없으면 생성 (사용자 편의)
            System.IO.Directory.CreateDirectory(parentDir);
        }

        // 입력된 경로를 그대로 파일 경로로 사용
        return requestedPath;
    }

    private string GenerateSasUrl(string connectionString, ShareFileClient fileClient)
    {
        try
        {
            var accountName = ExtractAccountNameFromConnectionString(connectionString);
            var accountKey = ExtractAccountKeyFromConnectionString(connectionString);

            if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey)) return "";

            var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);
            var sasBuilder = new ShareSasBuilder
            {
                ShareName = FileShareName,
                FilePath = fileClient.Name,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
            };
            sasBuilder.SetPermissions(ShareFileSasPermissions.Read);

            return $"{fileClient.Uri}?{sasBuilder.ToSasQueryParameters(credential)}";
        }
        catch
        {
            return fileClient.Uri.AbsoluteUri;
        }
    }

    private static string? ExtractAccountNameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
                return part.Substring("AccountName=".Length);
        }
        return null;
    }

    private static string? ExtractAccountKeyFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
                return part.Substring("AccountKey=".Length);
        }
        return null;
    }
}
