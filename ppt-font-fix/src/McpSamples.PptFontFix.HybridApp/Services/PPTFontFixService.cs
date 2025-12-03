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

namespace McpSamples.PptFontFix.HybridApp.Services;

/// <summary>
/// This provides interface for Ppt font fixing operations.
/// </summary>
public interface IPptFontFixService
{
    /// <summary>
    /// Uploads a PPT file stream to a temporary location on the server and returns a temp ID.
    /// The returned temp ID can be used later with the "temp:{id}" pattern in tools.
    /// </summary>
    /// <param name="fileStream">The PPTX file stream.</param>
    /// <param name="fileName">Original file name (for logging only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Temporary identifier for the uploaded file.</returns>
    Task<string> UploadPptFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// open a Ppt file.
    /// </summary>
    /// <param name="filePath"></param>
    Task OpenPptFileAsync(string filePath);

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
    private Presentation? _presentation;
    private readonly string? _fileShareMountPath;

    private HashSet<string>? _analyzedVisibleFonts;

    public PptFontFixService(
        ILogger<PptFontFixService> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();
        _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        _fileShareMountPath = configuration["AZURE_FILE_SHARE_MOUNT_PATH"];

    }

    private static string GetTempFilePath(string tempId)
    {
        // 업로드된 PPT 파일을 저장하는 서버 임시 경로 규칙
        return Path.Combine(Path.GetTempPath(), $"ppt_upload_{tempId}.tmp");
    }

    /// <inheritdoc />
    public async Task<string> UploadPptFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var tempId = Guid.NewGuid().ToString("N");
        var tempPath = GetTempFilePath(tempId);

        _logger.LogInformation("Uploading PPT file: {FileName} -> {TempPath}", fileName, tempPath);

        using (var outputStream = File.Create(tempPath))
        {
            await fileStream.CopyToAsync(outputStream, cancellationToken);
        }

