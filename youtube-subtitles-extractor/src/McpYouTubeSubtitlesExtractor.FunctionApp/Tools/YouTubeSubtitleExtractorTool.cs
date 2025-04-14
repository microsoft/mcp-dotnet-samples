using System.Text;

using Aliencube.YouTubeSubtitlesExtractor.Abstractions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace AzureSamples.McpYouTubeSubtitlesExtractor.FunctionApp.Tools;

public class YouTubeSubtitleExtractorTool(IYouTubeVideo video, ILogger<YouTubeSubtitleExtractorTool> logger)
{
    [Function(nameof(GetSubtitleAsync))]
    public async Task<string> GetSubtitleAsync(
        [McpToolTrigger("get_subtitle", "Gets the subtitle in a given language from the given YouTube link.")] ToolInvocationContext context,
        [McpToolProperty("videoUrl", "string", "The YouTube video URL.")] string videoUrl,
        [McpToolProperty("languageCode", "string", "The two-letter language code.")] string languageCode
    )
    {
        var subtitle = await video.ExtractSubtitleAsync(videoUrl, languageCode).ConfigureAwait(false);
        if (subtitle == null)
        {
            logger.LogError("Subtitle not found for video URL: {videoUrl} and language code: {languageCode}", videoUrl, languageCode);
            return "No subtitle found.";
        }

        if (subtitle.Content!.Count == 0)
        {
            logger.LogError("No subtitle content found for video URL: {videoUrl} and language code: {languageCode}", videoUrl, languageCode);
            return "No subtitle content found.";
        }

        var sb = new StringBuilder();
        foreach (var content in subtitle.Content)
        {
            sb.AppendLine(content.Text ?? string.Empty);
        }

        logger.LogInformation("Subtitle for video URL: {videoUrl} and language code: {languageCode} has been processed", videoUrl, languageCode);

        return sb.ToString();
    }

    [Function(nameof(GetAvailableLanguagesAsync))]
    public async Task<IEnumerable<string>> GetAvailableLanguagesAsync(
        [McpToolTrigger("get_available_languages", "Gets the list of available languages for the given YouTube link.")] ToolInvocationContext context,
        [McpToolProperty("videoUrl", "string", "The YouTube video URL.")] string videoUrl
    )
    {
        var details = await video.ExtractVideoDetailsAsync(videoUrl).ConfigureAwait(false);
        if (details == null)
        {
            logger.LogError("No video details found for video URL: {videoUrl}", videoUrl);
            return [];
        }

        if (details.AvailableLanguageCodes.Count == 0)
        {
            logger.LogError("No available languages found for video URL: {videoUrl}", videoUrl);
            return [];
        }

        logger.LogInformation("Available languages for video URL: {videoUrl} are {languageCodes}", videoUrl, string.Join(", ", details.AvailableLanguageCodes));

        return details.AvailableLanguageCodes;
    }
}
