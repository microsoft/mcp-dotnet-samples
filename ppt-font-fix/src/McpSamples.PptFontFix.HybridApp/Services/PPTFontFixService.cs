using ShapeCrawler;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Linq;
using McpSamples.PptFontFix.HybridApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading;
using Azure.Storage.Sas;
using McpSamples.PptFontFix.HybridApp.Configurations;

namespace McpSamples.PptFontFix.HybridApp.Services;

/// <summary>
/// This provides interface for Ppt font fixing operations.
/// </summary>
public interface IPptFontFixService
{
    
    /// <summary>
    /// open a Ppt file.
    /// </summary>
    /// <param name="filePath"></param>
    Task<string?> OpenPptFileAsync(string filePath);

    /// <summary>
    /// Analyze fonts in a Ppt file.
    /// </summary>
    /// <returns>A classified list of fonts used in the presentation.</returns>
    Task<PptFontAnalyzeResult> AnalyzeFontsAsync();

    /// <summary>
    /// Save the modified Ppt file.
    /// </summary>
    /// <param name="desiredFileName">The desired file name to save as.</param>
    /// <param name="outputDirectory">The directory path on the host machine to save the modified file.</param> // ✅ 신규 추가
    Task<string> SavePptFileAsync(string desiredFileName, string? outputDirectory = null);

    /// <summary>
    /// Remove Unused Fonts from the presentation.
    /// </summary>
    /// <param name="locationsToRemove">A list of shape locations to be removed.</param>
    Task<int> RemoveUnusedFontsAsync(List<FontUsageLocation> locationsToRemove);

    ///<summary>
    /// Replace a font with another font throughout the presentation.
    /// </summary>
    /// <param name="fontToReplace">The font name to be replaced.</param>
    /// <param name="replacementFont">The replacement font name.</param>
    /// <returns>The number of replacements made.</returns>
    Task<int> ReplaceFontAsync(string fontToReplace, string replacementFont);
}

/// <summary>
/// This represents the service entity for Ppt font fixing.
/// </summary>
/// <param name="logger"></param>
public class PptFontFixService : IPptFontFixService
{
    private readonly ILogger<PptFontFixService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IWebHostEnvironment? _webHostEnvironment;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly PptFontFixAppSettings _settings;
    private Presentation? _presentation;
    private readonly string? _fileShareMountPath;

    private HashSet<string>? _analyzedVisibleFonts;

    public PptFontFixService(
        ILogger<PptFontFixService> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        PptFontFixAppSettings settings,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();
        _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        _fileShareMountPath = configuration["AZURE_FILE_SHARE_MOUNT_PATH"];
        _settings = settings;
        
    }
    private readonly string? _hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");


    
    /// <inheritdoc />
    public async Task<string?> OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        _presentation?.Dispose();
        _presentation = null;
        _analyzedVisibleFonts = null; 

