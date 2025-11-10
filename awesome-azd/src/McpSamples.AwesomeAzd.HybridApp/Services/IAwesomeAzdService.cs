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
        /// Retrieves template details from the cached template list obtained from <see cref="GetTemplateListAsync"/>.
        /// </summary>
        /// <param name="title">The template Title to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The <see cref="AwesomeAzdTemplateModel"/> corresponding to the specified Title, or error Model if not found.</returns>
        Task<AwesomeAzdTemplateModel?> GetTemplateDetailByTitleAsync(string title, CancellationToken cancellationToken = default);

    }
}
