using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System;
using System.Threading.Tasks;
using McpSamples.PptFontFix.HybridApp.Services;

using ModelContextProtocol.Server;

namespace McpSamples.PptFontFix.HybridApp.Tools;

/// <summary>
/// This provides interface for the PPT font fix tool.
/// </summary>

public interface IPPTFontFixTool
{
    /// <summary>
    /// open a PPT file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>Returns the result as a string.</returns>
    Task<string> OpenPPTFileAsync(string filePath);
}

/// <summary>
/// This represents the tool entity for PPT font fixing.
/// </summary>
/// <param name="logger"><see cref="ILogger{TCategoryName}"/> instance.</param>
/// <param name="service"><see cref="IPPTFontFixService"/> instance.</param>
[McpServerToolType]
public class PPTFontFixTool(IPPTFontFixService service, ILogger<PPTFontFixTool> logger) : IPPTFontFixTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "open_ppt_file", Title = "Open a PPT File")]
    [Description("Opens a PPT file and verifies it using ShapeCrawler.")]
    public async Task<string> OpenPPTFileAsync(
        [Description("The path of the PPT file to open")] string filePath)
    {
        try
        {
            await service.OpenPPTFileAsync(filePath).ConfigureAwait(false);

            logger.LogInformation("PPT file opened successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open PPT file: {FilePath}", filePath);

            return ex.Message;
        }

        return null;
    }
}