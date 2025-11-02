using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.OpenApiToSdk.HybridApp.Services;

/// <summary>
/// Entry point for the OpenAPI to SDK MCP server.
/// </summary>
var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OpenApiToSdkAppSettings>(builder.Configuration, args);

// 추가: OpenAPI 서비스 등록
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOpenApiService, OpenApiService>();

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();