using System.Text.Json;

using McpAwesomeCopilot.Common.Models;

using Microsoft.Extensions.Logging;

namespace McpAwesomeCopilot.Common.Services;

/// <summary>
/// Service for loading and managing metadata configuration
/// </summary>
public class MetadataService(HttpClient http, JsonSerializerOptions options, ILogger<MetadataService> logger) : IMetadataService
{
    private const string MetadataFileName = "metadata.json";
    private const string AwesomeCopilotFileUrl = "https://raw.githubusercontent.com/github/awesome-copilot/refs/heads/main/{mode}/{filename}";

    private readonly string _metadataFilePath = Path.Combine(AppContext.BaseDirectory, MetadataFileName);
    private Metadata? _cachedMetadata;

    /// <summary>
    /// Searches for relevant data in chatmodes, instructions, and prompts based on keywords in their description fields
    /// </summary>
    /// <param name="keywords">The keywords to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Metadata object containing all matching search results</returns>
    public async Task<Metadata> SearchAsync(string keywords, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keywords) == true)
        {
            return new Metadata();
        }

        var metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);
        var searchTerms = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(term => term.Trim().ToLowerInvariant())
                                  .Where(term => string.IsNullOrEmpty(term) != true)
                                  .ToArray();

        logger.LogInformation("Search terms: {terms}", string.Join(", ", searchTerms));

        var result = new Metadata
        {
            // Search in ChatModes
            ChatModes = [.. metadata.ChatModes.Where(cm => ContainsAnyKeyword(cm.Description, searchTerms) == true)],

            // Search in Instructions
            Instructions = [.. metadata.Instructions.Where(inst => ContainsAnyKeyword(inst.Description, searchTerms) == true)],

            // Search in Prompts
            Prompts = [.. metadata.Prompts.Where(prompt => ContainsAnyKeyword(prompt.Description, searchTerms) == true)]
        };

        return result;
    }

    /// <summary>
    /// Loads file contents from the awesome-copilot repository
    /// </summary>
    /// <param name="mode">The mode directory (chatmodes, instructions, or prompts)</param>
    /// <param name="filename">The filename to load</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The file contents as a string</returns>
    public async Task<string> LoadAsync(string mode, string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mode) == true)
        {
            throw new ArgumentException("Mode cannot be null or empty", nameof(mode));
        }

        if (string.IsNullOrWhiteSpace(filename) == true)
        {
            throw new ArgumentException("Filename cannot be null or empty", nameof(filename));
        }

        var url = AwesomeCopilotFileUrl.Replace("{mode}", mode).Replace("{filename}", filename);
        try
        {
            var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Loaded content from {url}", url);

            return content;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to load file '{filename}' from mode '{mode}': {ex.Message}", ex);
        }
    }

    private async Task<Metadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        if (_cachedMetadata != null)
        {
            return _cachedMetadata;
        }

        if (File.Exists(_metadataFilePath) != true)
        {
            throw new FileNotFoundException($"Metadata file not found at: {_metadataFilePath}");
        }

        var json = await File.ReadAllTextAsync(_metadataFilePath, cancellationToken).ConfigureAwait(false);
        _cachedMetadata = JsonSerializer.Deserialize<Metadata>(json, options)
                          ?? throw new InvalidOperationException("Failed to deserialize metadata");

        return _cachedMetadata;
    }

    private static bool ContainsAnyKeyword(string description, string[] searchTerms)
    {
        if (string.IsNullOrEmpty(description))
        {
            return false;
        }

        var result = searchTerms.Any(term => description.Contains(term, StringComparison.InvariantCultureIgnoreCase) == true);

        return result;
    }
}