        if (Uri.TryCreate(filePath, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            _logger.LogError("File path cannot be a URL. Must be a local or mounted path.");
            return "[Error] File Access: Direct URL access is not supported. Please ensure the file is placed in the shared volume.";
        }

        var searchPaths = new List<string> { filePath }; 
        string safeFileName = Path.GetFileName(filePath.Replace('\\', '/'));

        // (기존) 컨테이너 내부의 미리 약속된 경로를 탐색
        searchPaths.Add(Path.Combine("/app", safeFileName));
        searchPaths.Add(Path.Combine("/files", safeFileName));
        searchPaths.Add(Path.Combine("/app/mounts", safeFileName));

        // (기존) WebRoot 및 Temp 경로 탐색
        string baseDir = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        searchPaths.Add(Path.Combine(baseDir, "generated", safeFileName));

        // (신규) File Share Mount Path를 기반으로 파일명으로 직접 접근 시도
        if (!string.IsNullOrEmpty(_fileShareMountPath))
        {
            searchPaths.Add(Path.Combine(_fileShareMountPath, "generated", safeFileName));
        }

        string? foundPath = null;
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                foundPath = path;
                _logger.LogInformation("✅ File found at: {Path}", foundPath);
                break;
            }
        }
        
        if (foundPath == null)
        {
            // 2-1. Azure Container Apps (Remote HTTP) 환경 (IsAzure 플래그 사용)
            if (_settings.IsAzure && _settings.IsHttpMode)
            {
                // [변경] Azure 환경에서는 curl 업로드 가이드 반환
                string uploadUrl = "/upload"; 
                var request = _httpContextAccessor?.HttpContext?.Request;
                if (request != null)
                {
                    uploadUrl = $"{request.Scheme}://{request.Host}/upload";
                }
                
                return $"""
                    ⚠️ Action Required: File Upload Needed (Azure Remote)

                    I am running in an Azure environment and cannot access your local file `{filePath}` directly.
                    To proceed, you need to upload the file to the server.

                    Please execute the appropriate command for your operating system (OS) using your terminal or shell tool:

                    ### 🍎/🐧 macOS & Linux (Example)

                    curl -F "file=@{filePath}" {uploadUrl}

                    ### 💻 Windows (PowerShell/CMD Example)

                    curl.exe -F "file=@{filePath}" {uploadUrl}

                    After the command completes successfully, please call the `open_ppt_file` tool again with the path returned by the upload.
                    """;
            }
            // 2-2. Docker Container (Stdio/HTTP Local) 환경 (IsContainer 플래그 사용)
            else if (_settings.IsContainer)
            {
                // [변경] Docker 환경에서는 볼륨 복사(cp) 가이드 반환
                string targetContainerPath = Path.Combine(_settings.SpecsPath, safeFileName);
                string targetHostPath = targetContainerPath;
                string? hostRootPath = Environment.GetEnvironmentVariable("HOST_ROOT_PATH");


                if (!string.IsNullOrEmpty(_hostRootPath))
                {

                    string normalizedHostRoot = _hostRootPath.Replace('\\', '/').TrimEnd('/');
                    
                    targetHostPath = $"{normalizedHostRoot}/ppt-font-fix/workspace/{safeFileName}";
                }
                
                return $"""
                    ⚠️ Action Required: File Synchronization Needed (Docker Container)

                    The file '{safeFileName}' (Original path: `{filePath}`) is not currently in the shared volume visible to the container.
                    To proceed, I need access to this file.

                    EXECUTION PLAN (Follow Strictly):
                    1. COPY the file from your local machine path (`{filePath}`) 
                       to the host directory mapped to the container volume. 
                       The recommended target path is: `{targetHostPath}`
                    2. RETRY the `open_ppt_file` tool immediately with the original file path (`{filePath}`).
                    """;
            }
            // 2-3. 로컬 환경 또는 기타 오류
            
            _logger.LogError("❌ File not found. Searched in: {Paths}", string.Join(", ", searchPaths));
            // [변경] 원본 코드의 복사 시도/throw 대신 에러 메시지 반환
            return $"[Error] File Not Found: The file '{filePath}' was not found. Please ensure the path is correct and accessible.";
        }
        
        // 3. 파일 열기 (foundPath 사용)
        try
        {
            _presentation?.Dispose();
            _presentation = new ShapeCrawler.Presentation(foundPath);
            _analyzedVisibleFonts = null; 
            _logger.LogInformation("Ppt file opened successfully: {FilePath}", foundPath);
            return "✅ Ppt file opened successfully and ready for analysis.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Ppt file from path: {Path}", foundPath);
            return $"[Error] Failed to open Ppt file: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task<PptFontAnalyzeResult> AnalyzeFontsAsync()
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before analyzing fonts.");
        }

        var totalFontsInSlides = new HashSet<string>();
        var visibleFontUsages = new Dictionary<string, List<FontUsageLocation>>();
        var result = new PptFontAnalyzeResult();
        var slideWidth = this._presentation.SlideWidth;
        var slideHeight = this._presentation.SlideHeight;

        Action<IParagraphPortion, bool, bool, FontUsageLocation> processPortion = (portion, isShapeVisible, isSlideVisible, location) =>
        {
            if (portion.GetType().Name == "ParagraphLineBreak") 
            {
                return; 
            }
            if (portion.Font == null) return;
            string? fontName = null;
    
            try
            {
                fontName = portion.Font.LatinName; 
            }
            catch (NullReferenceException ex)
            {
                _logger.LogWarning(ex, 
                    "NullReferenceException occurred while getting LatinName for Shape: {ShapeName} in Slide: {SlideNumber}. Skipping portion.", 
                    location.ShapeName, location.SlideNumber);
                return;
            }

            if (string.IsNullOrEmpty(fontName)) 
            {
                return; 
            }

            totalFontsInSlides.Add(fontName);

            if (isSlideVisible && isShapeVisible)
            {
                bool isWhitespace = string.IsNullOrWhiteSpace(portion.Text);
                if (!isWhitespace)
                {
                    if (!visibleFontUsages.TryGetValue(fontName, out List<FontUsageLocation>? value))
                    {
                        value = [];
                        visibleFontUsages[fontName] = value;
                    }

                    value.Add(location);
                }
            }
        };

        foreach (var slide in this._presentation.Slides)
        {
            bool isSlideVisible = !slide.Hidden();
            foreach (var shape in slide.Shapes)
            {
                var location = new FontUsageLocation { SlideNumber = slide.Number, ShapeName = shape.Name };

                bool isEmptyBox = shape.TextBox != null && string.IsNullOrWhiteSpace(shape.TextBox.Text);
                if (isEmptyBox)
                {
                    _logger.LogWarning("[Empty Box Detected] Slide Number: {SlideNumber}, Shape Name: {ShapeName}",
                        slide.Number, shape.Name);
                    result.UnusedFontLocations.Add(location);
                }

                bool isShapeVisible = !((shape.X + shape.Width) <= 0 ||
                                        shape.X >= slideWidth ||
                                        (shape.Y + shape.Height) <= 0 ||
                                        shape.Y >= slideHeight);

                if (!isShapeVisible && shape.TextBox != null && !isEmptyBox)
                {
                    _logger.LogWarning("[Text Shape Outside Slide] Slide Number: {SlideNumber}, Shape Name: {ShapeName}",
                        slide.Number, shape.Name);
                    result.UnusedFontLocations.Add(location);
                }

                if (shape.TextBox != null)
                {
                    foreach (var portion in shape.TextBox.Paragraphs.SelectMany(p => p.Portions))
                    {
                        processPortion(portion, isShapeVisible, isSlideVisible, location);
                    }
                }
            }
        }

        var allVisibleFontNames = new HashSet<string>(visibleFontUsages.Keys);
        
        this._analyzedVisibleFonts = new HashSet<string>(allVisibleFontNames, StringComparer.OrdinalIgnoreCase);
        
        var unusedFonts = new HashSet<string>(totalFontsInSlides);
        unusedFonts.ExceptWith(allVisibleFontNames);
        result.UnusedFonts = unusedFonts.ToList();
        int standardFontCount = 2;
        var sortedVisibleFonts = visibleFontUsages
            .OrderByDescending(pair => pair.Value.Count)
            .ToList();
        result.UsedFonts = sortedVisibleFonts
            .Take(standardFontCount)
            .Select(pair => pair.Key)
            .ToList();
        var inconsistentPairs = sortedVisibleFonts
            .Skip(standardFontCount)
            .ToList();
        result.InconsistentlyUsedFonts = inconsistentPairs
            .Select(pair => pair.Key)
            .ToList();
        result.InconsistentFontLocations = inconsistentPairs
            .SelectMany(pair => pair.Value)
            .ToList();

        _logger.LogInformation("[Result] Used (Standard) Fonts: {Fonts}", string.Join(", ", result.UsedFonts));
        _logger.LogInformation("[Result] Unused Fonts: {Fonts}", string.Join(", ", result.UnusedFonts));
        _logger.LogInformation("[Result] Inconsistently Used Fonts: {Fonts}", string.Join(", ", result.InconsistentlyUsedFonts));

        return await Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<string> SavePptFileAsync(string desiredFileName, string? outputDirectory = null)
    {
        if (this._presentation == null) throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before saving.");
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredFileName, nameof(desiredFileName));

        // 파일 이름 정리 (안전한 파일 이름 추출)
        string safeFileName = Path.GetFileName(desiredFileName).Replace(":", "").Trim();

        _logger.LogInformation("Save process started. Target: {SafeName}", safeFileName);

        using (var memoryStream = new MemoryStream())
        {
            await Task.Run(() => _presentation.Save(memoryStream));
            memoryStream.Position = 0;

            string finalPhysicalPath = "";
            string baseDirectory;
            string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
            string webGeneratedDir = Path.Combine(webRoot, "generated"); // 웹 서비스 경로

            bool isContainerEnv = !OperatingSystem.IsWindows() || !string.IsNullOrEmpty(_fileShareMountPath);
            bool isHttpMode = _httpContextAccessor?.HttpContext?.Request != null;


            if (isHttpMode)
{
    // 💡 HTTP 환경에서는 마운트 경로를 무시하고 웹 서비스 경로만 사용합니다.
    baseDirectory = webGeneratedDir;
    _logger.LogInformation("Base Path: HTTP Mode detected. Using Web Root -> {Path}", baseDirectory);
}
// 2. HTTP 모드가 아닐 때 (Stdio/Local/마운트 볼륨 모드)
else
{
    if (!string.IsNullOrEmpty(_fileShareMountPath))
    {
        // Azure File Share Mount Path를 사용합니다.
        baseDirectory = Path.Combine(_fileShareMountPath, "generated");
        _logger.LogInformation("Base Path: File Share Mount (Non-HTTP) -> {Path}", baseDirectory);
    }
    else if (Directory.Exists("/files"))
    {
        // Stdio Container Volume Mount (/files)를 사용합니다.
        baseDirectory = "/files";
        _logger.LogInformation("Base Path: Stdio Volume Mount (/files) -> {Path}", baseDirectory);
    }
    else
    {
        // Fallback으로 웹 루트 경로를 사용합니다.
        baseDirectory = webGeneratedDir;
        _logger.LogInformation("Base Path: Local/Fallback Web Root -> {Path}", baseDirectory);
    }
}

            // 2. 디렉토리 확인 및 생성
            if (!Directory.Exists(baseDirectory))
            {
                try
                {
                    Directory.CreateDirectory(baseDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FATAL: Could not create base directory {Path}.", baseDirectory);
                    throw;
                }
            }
            
            finalPhysicalPath = Path.Combine(baseDirectory, safeFileName);
            
            // 3. 파일 저장 (마운트 볼륨/웹 루트에 단일 저장)
            try 
            {
                memoryStream.Position = 0;
                using (var fs = new FileStream(finalPhysicalPath, FileMode.Create, FileAccess.Write))
                {
                    await memoryStream.CopyToAsync(fs);
                    await fs.FlushAsync();
                }

                // Docker/Linux 환경에서 권한 설정
                if (!OperatingSystem.IsWindows() && string.IsNullOrEmpty(this._fileShareMountPath))
                {
                    File.SetUnixFileMode(finalPhysicalPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
                }
                
                _logger.LogInformation("✅ File successfully saved to mount/web path: {Path}", finalPhysicalPath);
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "FATAL: Could not save file to the final physical path: {Path}", finalPhysicalPath);
                throw;
            }

            // 4. 최종 반환: HTTP Context가 있다면 웹 URL을, 없다면 물리적 경로를 반환
            if (_httpContextAccessor?.HttpContext?.Request != null)
            {
                // 모든 파일이 'generated' 폴더 아래에 저장되었으므로, 웹 URL 경로는 /generated/{filename}으로 통일
                var request = _httpContextAccessor.HttpContext.Request;
                string url = $"{request.Scheme}://{request.Host}/generated/{safeFileName}";
                _logger.LogInformation("✅ Returning Web URL: {Url}", url);
                return url;
            }
            else
            {
                // STDIN/STDOUT (stdio) 또는 로컬 실행 환경에서 HTTP Context가 없을 경우
                _logger.LogInformation("✅ Returning Physical Path (No HTTP Context): {Path}", finalPhysicalPath);
                return finalPhysicalPath;
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveUnusedFontsAsync(List<FontUsageLocation> locationsToRemove)
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before removing unused fonts.");
        }

        if (locationsToRemove == null || locationsToRemove.Count == 0)
        {
            _logger.LogInformation("No locations provided for removal. Skipping removal process.");
            return await Task.FromResult(0);
        }

        int removalCount = 0;

        foreach (var location in locationsToRemove)
        {
            var slide = this._presentation.Slides.FirstOrDefault(s => s.Number == location.SlideNumber);
            if (slide == null)
            {
                _logger.LogWarning("Slide number {SlideNumber} not found. Skipping.", location.SlideNumber);
                continue;
            }

            var shape = slide.Shapes.FirstOrDefault(sh => sh.Name == location.ShapeName);
            if (shape == null)
            {
                _logger.LogWarning("Shape name {ShapeName} not found in slide {SlideNumber}. Skipping.", location.ShapeName, location.SlideNumber);
                continue;
            }

            shape.Remove();
            removalCount++;
            _logger.LogInformation("Removed shape {ShapeName} from slide {SlideNumber}.", location.ShapeName, location.SlideNumber);
        }

        _logger.LogInformation("Total shapes removed: {Count}", removalCount);
        return await Task.FromResult(removalCount);
    }
    
    /// <inheritdoc />
    public async Task<int> ReplaceFontAsync(string fontToReplace, string replacementFont)
    {
        if (this._presentation == null)
        {
            throw new InvalidOperationException("Ppt file is not opened. Please open a Ppt file before replacing fonts.");
        }

        if (string.IsNullOrWhiteSpace(fontToReplace))
        {
            throw new ArgumentException("Font to replace cannot be null or whitespace.", nameof(fontToReplace));
        }

        if (string.IsNullOrWhiteSpace(replacementFont))
        {
            throw new ArgumentException("Replacement font cannot be null or whitespace.", nameof(replacementFont));
        }

        if (!this._analyzedVisibleFonts.Contains(replacementFont))
        {
            throw new ArgumentException($"The font '{replacementFont}' is not a valid font found in the presentation's visible text. Please choose from the analyzed list.", nameof(replacementFont));
        }


        int replacementCount = 0;

        foreach (var slide in this._presentation.Slides)
        {
            foreach (var shape in slide.Shapes)
            {
                if (shape.TextBox != null)
                {
                    foreach (var portion in shape.TextBox.Paragraphs.SelectMany(p => p.Portions))
                    {
                        if (portion.Font != null && string.Equals(portion.Font.LatinName, fontToReplace, StringComparison.OrdinalIgnoreCase))
                        {
                            portion.Font.LatinName = replacementFont;
                            replacementCount++;
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Total font replacements made: {Count}", replacementCount);
        return await Task.FromResult(replacementCount);
    }
}