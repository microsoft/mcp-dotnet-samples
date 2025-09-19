using System.ComponentModel;

using McpSamples.AwesomeCopilot.HybridApp.Models;
using McpSamples.AwesomeCopilot.HybridApp.Services;

using ModelContextProtocol.Server;

namespace McpSamples.AwesomeCopilot.HybridApp.Tools;

/// <summary>
/// This provides interfaces for metadata tool operations.
/// </summary>
public interface IMetadataTool
{
    /// <summary>
    /// Searches custom instructions based on keywords in their titles and descriptions.
    /// </summary>
    /// <param name="keywords">The keyword to search for</param>
    /// <returns>A <see cref="MetadataResult"/> containing the search results.</returns>
    Task<MetadataResult> SearchAsync(string keywords);

    /// <summary>
    /// Loads a custom instruction from the awesome-copilot repository.
    /// </summary>
    /// <param name="mode">The instruction mode</param>
    /// <param name="filename">The filename of the instruction</param>
    /// <returns>The file contents as a string</returns>
    Task<string> LoadAsync(InstructionMode mode, string filename);

    /// <summary>
    /// Lists all collections available in the metadata.
    /// </summary>
    /// <returns>Returns a <see cref="MetadataResult"/> containing collections</returns>
    Task<MetadataResult> ListCollectionsAsync();

    /// <summary>
    /// Loads a collection by id
    /// </summary>
    /// <param name="id">Collection id</param>
    /// <returns>Returns the collection as an object or error message as string</returns>
    Task<object> LoadCollectionAsync(string id);
}

/// <summary>
/// This represents the tools entity for metadata of Awesome Copilot repository.
/// </summary>
[McpServerToolType]
public class MetadataTool(IMetadataService service, ILogger<MetadataTool> logger) : IMetadataTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "search_instructions", Title = "Searches custom instructions")]
    [Description("Searches custom instructions based on keywords in their titles and descriptions.")]
    public async Task<MetadataResult> SearchAsync(
        [Description("The keyword to search for")] string keywords)
    {
        var result = new MetadataResult();
        try
        {
            var metadata = await service.SearchAsync(keywords).ConfigureAwait(false);

            logger.LogInformation("Search completed successfully with keyword '{Keywords}'.", keywords);

            result.Metadata = metadata;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while searching instructions with keyword '{Keywords}'.", keywords);

            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    [McpServerTool(Name = "load_instruction", Title = "Loads a custom instruction")]
    [Description("Loads a custom instruction from the repository.")]
    public async Task<string> LoadAsync(
        [Description("The instruction mode")] InstructionMode mode,
        [Description("The filename of the instruction")] string filename)
    {
        try
        {
            if (mode == InstructionMode.Undefined)
            {
                throw new ArgumentException("Instruction mode must be defined.", nameof(mode));
            }

            var result = await service.LoadAsync(mode.ToString().ToLowerInvariant(), filename).ConfigureAwait(false);

            logger.LogInformation("Load completed successfully with mode {Mode} and filename {Filename}.", mode, filename);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while loading instruction with mode {Mode} and filename {Filename}.", mode, filename);

            return ex.Message;
        }
    }

    /// <summary>
    /// Lists all collections available in the metadata.
    /// </summary>
    [McpServerTool(Name = "list_collections", Title = "Lists available collections")]
    [Description("Lists all collections available in the repository metadata.")]
    public async Task<MetadataResult> ListCollectionsAsync()
    {
        var result = new MetadataResult();
        try
        {
            var metadata = await service.SearchAsync(string.Empty).ConfigureAwait(false);
            result.Metadata = new Metadata { Collections = metadata.Collections };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while listing collections.");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Loads a collection by id
    /// </summary>
    [McpServerTool(Name = "load_collection", Title = "Loads a collection by id")]
    [Description("Loads a collection by id from the metadata and returns its object representation.")]
    public async Task<object> LoadCollectionAsync([Description("The collection id")] string id)
    {
        try
        {
            var collection = await service.GetCollectionAsync(id).ConfigureAwait(false);
            if (collection == null)
            {
                return new { Error = $"Collection '{id}' not found" };
            }

            return collection;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while loading collection {Id}.", id);
            return new { Error = ex.Message };
        }
    }
}
