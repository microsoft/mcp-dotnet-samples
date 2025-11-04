using Azure.Core;
using Azure.Identity;

using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

using Microsoft.Graph;

var envs = Environment.GetEnvironmentVariables();
var useStreamableHttp = AppSettings.UseStreamableHttp(envs, args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

if (useStreamableHttp == true)
{
    var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT") ?? "7071";
    (builder as WebApplicationBuilder)!.WebHost.UseUrls($"http://localhost:{port}");

    Console.WriteLine($"Listening on port {port}");
}

builder.Services.AddAppSettings<OnedriveDownloadAppSettings>(builder.Configuration, args);

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var settings = sp.GetRequiredService<OnedriveDownloadAppSettings>();
    var entraId = settings.EntraId;

    TokenCredential credential = entraId.UseManagedIdentity
                                   ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(entraId.UserAssignedClientId))
                                   : throw new InvalidOperationException("Managed Identity is not configured. This server is designed for Azure deployment.");

    string[] scopes = { "https://graph.microsoft.com/.default" };
    var client = new GraphServiceClient(credential, scopes);

    return client;
});

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();