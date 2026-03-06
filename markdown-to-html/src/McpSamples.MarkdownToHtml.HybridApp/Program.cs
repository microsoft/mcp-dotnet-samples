using System.Text.RegularExpressions;

using McpSamples.MarkdownToHtml.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<MarkdownToHtmlAppSettings>(builder.Configuration, args);

builder.Services.AddSingleton<Regex>(sp =>
{
    var regex = new Regex("\\<pre\\>\\<code class=\"language\\-(.+)\"\\>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    return regex;
});

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
