using McpSamples.MarkdownToHtml.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<MarkdownToHtmlAppSettings>(builder.Configuration, args);

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
