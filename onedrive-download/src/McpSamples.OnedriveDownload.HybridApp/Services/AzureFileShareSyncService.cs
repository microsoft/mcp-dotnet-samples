using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Logging;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Azure File Share에서 파일을 로컬 컴퓨터로 자동 동기화하는 서비스
/// </summary>
public class AzureFileShareSyncService
{
    private readonly ILogger<AzureFileShareSyncService> _logger;
    private const string ShareName = "downloads";

    public AzureFileShareSyncService(ILogger<AzureFileShareSyncService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Azure File Share에서 파일을 로컬 경로로 다운로드합니다.
    /// (HTTPS 기반, 포트 443 사용 - VPN/핫스팟 불필요)
    /// </summary>
    /// <remarks>
    /// 다운로드 경로: {프로젝트루트}/generated/
    /// 예: C:\Users\Woo_Ang\Desktop\cdp-mcp\onedrive-download\generated\
    /// </remarks>
    public async Task SyncFilesAsync(string connectionString)
    {
        try
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("[Sync] AZURE_STORAGE_CONNECTION_STRING is not configured. Skipping sync.");
                return;
            }

            // 프로젝트 루트의 'generated' 폴더로 설정
            // AppContext.BaseDirectory는 실행 파일 디렉토리
            // bin/Debug(또는 Release)/net9.0/ 에서 ../../.. 올라가면 프로젝트 루트
            string projectRoot = FindProjectRoot();
            string localFolderPath = Path.Combine(projectRoot, "generated");

            _logger.LogInformation("[Sync] Starting Azure File Share sync...");
            _logger.LogInformation("[Sync] Share Name: {ShareName}, Local Path: {LocalPath}", ShareName, localFolderPath);

            // 1. Azure File Share 연결
            var shareClient = new ShareClient(connectionString, ShareName);

            // 공유가 없으면 건너뛰기 (정상 상황)
            if (!await shareClient.ExistsAsync())
            {
                _logger.LogInformation("[Sync] Azure File Share '{ShareName}' does not exist yet. Skipping sync.", ShareName);
                return;
            }

            // 2. 로컬 폴더 생성 (없으면)
            if (!Directory.Exists(localFolderPath))
            {
                Directory.CreateDirectory(localFolderPath);
                _logger.LogInformation("[Sync] Created local folder: {LocalFolderPath}", localFolderPath);
            }

            // 3. 루트 디렉토리 클라이언트 획득
            var rootDirectoryClient = shareClient.GetRootDirectoryClient();

            // 4. 파일 목록 조회 및 다운로드
            int downloadCount = 0;
            await foreach (ShareFileItem item in rootDirectoryClient.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    // 파일인 경우만 다운로드
                    await DownloadFileAsync(rootDirectoryClient, item.Name, localFolderPath);
                    downloadCount++;
                }
            }

            _logger.LogInformation("[Sync] ✅ File sync completed. Downloaded {Count} file(s) to {LocalFolderPath}",
                downloadCount, localFolderPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] ⚠️ File sync warning (non-fatal): {Message}", ex.Message);
            // 동기화 실패는 경고만 하고 프로그램을 중단시키지 않음
        }
    }

    /// <summary>
    /// 프로젝트 루트 디렉토리를 찾습니다.
    /// bin/Debug(또는 Release)/net9.0/ 에서 상위 폴더로 이동하여 .csproj 파일을 찾습니다.
    /// </summary>
    private static string FindProjectRoot()
    {
        // 현재 실행 경로: bin/Debug(또는 Release)/net9.0/
        string baseDir = AppContext.BaseDirectory;

        // 상위 폴더로 이동
        DirectoryInfo current = new DirectoryInfo(baseDir);

        // .csproj 파일이 있는 디렉토리를 찾을 때까지 상위로 이동
        while (current != null && current.Parent != null)
        {
            var csprojFiles = current.GetFiles("*.csproj");
            if (csprojFiles.Length > 0)
            {
                // .csproj가 있는 폴더가 프로젝트 루트의 바로 위가 아니라
                // 더 위에 있을 수 있으니 (예: src/McpSamples.OnedriveDownload.HybridApp/)
                // 계속 올라가서 azure.yaml이나 .git가 있는 폴더를 찾자
                return FindSolutionRoot(current);
            }
            current = current.Parent;
        }

        // 못 찾으면 기본값 반환
        return baseDir;
    }

    /// <summary>
    /// 솔루션 루트 디렉토리를 찾습니다 (azure.yaml이나 .git가 있는 폴더).
    /// </summary>
    private static string FindSolutionRoot(DirectoryInfo startDir)
    {
        DirectoryInfo current = startDir;

        while (current != null)
        {
            // azure.yaml 또는 .git 폴더가 있으면 그것이 솔루션 루트
            if (current.GetFiles("azure.yaml").Length > 0 ||
                current.GetDirectories(".git").Length > 0 ||
                current.GetFiles(".gitignore").Length > 0)
            {
                return current.FullName;
            }
            current = current.Parent;
            if (current == null)
            {
                break;
            }
        }

        // 못 찾으면 프로젝트 폴더 반환
        return startDir.FullName;
    }

    /// <summary>
    /// 개별 파일을 다운로드합니다.
    /// </summary>
    private async Task DownloadFileAsync(ShareDirectoryClient directoryClient, string fileName, string localFolderPath)
    {
        try
        {
            var fileClient = directoryClient.GetFileClient(fileName);
            string localFilePath = Path.Combine(localFolderPath, fileName);

            _logger.LogInformation("[Sync] Downloading: {FileName}", fileName);

            // 파일 다운로드
            ShareFileDownloadInfo download = await fileClient.DownloadAsync();

            // 로컬에 저장
            using (FileStream stream = File.OpenWrite(localFilePath))
            {
                await download.Content.CopyToAsync(stream);
                await stream.FlushAsync();
            }

            _logger.LogInformation("[Sync] ✓ Downloaded: {FileName} → {LocalFilePath}", fileName, localFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Failed to download {FileName}: {Message}", fileName, ex.Message);
            // 개별 파일 다운로드 실패는 계속 진행
        }
    }
}
