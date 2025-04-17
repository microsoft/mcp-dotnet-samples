using System.ComponentModel;
using System.Text;

using Aliencube.YouTubeSubtitlesExtractor.Abstractions;

using ModelContextProtocol.Server;

namespace McpYouTubeSubtitlesExtractor.ContainerApp.Tools;

[McpServerToolType]
public class YouTubeSubtitleExtractorTool(IYouTubeVideo video, ILogger<YouTubeSubtitleExtractorTool> logger)
{
    [McpServerTool(Name = "get_subtitle", Title = "Get Subtitle")]
    [Description("Gets the subtitle in a given language from the given YouTube link.")]
    public async Task<string> GetSubtitleAsync(
        [Description("The YouTube video URL.")] string videoUrl,
        [Description("The two-letter language code.")] string languageCode
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

    [McpServerTool(Name = "get_available_languages", Title = "Get Available Languages")]
    [Description("Gets the list of available languages for the given YouTube link.")]
    public async Task<IEnumerable<string>> GetAvailableLanguagesAsync(
        [Description("The YouTube video URL.")] string videoUrl
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
