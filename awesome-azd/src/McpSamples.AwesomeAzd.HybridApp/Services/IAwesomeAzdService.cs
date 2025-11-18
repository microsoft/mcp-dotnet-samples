using McpSamples.AwesomeAzd.HybridApp.Models;

namespace McpSamples.AwesomeAzd.HybridApp.Services
{
    /// <summary>
    /// Provides interfaces for metadata service operations for Awesome AZD templates.
    /// </summary>
    public interface IAwesomeAzdService
    {
        /// <summary>
        /// Searches for relevant templates in the Awesome AZD repository based on keywords.
        /// </summary>
        /// <param name="keywords">The keywords to search for.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A <see cref="List{AwesomeAzdTemplateModel}"/> containing all matching search results.</returns>
        Task<List<AwesomeAzdTemplateModel>> GetTemplateListAsync(string keywords, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a given AZD template command asynchronously.
        /// </summary>
        /// <param name="command">The AZD command to execute (e.g., "azd init -t owner/repo --environment myenv").</param>
        /// <param name="workingDirectory">
        /// The directory where the command should be executed. 
        /// Can be <c>null</c> if a default directory should be used internally.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to cancel the async operation.</param>
        /// <returns>
        /// A <see cref="CommandExecutionResult"/> containing the success status, output, and any error messages from the command execution.
        /// </returns>
        Task<CommandExecutionResult> ExecuteTemplateCommandAsync(string command, string workingDirectory, CancellationToken cancellationToken = default);
    }
}
