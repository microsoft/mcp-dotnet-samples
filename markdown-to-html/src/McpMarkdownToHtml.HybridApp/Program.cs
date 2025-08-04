using McpMarkdownToHtml.Common.Configurations;
using McpMarkdownToHtml.Common.Extensions;
using McpMarkdownToHtml.HybridApp.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings(builder.Configuration, args);

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
