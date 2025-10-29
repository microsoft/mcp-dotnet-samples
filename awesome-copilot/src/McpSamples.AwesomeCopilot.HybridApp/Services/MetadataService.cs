using System.Text.Json;

using McpSamples.AwesomeCopilot.HybridApp.Models;

namespace McpSamples.AwesomeCopilot.HybridApp.Services;

/// <summary>
/// This represents the service entity for searching and loading custom instructions from the awesome-copilot repository.
/// </summary>
public class MetadataService(HttpClient http, JsonSerializerOptions options, ILogger<MetadataService> logger) : IMetadataService
{
    private const string MetadataFileName = "metadata.json";
    private const string AwesomeCopilotFileUrl = "https://raw.githubusercontent.com/github/awesome-copilot/refs/heads/main/{directory}/{filename}";

    private readonly string _metadataFilePath = Path.Combine(AppContext.BaseDirectory, MetadataFileName);
    private Metadata? _cachedMetadata;

    /// <inheritdoc />
    public async Task<Metadata> SearchAsync(string keywords, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(keywords) == true)
        {
            // Treat empty or whitespace keywords as no filtering
            return metadata;
        }

        var searchTerms = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(term => term.Trim().ToLowerInvariant())
                                  .Where(term => string.IsNullOrWhiteSpace(term) != true)
                                  .ToArray();

        logger.LogInformation("Search terms: {terms}", string.Join(", ", searchTerms));

        var result = new Metadata
        {
            // Search in ChatModes
            ChatModes = [.. metadata.ChatModes.Where(cm => ContainsAnyKeyword(cm.Title, searchTerms) == true ||
                                                           ContainsAnyKeyword(cm.Description, searchTerms) == true)],

            // Search in Instructions
            Instructions = [.. metadata.Instructions.Where(inst => ContainsAnyKeyword(inst.Title, searchTerms) == true ||
                                                                   ContainsAnyKeyword(inst.Description, searchTerms) == true)],

            // Search in Prompts
            Prompts = [.. metadata.Prompts.Where(prompt => ContainsAnyKeyword(prompt.Description, searchTerms) == true)],

            // Search in Collections
            Collections = [.. metadata.Collections.Where(c =>
                ContainsAnyKeyword(c.Name, searchTerms) == true ||
                ContainsAnyKeyword(c.Description, searchTerms) == true ||
                ContainsAnyKeyword(c.Usage, searchTerms) == true ||
                (c.Tags is not null && c.Tags.Any(tag => searchTerms.Any(term => tag.Contains(term, StringComparison.InvariantCultureIgnoreCase)))))],

            // Search in Agents
            Agents = [.. metadata.Agents.Where(agent => ContainsAnyKeyword(agent.Title, searchTerms) == true ||
                                                         ContainsAnyKeyword(agent.Description, searchTerms) == true)]
        };

        return result;
    }

    /// <inheritdoc />
    public async Task<string> LoadAsync(string directory, string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory) == true)
        {
            throw new ArgumentException("Directory cannot be null or empty", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(filename) == true)
        {
            throw new ArgumentException("Filename cannot be null or empty", nameof(filename));
        }

        var url = AwesomeCopilotFileUrl.Replace("{directory}", directory).Replace("{filename}", filename);
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
            throw new InvalidOperationException($"Failed to load file '{filename}' from directory '{directory}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Collection?> GetCollectionAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Collection id cannot be null or empty", nameof(id));
        }

        var metadata = await GetMetadataAsync(cancellationToken).ConfigureAwait(false);

        var collection = metadata.Collections.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.InvariantCultureIgnoreCase));

        return collection;
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

    private static bool ContainsAnyKeyword(string? text, string[] searchTerms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var result = searchTerms.Any(term => text.Contains(term, StringComparison.InvariantCultureIgnoreCase) == true);

        return result;
    }
}
