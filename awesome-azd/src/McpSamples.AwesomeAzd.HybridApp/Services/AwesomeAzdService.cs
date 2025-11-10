namespace McpSamples.AwesomeAzd.HybridApp.Services;

using System.Text.Json;
using McpSamples.AwesomeAzd.HybridApp.Models;

public class AwesomeAzdService(HttpClient http, ILogger<AwesomeAzdService> logger) : IAwesomeAzdService
{
    private const string AwesomeAzdTemplateFileUrl = "https://raw.githubusercontent.com/Azure/awesome-azd/main/website/static/templates.json";

    private List<AwesomeAzdTemplateModel>? _cachedTemplates;

    public async Task<List<AwesomeAzdTemplateModel>> GetTemplateListAsync(string keywords, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return new List<AwesomeAzdTemplateModel>();
        }

        var templates = await GetTemplatesAsync(cancellationToken).ConfigureAwait(false);

        var searchTerms = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(term => term.Trim().ToLowerInvariant())
                                  .Where(term => string.IsNullOrWhiteSpace(term) != true)
                                  .ToArray();

        logger.LogInformation("Search terms: {terms}", string.Join(", ", searchTerms));

        var result = templates
            .Where(t => ContainsAnyKeyword(t.Title, searchTerms)
                    || ContainsAnyKeyword(t.Description, searchTerms)
                    || ContainsAnyKeyword(t.Author, searchTerms)
                    || ContainsAnyKeyword(t.Source, searchTerms)
                    || (t.Tags?.Any(tag => ContainsAnyKeyword(tag, searchTerms)) ?? false)
                    || (t.Languages?.Any(lang => ContainsAnyKeyword(lang, searchTerms)) ?? false)
                    || (t.AzureServices?.Any(svc => ContainsAnyKeyword(svc, searchTerms)) ?? false))
            .ToList();

        _cachedTemplates = result;
        return result;
    }

    private async Task<List<AwesomeAzdTemplateModel>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        if (_cachedTemplates != null && _cachedTemplates.Any())
        {
            return _cachedTemplates;
        }

        try
        {
            logger.LogInformation("Fetching templates from {url}", AwesomeAzdTemplateFileUrl);

            var response = await http.GetAsync(AwesomeAzdTemplateFileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _cachedTemplates = JsonSerializer.Deserialize<List<AwesomeAzdTemplateModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<AwesomeAzdTemplateModel>();


            logger.LogInformation("Loaded {count} templates.", _cachedTemplates.Count);
            return _cachedTemplates;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch or deserialize templates.");
            return new List<AwesomeAzdTemplateModel>();
        }
    }

    private static bool ContainsAnyKeyword(string? text, string[] searchTerms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return searchTerms.Any(term => text.Contains(term, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task<AwesomeAzdTemplateModel?> GetTemplateDetailByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

}