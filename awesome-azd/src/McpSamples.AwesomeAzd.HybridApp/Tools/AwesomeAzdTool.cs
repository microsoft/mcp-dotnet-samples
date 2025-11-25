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
    Task<List<AwesomeAzdTemplateResponse>> GetTemplateListAsync(string keywords);

    /// <summary>
    /// Generates an AzdCommand object with default working directory and environment.
    /// </summary>
    /// <param name="srcPath">GitHub repository URL for the template</param>
    /// <param name="hostUserProfile">Host user profile path (optional)</param>
    /// <param name="workingDirectory">Working directory where the command would run (optional)</param>
    /// <param name="envName">Name of the environment to apply (optional)</param>
    /// <returns>A task that resolves to an <see cref="AzdCommand"/>.</returns>
    Task<AzdCommand> CreateCommandAsync(
        [Description("GitHub repository URL for the template")] string srcPath,
        [Description("Host user profile path")] string? hostUserProfile = null,
        [Description("Working directory where the command would run")] string? workingDirectory = null,
        [Description("Name of the environment to apply")] string? envName = null);
        
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
    public async Task<List<AwesomeAzdTemplateResponse>> GetTemplateListAsync(
        [Description("The keyword to search templates for")] string keywords)
    {
        var result = new List<AwesomeAzdTemplateResponse>();

        try
        {
            var templates = await service.GetTemplateListAsync(keywords).ConfigureAwait(false);
            result = templates;

            logger.LogInformation("Template search completed successfully for keyword '{Keywords}'.", keywords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while searching templates with keyword '{Keywords}'.", keywords);
            result.Add(new AwesomeAzdTemplateResponse
            {
                Title = "Error",
                Description = ex.Message
            });
        }

        return result;
    }

    /// <inheritdoc />
    [McpServerTool(Name = "make_command", Title = "Generate an AzdCommand with defaults")]
    [Description("Generates an AzdCommand with default working directory and environment without executing it.")]
    public Task<AzdCommand> CreateCommandAsync(
        [Description("GitHub repository URL for the template")] string srcPath,
        [Description("Host user profile path")] string? hostUserProfile = null,
        [Description("Working directory where the command would run")] string? workingDirectory = null,
        [Description("Name of the environment to apply")] string? envName = null)
    {
        var environment = string.IsNullOrWhiteSpace(envName) ? "myenv" : envName;

        // workingDirectory와 hostUserProfile 둘 다 없으면 예외 처리
        if (string.IsNullOrWhiteSpace(workingDirectory) && string.IsNullOrWhiteSpace(hostUserProfile))
        {
            var msg = "Both hostUserProfile and workingDirectory are null or empty. Please provide at least one valid path.";
            logger.LogError(msg);
            throw new ArgumentException(msg, nameof(workingDirectory));
        }
        
        // workingDirectory가 지정되지 않았다면, hostUserProfile 기준으로 경로 구성
        var directory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.Combine(hostUserProfile, ExtractRepoName(srcPath))
            : workingDirectory;

        var ownerRepo = ExtractOwnerRepo(srcPath);

        var command = $"azd init -t {ownerRepo} --environment {environment}";

        logger.LogInformation("Generated AzdCommand for srcPath '{ownerRepo}' at directory '{Directory}'", ownerRepo, directory);

        var azdCommand = new AzdCommand
        {
            Command = command,
            WorkingDirectory = directory
        };

        return Task.FromResult(azdCommand);
    }

    /// <summary>
    /// Extracts the repository name from a GitHub URL (e.g., "owner/repo" -> "repo").
    /// </summary>
    private string ExtractRepoName(string srcPath)
    {
        try
        {
            var uri = new Uri(srcPath);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                return segments[1]; // repo name
            }
        }
        catch
        {
            logger.LogWarning("Failed to parse repo name from srcPath: {SrcPath}", srcPath);
        }

        return srcPath;
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
        catch
        {
            // 실패하면 그대로 srcPath 반환
        }

        return srcPath;
    }

}
