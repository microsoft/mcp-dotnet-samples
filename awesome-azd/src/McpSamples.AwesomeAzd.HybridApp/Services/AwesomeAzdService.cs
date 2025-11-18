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

    public async Task<CommandExecutionResult> ExecuteTemplateCommandAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                string current = Directory.GetCurrentDirectory();
                logger.LogInformation("Initial CurrentDirectory = {path}", current);

                // 템플릿 이름 파싱
                string templateName = ExtractTemplateNameFromCommand(command);
                logger.LogInformation("Parsed template name = {name}", templateName);

                workingDirectory = Path.Combine(current, templateName);

                if (!Directory.Exists(workingDirectory))
                {
                    logger.LogInformation("Creating default directory: {dir}", workingDirectory);
                    Directory.CreateDirectory(workingDirectory);
                }
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            cancellationToken.Register(() =>
            {
                try { process.Kill(); } catch { }
            });

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            return new CommandExecutionResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error
            };
        }
        catch (OperationCanceledException)
        {
            return new CommandExecutionResult
            {
                Success = false,
                Output = "",
                Error = "Command execution cancelled"
            };
        }
        catch (Exception ex)
        {
            return new CommandExecutionResult
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


    private string ExtractTemplateNameFromCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "MyTemplate";

        try
        {
            // -t 다음 값을 뜯어오기
            // 예: -t Azure-Samples/openai-mcp-agent-dotnet
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "-t")
                {
                    string path = parts[i + 1]; // Azure-Samples/openai-mcp-agent-dotnet

                    // owner/repo → repo
                    if (path.Contains('/'))
                    {
                        string repo = path.Split('/').Last().Trim();
                        if (!string.IsNullOrWhiteSpace(repo))
                            return repo;
                    }

                    return path; // fallback (owner 없이 repo만 왔을 때)
                }
            }
        }
        catch
        {
            // 무슨 일이 생겨도 default
        }

        return "MyTemplate";
    }

}