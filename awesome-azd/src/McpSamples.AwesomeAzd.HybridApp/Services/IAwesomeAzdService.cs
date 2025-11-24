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
        /// Executes a given AZD template asynchronously using the specified GitHub repository as the source.
        /// </summary>
        /// <param name="srcPath">
        /// The GitHub repository URL of the template (e.g., "https://github.com/owner/repo").
        /// This will be converted internally into the AZD command.
        /// </param>
        /// <param name="workingDirectory">
        /// The directory where the command should be executed.
        /// Can be <c>null</c> if a default directory should be used internally.
        /// </param>
        /// <param name="envName">
        /// The name of the environment to apply.
        /// Can be <c>null</c> to use the default environment internally (e.g., "myenv").
        /// </param>
        /// <param name="cancellationToken">Cancellation token to cancel the async operation.</param>
        /// <returns>
        /// A <see cref="ExecutionResult"/> containing the success status, output, and any error messages from the command execution.
        /// </returns>
        Task<ExecutionResult> ExecuteTemplateAsync(string srcPath, string workingDirectory, string envName, CancellationToken cancellationToken = default);
    }
}
