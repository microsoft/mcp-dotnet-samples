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

        return result;
    }

    public async Task<ExecutionResult> ExecuteTemplateAsync(string srcPath, string workingDirectory, string envName, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string ownerRepo = ExtractOwnerRepo(srcPath);

            string command = $"azd init -t {ownerRepo} --environment {envName}";
            logger.LogInformation("Generated command: {cmd}", command);

            if (!Directory.Exists(workingDirectory))
            {
                logger.LogInformation("Creating directory: {dir}", workingDirectory);
                Directory.CreateDirectory(workingDirectory);
            }
            else
            {
                logger.LogInformation("Using existing directory: {dir}", workingDirectory);
            }

            string fileName;
            string arguments;

            if (OperatingSystem.IsWindows())
            {
                fileName = "cmd.exe";
                arguments = $"/c {command}";
            }
            else
            {
                fileName = "/bin/bash";
                arguments = $"-c \"{command}\"";
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            logger.LogInformation("Executing: {file} {args}", fileName, arguments);
            process.Start();

            cancellationToken.Register(() =>
            {
                try { process.Kill(); } catch { }
            });

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            return new ExecutionResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Command execution cancelled by user");
            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = "Execution cancelled"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while executing command");
            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = ex.Message
            };
        }
    }

    
    private async Task<List<AwesomeAzdTemplateModel>> GetTemplatesAsync(CancellationToken cancellationToken)
    {

        try
        {
            logger.LogInformation("Fetching templates from {url}", AwesomeAzdTemplateFileUrl);

            var response = await http.GetAsync(AwesomeAzdTemplateFileUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = JsonSerializer.Deserialize<List<AwesomeAzdTemplateModel>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<AwesomeAzdTemplateModel>();

            logger.LogInformation("Loaded {count} templates.", result.Count);

            return result;
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