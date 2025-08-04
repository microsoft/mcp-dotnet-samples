using System.Text.RegularExpressions;

using McpMarkdownToHtml.Common.Configurations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpMarkdownToHtml.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppSettings(this IServiceCollection services, IConfiguration config, string[] args)
    {
        services.AddSingleton<HtmlSettings>(_ =>
        {
            var settings = AppSettings.Parse(config, args);

            return settings.Html;
        });

        services.AddSingleton<Regex>(sp =>
        {
            var regex = new Regex("\\<pre\\>\\<code class=\"language\\-(.+)\"\\>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            return regex;
        });

        return services;
    }
}
