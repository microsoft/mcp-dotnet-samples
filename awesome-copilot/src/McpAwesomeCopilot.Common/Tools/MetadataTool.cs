using System.ComponentModel;

using McpAwesomeCopilot.Common.Models;
using McpAwesomeCopilot.Common.Services;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

namespace McpAwesomeCopilot.Common.Tools;

/// <summary>
/// This represents the tools entity for metadata of Awesome Copilot repository.
/// </summary>
[McpServerToolType]
public class MetadataTool(IMetadataService service, ILogger<MetadataTool> logger)
{
    private readonly IMetadataService _service = service;
    private readonly ILogger<MetadataTool> _logger = logger;

    [McpServerTool(Name = "search_instructions", Title = "Searches custom instructions")]
    [Description("Searches custom instructions based on keywords in their descriptions.")]
    public async Task<MetadataResult> SearchAsync([Description("The keyword to search for")] string keywords)
    {
        var result = new MetadataResult();
        try
        {
            var metadata = await _service.SearchAsync(keywords).ConfigureAwait(false);

            _logger.LogInformation("Search completed successfully with keyword '{Keywords}'.", keywords);

            result.Metadata = metadata;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while searching instructions with keyword '{Keywords}'.", keywords);

            result.ErrorMessage = ex.Message;

            return result;
        }
    }

    [McpServerTool(Name = "load_instruction", Title = "Loads a custom instruction")]
    [Description("Loads a custom instruction from the repository.")]
    public async Task<string> LoadAsync(
        [Description("The instruction mode")] string mode,
        [Description("The filename of the instruction")] string filename)
    {
        try
        {
            var result = await _service.LoadAsync(mode, filename).ConfigureAwait(false);

            _logger.LogInformation("Load completed successfully with mode {Mode} and filename {Filename}.", mode, filename);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while loading instruction with mode {Mode} and filename {Filename}.", mode, filename);

            return ex.Message;
        }
    }
}
