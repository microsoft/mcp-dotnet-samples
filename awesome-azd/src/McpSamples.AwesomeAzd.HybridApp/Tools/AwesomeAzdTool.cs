using System.ComponentModel;

using McpSamples.AwesomeAzd.HybridApp.Services;
using McpSamples.AwesomeAzd.HybridApp.Models;

using ModelContextProtocol.Server;

namespace McpSamples.AwesomeAzd.HybridApp.Tools;

/// <summary>
/// Provides MCP tool operations for Azure Developer templates.
/// </summary>
public interface IAwesomeAzdTool
{
    /// <summary>
    /// Searches available Azure Developer templates by keyword.
    /// </summary>
    /// <param name="keywords">The keyword to search templates for</param>
    /// <returns>A list of matching template titles.</returns>
    Task<List<AwesomeAzdTemplateModel>> GetTemplateListAsync(string keywords);
}

/// <summary>
/// This represents the tools entity for Awesome Azd template operations.
/// </summary>
[McpServerToolType]
public class AwesomeAzdTool(IAwesomeAzdService service, ILogger<AwesomeAzdTool> logger) : IAwesomeAzdTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "get_templates", Title = "Search Azure Developer templates")]
    [Description("Searches available Azure Developer templates by keyword.")]
    public async Task<List<AwesomeAzdTemplateModel>> GetTemplateListAsync(
        [Description("The keyword to search templates for")] string keywords)
    {
        var result = new List<AwesomeAzdTemplateModel>();

        try
        {
            var templates = await service.GetTemplateListAsync(keywords).ConfigureAwait(false);
            result = templates.ToList();

            logger.LogInformation("Template search completed successfully for keyword '{Keywords}'.", keywords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while searching templates with keyword '{Keywords}'.", keywords);
            result.Add(new AwesomeAzdTemplateModel
            {
                Title = "Error",
                Description = ex.Message
            });
        }

        return result;
    }
}
