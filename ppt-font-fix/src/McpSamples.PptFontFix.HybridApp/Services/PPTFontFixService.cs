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
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage.Blobs.Specialized;

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
    private readonly BlobServiceClient? _blobServiceClient;

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

        // Azure Connection String을 사용하여 BlobServiceClient 초기화 시도
        string? azureConnectionString = configuration["AzureBlobConnectionString"];
        if (!string.IsNullOrEmpty(azureConnectionString))
        {
            _blobServiceClient = new BlobServiceClient(azureConnectionString);
        }
    }

    /// <inheritdoc />
    public async Task OpenPptFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri? uriResult) &&
        (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            _logger.LogInformation("✅ Detected URL path. Attempting to open from Blob Storage: {Url}", filePath);

            if (_blobServiceClient == null)
            {
                throw new InvalidOperationException("Cannot open from URL. AzureBlobConnectionString is not configured.");
            }

            try
            {
                // URL에서 컨테이너 이름과 Blob 이름 추출
                // 예: https://stzbfq6f2xfezko.blob.core.windows.net/ppt-files/test2.pptx
                // [0] = "", [1] = "ppt-files/", [2] = "test2.pptx"
                
                // URL Segment는 '/'로 분리되며, 첫 번째는 항상 비어있습니다.
                // 컨테이너 이름은 일반적으로 두 번째 세그먼트입니다.
                string containerSegment = uriResult.Segments.Skip(1).FirstOrDefault()?.TrimEnd('/') ?? throw new ArgumentException("Invalid Blob URL format: Container name not found.", nameof(filePath));
                string blobName = string.Join("", uriResult.Segments.Skip(2));

                if (string.IsNullOrEmpty(blobName))
                {
                    throw new ArgumentException("Invalid Blob URL format: Blob name not found.", nameof(filePath));
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerSegment);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Blob file not found in container '{containerSegment}': {blobName}");
                }

                // 메모리 스트림으로 Blob 다운로드
                _presentation?.Dispose();
                var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                memoryStream.Position = 0;

                _presentation = new ShapeCrawler.Presentation(memoryStream);
                _analyzedVisibleFonts = null;
                _logger.LogInformation("Ppt file opened successfully from Blob.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to open Ppt file from URL.");
                throw new InvalidOperationException($"Failed to open Blob file at {filePath}.", ex);
            }
        }

        var searchPaths = new List<string>();

        searchPaths.Add(filePath); 

        string safePath = filePath.Replace('\\', '/');
        string fileName = safePath.Split('/').Last();

        searchPaths.Add(Path.Combine("/app", fileName));
        searchPaths.Add(Path.Combine("/files", fileName));
        searchPaths.Add(Path.Combine("/app/mounts", fileName));

        string baseDir = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        searchPaths.Add(Path.Combine(baseDir, "generated", fileName));
        searchPaths.Add(Path.Combine(Path.GetTempPath(), fileName));

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
            _logger.LogError("❌ File not found. Searched in: {Paths}", string.Join(", ", searchPaths));
            throw new FileNotFoundException($"Ppt file not found. File must be accessible at the host path OR successfully copied to the container volume.", filePath);
        }

        try
        {
            _presentation?.Dispose();
            _presentation = new ShapeCrawler.Presentation(foundPath);
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

        string safeFileName = Path.GetFileName(desiredFileName);
        if (safeFileName.Contains('\\')) safeFileName = safeFileName.Split('\\').Last();
        if (safeFileName.Contains('/')) safeFileName = safeFileName.Split('/').Last();
        safeFileName = safeFileName.Replace(":", "").Trim();

        _logger.LogInformation("Save process started. Target: {SafeName}", safeFileName);

        using (var memoryStream = new MemoryStream())
        {
            await Task.Run(() => _presentation.Save(memoryStream));
            memoryStream.Position = 0;

            string? azureConnectionString = _configuration["AzureBlobConnectionString"];
            if (!string.IsNullOrEmpty(azureConnectionString))
            {
                try 
                {
                    _logger.LogInformation("Environment detected: Azure Blob Storage");
                    var blobServiceClient = new BlobServiceClient(azureConnectionString);
                    var containerClient = blobServiceClient.GetBlobContainerClient("generated-files");
                    await containerClient.CreateIfNotExistsAsync();
                    var blobClient = containerClient.GetBlobClient(safeFileName);
                    await blobClient.UploadAsync(memoryStream, overwrite: true);
                    var sasBuilder = new BlobSasBuilder()
                    {
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), 
                        BlobName = safeFileName, 
                        BlobContainerName = "generated-files", 
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read); 

                    Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
        
                    _logger.LogInformation("✅ Returning Blob URL with SAS token.");
                    return sasUri.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Azure save failed. Falling back to local storage.");
                    memoryStream.Position = 0;
                }
            }   
            string finalPhysicalPath = "";
            string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
            string generatedDir = Path.Combine(webRoot, "generated");
            if (!Directory.Exists(generatedDir)) Directory.CreateDirectory(generatedDir);
            string webStoragePath = Path.Combine(generatedDir, safeFileName); 
            bool isDocker = !OperatingSystem.IsWindows();

            if (isDocker)
            {
                _logger.LogInformation("Environment detected: Docker Container");
                
                try 
                {
                    string syncPath = Path.Combine("/files", safeFileName);
                    
                    memoryStream.Position = 0; 
                    using (var fs = new FileStream(syncPath, FileMode.Create, FileAccess.Write))
                    {
                        await memoryStream.CopyToAsync(fs);
                        await fs.FlushAsync(); 
                    }
                    File.SetUnixFileMode(syncPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
                    
                    finalPhysicalPath = syncPath; 
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "FATAL: Could not save to /files volume. Falling back to internal web root.");
                    finalPhysicalPath = webStoragePath;
                }

                memoryStream.Position = 0;
                using (var fs = new FileStream(webStoragePath, FileMode.Create, FileAccess.Write))
                {
                    await memoryStream.CopyToAsync(fs);
                }
                File.SetUnixFileMode(webStoragePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            else
            {
                _logger.LogInformation("Environment detected: Local Windows");

                string finalPathToSave;

                if (!string.IsNullOrEmpty(outputDirectory) && Directory.Exists(outputDirectory))
                {
                    finalPathToSave = Path.Combine(outputDirectory, safeFileName);
                    _logger.LogInformation("Saving to user-specified path: {Path}", finalPathToSave);
                }
                else
                {
                    if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        _logger.LogWarning("Specified output directory '{Dir}' does not exist or is invalid. Falling back to default.", outputDirectory);
                    }
                    
                    finalPathToSave = webStoragePath;
                    _logger.LogInformation("Saving to default webroot path: {Path}", finalPathToSave);
                }
                
                using (var fs = new FileStream(finalPathToSave, FileMode.Create, FileAccess.Write)) // 👈 finalPathToSave 사용
                {
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(fs);
                }

                finalPhysicalPath = finalPathToSave; 
            }

            if (_httpContextAccessor?.HttpContext?.Request != null)
            {
                var request = _httpContextAccessor.HttpContext.Request;
                string url = $"{request.Scheme}://{request.Host}/generated/{safeFileName}";
                _logger.LogInformation("✅ Returning Web URL: {Url}", url);
                return url;
            }
            else
            {
                _logger.LogInformation("✅ Returning Physical Path: {Path}", finalPhysicalPath);
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