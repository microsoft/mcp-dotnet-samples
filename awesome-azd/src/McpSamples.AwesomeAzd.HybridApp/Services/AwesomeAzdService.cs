namespace McpSamples.AwesomeAzd.HybridApp.Services;

using System.Diagnostics;
using System.Text.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using McpSamples.AwesomeAzd.HybridApp.Models;

public class AwesomeAzdService(HttpClient http, ILogger<AwesomeAzdService> logger) : IAwesomeAzdService
{
    private const string AwesomeAzdTemplateFileUrl = "https://raw.githubusercontent.com/Azure/awesome-azd/main/website/static/templates.json";

    public async Task<List<AwesomeAzdTemplateResponse>> GetTemplateListAsync(string keywords, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return new List<AwesomeAzdTemplateResponse>();
        }

        var templates = await GetTemplatesAsync(cancellationToken).ConfigureAwait(false);

        var searchTerms = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(term => term.Trim().ToLowerInvariant())
                                  .Where(term => string.IsNullOrWhiteSpace(term) != true)
                                  .ToArray();

        logger.LogInformation("Search terms: {terms}", string.Join(", ", searchTerms));

        var searchResult = templates
            .Where(t => ContainsAnyKeyword(t.Title, searchTerms)
                    || ContainsAnyKeyword(t.Description, searchTerms)
                    || ContainsAnyKeyword(t.Author, searchTerms)
                    || ContainsAnyKeyword(t.Source, searchTerms)
                    || (t.Tags?.Any(tag => ContainsAnyKeyword(tag, searchTerms)) ?? false)
                    || (t.Languages?.Any(lang => ContainsAnyKeyword(lang, searchTerms)) ?? false)
                    || (t.AzureServices?.Any(svc => ContainsAnyKeyword(svc, searchTerms)) ?? false))
            .ToList();

        var responseList = searchResult.Select(m => new AwesomeAzdTemplateResponse
        {
            Id = m.Id,
            Title = m.Title,
            Description = m.Description,
            Source = m.Source
        }).ToList();

        return responseList;
    }

    
    private async Task<List<AwesomeAzdTemplateDomain>> GetTemplatesAsync(CancellationToken cancellationToken)
    {

        try
        {
            logger.LogInformation("Fetching templates from {url}", AwesomeAzdTemplateFileUrl);

            var response = await http.GetAsync(AwesomeAzdTemplateFileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = JsonSerializer.Deserialize<List<AwesomeAzdTemplateDomain>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<AwesomeAzdTemplateDomain>();

            logger.LogInformation("Loaded {count} templates.", result.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch or deserialize templates.");
            return new List<AwesomeAzdTemplateDomain>();
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

    private string ExtractOwnerRepo(string srcPath)
    {
        try
        {
            var uri = new Uri(srcPath);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                return $"{segments[0]}/{segments[1]}"; // owner/repo
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse srcPath as GitHub URL: {srcPath}", srcPath);
        }

        return srcPath;
    }

}