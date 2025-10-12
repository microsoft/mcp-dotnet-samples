using DocumentFormat.OpenXml.Packaging;
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
            var presentationDocument = PresentationDocument.Open(filePath, false);
            await Task.CompletedTask;

            logger.LogInformation("PPT file opened successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PPT file: {FilePath}", filePath);
            throw;
        }
    }
}