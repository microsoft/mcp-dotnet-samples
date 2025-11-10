using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.OpenApiToSdk.HybridApp.Services;
using McpSamples.OpenApiToSdk.HybridApp.Tools; // Added for OpenApiToSdkTool
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi; // Added for OpenAPI

using Microsoft.OpenApi.Models; // Added for OpenApiSpecVersion

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OpenApiToSdkAppSettings>(builder.Configuration, args);

builder.Services.AddHttpClient<IOpenApiService, OpenApiService>();
builder.Services.AddScoped<OpenApiToSdkTool>(); // Re-added explicit registration

if (useStreamableHttp == true)
{
    builder.Services.AddHttpContextAccessor(); // Re-added
    builder.Services.AddOpenApi("swagger", o =>
    {
        o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
        o.AddDocumentTransformer<McpDocumentTransformer<OpenApiToSdkAppSettings>>();
    });
    builder.Services.AddOpenApi("openapi", o =>
    {
        o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
        o.AddDocumentTransformer<McpDocumentTransformer<OpenApiToSdkAppSettings>>();
    });
}

IHost app = builder.BuildApp(useStreamableHttp);

if (useStreamableHttp == true)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
    (app as WebApplication)!.UseStaticFiles(); // Re-added
}

await app.RunAsync();