using Azure.Core;
using Azure.Identity;
using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Graph;

using Constants = McpSamples.OnedriveDownload.HybridApp.Constants;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// Force HTTP mode unless --stdio is explicitly passed
if (!args.Contains("--stdio", StringComparer.InvariantCultureIgnoreCase))
{
    useStreamableHttp = true;
}

IHostApplicationBuilder builder = useStreamableHttp
                                ? Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OnedriveDownloadAppSettings>(builder.Configuration, args);

if (useStreamableHttp == true)
{
    var port = Environment.GetEnvironmentVariable(Constants.AzureFunctionsCustomHandlerPortEnvironmentKey) ?? $"{Constants.DefaultAppPort}";
    (builder as Microsoft.AspNetCore.Builder.WebApplicationBuilder)!.WebHost.UseUrls(string.Format(Constants.DefaultAppUrl, port));

    Console.WriteLine($"Listening on port {port}");
    builder.Services.AddHttpContextAccessor();
}

// Add Azure Key Vault configuration
var keyVaultName = builder.Configuration["KeyVaultName"];
if (!string.IsNullOrEmpty(keyVaultName))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri($"https://{keyVaultName}.vault.azure.net/"),
            new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        // Log but don't fail - Key Vault might not be available in all environments
        Console.WriteLine($"Warning: Failed to load Key Vault: {ex.Message}");
    }
}

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var settings = sp.GetRequiredService<OnedriveDownloadAppSettings>();
    var entraId = settings.EntraId;

    // Always use Managed Identity in Azure
    // When running in Azure Functions behind APIM, the Managed Identity is the recommended approach
    if (string.IsNullOrEmpty(entraId.UserAssignedClientId))
    {
        throw new InvalidOperationException("UserAssignedClientId must be configured. Set OnedriveDownload__EntraId__UserAssignedClientId in environment variables or configuration.");
    }

    try
    {
        TokenCredential credential = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(entraId.UserAssignedClientId)
        );

        string[] scopes = { "https://graph.microsoft.com/.default" };
        var client = new GraphServiceClient(credential, scopes);

        return client;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to initialize GraphServiceClient with UserAssignedClientId '{entraId.UserAssignedClientId}': {ex.Message}", ex);
    }
});

if (useStreamableHttp == true)
{
    builder.Services.AddOpenApi("swagger", o =>
    {
        o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
        o.AddDocumentTransformer<McpDocumentTransformer<OnedriveDownloadAppSettings>>();
    });
    builder.Services.AddOpenApi("openapi", o =>
    {
        o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
        o.AddDocumentTransformer<McpDocumentTransformer<OnedriveDownloadAppSettings>>();
    });
}

IHost app = builder.BuildApp(useStreamableHttp);

if (useStreamableHttp == true)
{
    var webApp = (app as Microsoft.AspNetCore.Builder.WebApplication)!;
    webApp.MapOpenApi("/{documentName}.json");
}

await app.RunAsync();