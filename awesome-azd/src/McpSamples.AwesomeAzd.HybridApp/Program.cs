using McpSamples.AwesomeAzd.HybridApp.Services;
using McpSamples.AwesomeAzd.HybridApp.Configurations;
using McpSamples.AwesomeAzd.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<AwesomeAzdAppSettings>(builder.Configuration, args);

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
