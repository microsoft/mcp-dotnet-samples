using McpSamples.PptFontFix.HybridApp.Configurations;
using McpSamples.PptFontFix.HybridApp.Services;
using McpSamples.PptFontFix.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<PptFontFixAppSettings>(builder.Configuration, args);
builder.Services.AddSingleton<IPptFontFixService, PptFontFixService>();

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();