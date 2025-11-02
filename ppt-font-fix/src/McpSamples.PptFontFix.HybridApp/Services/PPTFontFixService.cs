using ShapeCrawler;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Services;

/// <summary>
/// This provides interface for PPT font fixing operations.
/// </summary>
public interface IPPTFontFixService
{
    /// <summary>
    /// open a PPT file.
    /// </summary>
    /// <param name="filePath"></param>
    Task OpenPPTFileAsync(string filePath);
}

/// <summary>
/// This represents the service entity for PPT font fixing.
/// </summary>
/// <param name="logger"></param>
public class PPTFontFixService(ILogger<PPTFontFixService> logger) : IPPTFontFixService
{
    /// <inheritdoc />
    public async Task OpenPPTFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
        if (File.Exists(filePath) == false)
        {
            throw new FileNotFoundException("PPT file does not exist.", filePath);
        }

        try
        {
            var presentation = new Presentation(filePath);
            logger.LogInformation("PPT file opened successfully and verified by ShapeCrawler: {FilePath}", filePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PPT file with ShapeCrawler: {FilePath}", filePath);
            throw;
        }
    }
}