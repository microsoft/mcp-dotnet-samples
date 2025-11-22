using McpSamples.PptFontFix.HybridApp.Configurations;
using McpSamples.PptFontFix.HybridApp.Services;
using McpSamples.PptFontFix.HybridApp.Tools;
using McpSamples.PptFontFix.HybridApp.Prompts;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<PptFontFixAppSettings>(builder.Configuration, args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPptFontFixService, PptFontFixService>();

builder.Services.AddTransient<IPptFontFixTool, PptFontFixTool>();
builder.Services.AddSingleton<IPptFontFixPrompt, PptFontFixPrompt>();

IHost app = builder.BuildApp(useStreamableHttp);

if (app is WebApplication webApp)
{
    webApp.UseStaticFiles(); 
}

await app.RunAsync();