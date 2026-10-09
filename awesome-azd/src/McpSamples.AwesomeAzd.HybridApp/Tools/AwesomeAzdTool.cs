using System.ComponentModel;
using System.Text.RegularExpressions;

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
    /// <param name="workingDirectory">Working directory where the command would run</param>
    /// <param name="envName">Name of the environment to apply</param>
    /// <returns>A task that resolves to an <see cref="AzdCommand"/>.</returns>
    Task<AzdCommand> CreateCommandAsync(
        [Description("GitHub repository URL for the template")] string srcPath,
        [Description("Working directory where the command would run")] string workingDirectory,
        [Description("Name of the environment to apply")] string envName);
        
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
    [Description("Generates an AzdCommand with default working directory.")]
    public Task<AzdCommand> CreateCommandAsync(
        [Description("GitHub repository URL for the template")] string srcPath,
        [Description("Working directory where the command would run")] string workingDirectory,
        [Description("Name of the environment to apply")] string envName)
    {
        string ownerRepo = srcPath;

        var match = Regex.Match(srcPath, @"github\.com/([^/]+/[^/]+)");
        if (match.Success)
        {
            ownerRepo = match.Groups[1].Value;
        }

        var command = $"azd init -t {ownerRepo} --environment {envName}";

        logger.LogInformation("Generated AzdCommand for ownerRepo '{ownerRepo}' at directory '{workingDirectory}'", ownerRepo, workingDirectory);

        var azdCommand = new AzdCommand
        {
            Command = command,
            WorkingDirectory = workingDirectory 
        };

        return Task.FromResult(azdCommand);
    }


}
