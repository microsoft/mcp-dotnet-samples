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

    /// <summary>
    /// Executes a given AZD template asynchronously using the specified GitHub repository as the source.
    /// If <paramref name="workingDirectory"/> or <paramref name="envName"/> is <c>null</c> or empty,
    /// default values will be used internally ("myenv" for envName, user home directory for workingDirectory).
    /// </summary>
    /// <param name="srcPath">
    /// The GitHub repository URL of the template (e.g., "https://github.com/owner/repo").
    /// This will be converted internally into the AZD command.
    /// </param>
    /// <param name="workingDirectory">
    /// Optional directory where the command should be executed. 
    /// Pass <c>null</c> to use the default directory internally.
    /// </param>
    /// <param name="envName">
    /// Optional environment name. Pass <c>null</c> to use the default ("myenv") internally.
    /// </param>
    /// <returns>
    /// A <see cref="ExecutionResult"/> containing the success status, output, and any error messages from the command execution.
    /// </returns>
    Task<ExecutionResult> ExecuteTemplateAsync(string srcPath, string? workingDirectory = null, string? envName = null);
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

    /// <inheritdoc />
    [McpServerTool(Name = "execute_template", Title = "Execute a template initialization in a specific directory")]
    [Description("Executes a template initialization based on a GitHub template repository in the given working directory.")]
    public async Task<ExecutionResult> ExecuteTemplateAsync(
        [Description("GitHub repository URL for the template")] string srcPath,
        [Description("Working directory where the command will run")] string? workingDirectory = null,
        [Description("Name of the environment to apply")] string? envName = null) 
    {
        try
        {
            var environment = string.IsNullOrWhiteSpace(envName) ? "myenv" : envName;
            var directory = string.IsNullOrWhiteSpace(workingDirectory) 
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ExtractRepoName(srcPath))
            : workingDirectory;

            var result  = await service.ExecuteTemplateAsync(srcPath, directory, environment);

            logger.LogInformation(
                "Template command executed for srcPath '{SrcPath}' with success={Success}", 
                srcPath, 
                result.Success
            );

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, 
                "Failed to execute template command for srcPath='{SrcPath}', workingDirectory='{WorkingDirectory}', envName='{EnvName}'",
                srcPath, workingDirectory, envName);

            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = ex.Message
            };
        }
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

}
