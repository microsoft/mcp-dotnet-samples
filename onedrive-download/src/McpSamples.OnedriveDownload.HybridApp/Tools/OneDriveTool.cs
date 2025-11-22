using System.ComponentModel;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        try
        {
            Logger.LogInformation("=== OneDriveTool.DownloadFileFromUrlAsync started ===");
            Logger.LogInformation("Sharing URL: {SharingUrl}", sharingUrl);

            // Step 1: OneDrive 공유 URL 인코딩 (u!...)
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sharingUrl));
            string encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
            Logger.LogInformation("✓ 인코딩된 URL: {EncodedUrl}", encodedUrl);

            // Step 2: Graph API 클라이언트 준비
            Logger.LogInformation("Graph API 클라이언트 초기화 중");
            var graphClient = await UserAuthService.GetPersonalOneDriveGraphClientAsync();

            // Step 3: 드라이브 아이템 메타데이터 조회 (파일명, 크기 확인용)
            Logger.LogInformation("Graph API에서 드라이브 아이템 메타데이터 조회 중");
            var driveItem = await graphClient
                .Shares[encodedUrl]
                .DriveItem
                .Request()
                .GetAsync();

            Logger.LogInformation("✓ 드라이브 아이템 조회 완료: {FileName}", driveItem.Name);

            // 폴더인지 확인 (폴더는 다운로드 불가)
            if (driveItem.Folder != null)
            {
                return new OneDriveDownloadResult
                {
                    ErrorMessage = "공유 링크가 파일이 아닌 폴더입니다."
                };
            }

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

            // Step 5: ★★★ Graph SDK의 Content.Request().GetAsync() 사용 ★★★
            // URL을 찾지 않고 SDK가 제공하는 콘텐츠 스트림 직접 요청
            // 이 방식은 @microsoft.graph.downloadUrl이 없어도 작동합니다.
            Logger.LogInformation("Graph API에서 파일 콘텐츠 스트림 요청 중");
            using (var contentStream = await graphClient.Shares[encodedUrl].DriveItem.Content.Request().GetAsync())
            {
                Logger.LogInformation("✓ 파일 스트림 수신 완료");

                // Step 6: Azure File Share에 스트리밍으로 업로드
                Logger.LogInformation("Azure File Share에 파일 업로드 중: {FileName}", safeFileName);

                // 파일 생성 (크기 지정)
                await fileClient.CreateAsync(driveItem.Size ?? 0);
                Logger.LogInformation("✓ 파일 생성 완료");

                // 스트림 업로드
                await fileClient.UploadAsync(contentStream);
                Logger.LogInformation("✓ 파일 업로드 완료");
            }

            // Step 7: SAS 토큰 생성하여 다운로드 URL 생성
            try
            {
                // 연결 문자열에서 계정 정보 추출
                var accountName = ExtractAccountNameFromConnectionString(connectionString);
                var accountKey = ExtractAccountKeyFromConnectionString(connectionString);

                if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
                {
                    Logger.LogWarning("SAS 토큰 생성을 위한 계정 정보를 추출할 수 없습니다. 일반 URI 반환");
                    string downloadUrl = fileClient.Uri.AbsoluteUri;
                    return new OneDriveDownloadResult
                    {
                        FileName = driveItem.Name,
                        DownloadPath = downloadUrl,
                        ErrorMessage = null
                    };
                }

                var credential = new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey);

                // SAS 토큰 빌더 생성
                var sasBuilder = new Azure.Storage.Sas.ShareSasBuilder()
                {
                    ShareName = FileShareName,
                    FilePath = safeFileName,
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
                };

                // Read 권한만 부여
                sasBuilder.SetPermissions(Azure.Storage.Sas.ShareFileSasPermissions.Read);

                // SAS URI 생성
                Uri sasUri = new Uri($"{fileClient.Uri}?{sasBuilder.ToSasQueryParameters(credential)}");
                Logger.LogInformation("✓ SAS 토큰 생성 완료");
                Logger.LogInformation("=== 다운로드 완료. SAS URL: {SasUrl}", sasUri.AbsoluteUri);

                // Step 8: 볼륨 마운트 시도
                try
                {
                    Logger.LogInformation("Step 8: Azure File Share 볼륨 마운트 시도 중");
                    TryMountFileShare(connectionString);
                    Logger.LogInformation("✓ Step 8: 볼륨 마운트 시도 완료");
                }
                catch (Exception mountEx)
                {
                    Logger.LogWarning(mountEx, "볼륨 마운트 실패 (계속 진행)");
                }

                return new OneDriveDownloadResult
                {
                    FileName = driveItem.Name,
                    DownloadPath = sasUri.AbsoluteUri,
                    ErrorMessage = null
                };
            }
            catch (Exception sasEx)
            {
                Logger.LogWarning(sasEx, "SAS 토큰 생성 실패, 일반 URI 반환");

                // SAS 실패 시 일반 URI만 반환
                string downloadUrl = fileClient.Uri.AbsoluteUri;
                return new OneDriveDownloadResult
                {
                    FileName = driveItem.Name,
                    DownloadPath = downloadUrl,
                    ErrorMessage = null
                };
            }
        }
        catch (ServiceException svEx)
        {
            // Graph API 관련 에러 디버깅용
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

    /// <summary>
    /// Azure File Share를 로컬에 마운트합니다.
    /// Windows: Z: 드라이브, Mac/Linux: ~/downloads 폴더
    /// </summary>
    private void TryMountFileShare(string connectionString)
    {
        var accountName = ExtractAccountNameFromConnectionString(connectionString);
        var accountKey = ExtractAccountKeyFromConnectionString(connectionString);

        if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
        {
            Logger.LogWarning("마운트 실패: 연결 문자열에서 계정 정보를 추출할 수 없습니다.");
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MountWindowsDrive(accountName, accountKey);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MountMacFolder(accountName, accountKey);
            }
            else
            {
                Logger.LogInformation("Linux는 sudo 권한이 필요하므로 자동 마운트를 건너뜁니다.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "마운트 시도 중 오류 발생");
        }
    }

    /// <summary>
    /// Windows에서 Z: 드라이브로 마운트
    /// </summary>
    private void MountWindowsDrive(string account, string key)
    {
        try
        {
            // 기존 연결 제거
            RunCommand("net", $"use Z: /delete /y");

            string uncPath = $@"\\{account}.file.core.windows.net\downloads";
            string args = $"use Z: {uncPath} /u:AZURE\\{account} \"{key}\"";

            RunCommand("net", args);

            if (System.IO.Directory.Exists("Z:\\"))
            {
                Logger.LogInformation("✅ Windows 마운트 성공! [Z:] 드라이브를 확인하세요.");
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", "Z:");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Windows 마운트 실패");
        }
    }

    /// <summary>
    /// Mac에서 ~/downloads 폴더로 마운트
    /// </summary>
    private void MountMacFolder(string account, string key)
    {
        try
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string mountPath = Path.Combine(homeDir, "downloads_azure");

            if (!System.IO.Directory.Exists(mountPath))
            {
                System.IO.Directory.CreateDirectory(mountPath);
            }

            string smbUrl = $"//AZURE;{account}:{key}@{account}.file.core.windows.net/downloads";
            RunCommand("mount_smbfs", $"{smbUrl} {mountPath}");

            Logger.LogInformation("✅ Mac 마운트 성공! [{MountPath}]를 확인하세요.", mountPath);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Mac 마운트 실패");
        }
    }

    /// <summary>
    /// 시스템 명령어 실행
    /// </summary>
    private void RunCommand(string command, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"프로세스 시작 실패: {command}");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            Logger.LogWarning("명령어 실패 ({Command}): {Error}", command, error);
        }
    }

}