        return tempId;
    }

    /// <inheritdoc />
    public async Task OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        // 0. temp:ID 패턴 처리 (업로드된 PPT 파일)
        if (filePath.StartsWith("temp:", StringComparison.OrdinalIgnoreCase))
        {
            var tempId = filePath.Substring(5);
            var tempFilePath = GetTempFilePath(tempId);

            if (!File.Exists(tempFilePath))
            {
                _logger.LogError("❌ Temp PPT file for ID '{TempId}' not found at {Path}", tempId, tempFilePath);
                throw new FileNotFoundException($"The uploaded temporary PPT file for ID '{tempId}' was not found or has expired.", tempFilePath);
            }

            try
            {
                _presentation?.Dispose();
                _presentation = new ShapeCrawler.Presentation(tempFilePath);
                _analyzedVisibleFonts = null;
                _logger.LogInformation("Ppt file opened successfully from temp upload: {Path}", tempFilePath);
                await Task.CompletedTask;
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open PPT file from temp path: {Path}", tempFilePath);
                throw;
            }
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
        searchPaths.Add(Path.Combine(Path.GetTempPath(), safeFileName));

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
        
        // 2. 외부 파일 처리 및 마운트 경로로 복사 (가장 중요한 신규 로직)
        if (foundPath == null && !Uri.TryCreate(filePath, UriKind.Absolute, out _) && !filePath.StartsWith("temp:", StringComparison.OrdinalIgnoreCase))
        {
            // 1) 찾지 못했고, URL이나 Temp ID도 아닐 경우, filePath 자체가 로컬 호스트 경로라고 가정하고 복사를 시도합니다.
            
            string targetBaseDir;
            if (!string.IsNullOrEmpty(_fileShareMountPath))
            {
                // File Share 마운트 경로를 사용합니다.
                targetBaseDir = Path.Combine(_fileShareMountPath, "uploaded");
            }
            else if (File.Exists("/files")) // Docker volume convention check
            {
                // Docker 볼륨 마운트 경로를 사용합니다.
                targetBaseDir = "/files";
            }
            else
            {
                // 최후의 수단으로 Web Root/generated를 사용하거나 예외를 발생시킵니다.
                targetBaseDir = Path.Combine(baseDir, "uploaded"); 
            }

            if (!Directory.Exists(targetBaseDir)) 
            {
                Directory.CreateDirectory(targetBaseDir);
            }

            string tempCopyPath = Path.Combine(targetBaseDir, safeFileName);
            
            try
            {
                _logger.LogInformation("Attempting to copy file from outside mount ({Source}) to inside mount ({Target})", filePath, tempCopyPath);
                // 호스트 경로에 있는 파일을 컨테이너 내부의 마운트 경로로 복사 시도
                // 이는 호스트가 컨테이너의 마운트 디렉토리를 공유하고 있어야 성공합니다.
                File.Copy(filePath, tempCopyPath, overwrite: true);
                foundPath = tempCopyPath;
                _logger.LogInformation("✅ File successfully copied to mount path: {Path}", foundPath);
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("❌ File not found at the original path: {Path}. Searched in: {Paths}", filePath, string.Join(", ", searchPaths));
                throw new FileNotFoundException($"Ppt file not found at the original path and could not be copied into the container.", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to copy file from outside mount to inside mount.");
                throw new IOException($"Failed to access or copy file from host path '{filePath}' into the container volume.", ex);
            }
        }
        else if (foundPath == null)
        {
            _logger.LogError("❌ File not found. Searched in: {Paths}", string.Join(", ", searchPaths));
            throw new FileNotFoundException($"Ppt file not found. File must be accessible at the host path OR successfully copied to the container volume.", filePath);
        }
        
        // 3. 파일 열기 (foundPath 사용)
        try
        {
            _presentation?.Dispose();
            // 파일을 메모리 스트림으로 읽어와서 ShapeCrawler에 전달하는 방식이
            // 파일 잠금 문제를 피하고 컨테이너 환경에서 안정적입니다.
            using (var fs = new FileStream(foundPath, FileMode.Open, FileAccess.Read))
            {
                var memoryStream = new MemoryStream();
                await fs.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                _presentation = new ShapeCrawler.Presentation(memoryStream); // ShapeCrawler 생성자에 Stream 전달
            }

            _analyzedVisibleFonts = null;
            _logger.LogInformation("Ppt file opened successfully: {FilePath}", foundPath);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Ppt file.");
            throw;
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

            bool isContainerEnv = !OperatingSystem.IsWindows() || !string.IsNullOrEmpty(_fileShareMountPath);
            bool isHttpMode = _httpContextAccessor?.HttpContext?.Request != null;

            if (isContainerEnv)
            {
                // 1. 저장 기본 경로 결정 (마운트 볼륨 또는 웹 루트)
                if (!string.IsNullOrEmpty(_fileShareMountPath))
                {
                    // Azure File Share Mount Path를 사용합니다. (예: /app/mounts/generated)
                    baseDirectory = Path.Combine(_fileShareMountPath, "generated");
                    _logger.LogInformation("Base Path: Azure File Share Mount -> {Path}", baseDirectory);
                }
                // else if (isHttpMode)
                // {
                //     // [HTTP Service Mode]: /files 마운트가 없거나 HTTP 서비스로 실행 중일 때 Web Root 사용
                //     string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
                //     baseDirectory = Path.Combine(webRoot, "generated");
                //     _logger.LogInformation("Base Path: HTTP Web Root -> {Path}", baseDirectory);
                // }
                else if (Directory.Exists("/files"))
                {
                    // [Stdio Container Mode]: HTTP 요청이 없고, /files 볼륨이 마운트되어 있을 때 사용
                    baseDirectory = "/files"; // 👈 Stdio 출력 경로
                    _logger.LogInformation("Base Path: Stdio Container Volume Mount (/files) -> {Path}", baseDirectory);
                }
                else
                {
                    // File Share가 없거나 마운트 실패 시, 로컬 웹 루트를 사용합니다.
                    string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
                    baseDirectory = Path.Combine(webRoot, "generated");
                    _logger.LogInformation("Base Path: Local/Docker Web Root -> {Path}", baseDirectory);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(outputDirectory) && Directory.Exists(outputDirectory))
                {
                    // 2-1. Stdio/로컬 환경: 사용자가 지정한 outputDirectory 사용 (요청 반영)
                    baseDirectory = outputDirectory;
                    _logger.LogInformation("Base Path: Local User Specified -> {Path}", baseDirectory);
                }
                else
                {
                    // 2-2. Fallback: 로컬 환경이지만 outputDirectory가 없으면 Web Root 사용
                    string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
                    baseDirectory = Path.Combine(webRoot, "generated");
                    _logger.LogInformation("Base Path: Local Web Root Fallback -> {Path}", baseDirectory);
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
                if (string.IsNullOrEmpty(this._fileShareMountPath))
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