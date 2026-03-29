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
        /// <returns>A <see cref="List{AwesomeAzdTemplateResponse}"/> containing all matching search results.</returns>
        Task<List<AwesomeAzdTemplateResponse>> GetTemplateListAsync(string keywords, CancellationToken cancellationToken = default);

    }
}
