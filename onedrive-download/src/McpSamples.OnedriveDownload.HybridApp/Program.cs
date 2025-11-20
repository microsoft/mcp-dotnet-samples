using Azure.Core;
using Azure.Identity;
using McpSamples.OnedriveDownload.HybridApp.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.DependencyCollector;
using McpSamples.OnedriveDownload.HybridApp.Services;
using McpSamples.OnedriveDownload.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Graph;

using Constants = McpSamples.OnedriveDownload.HybridApp.Constants;

// Check if running in provisioning mode (during azd provision)
var isProvisioning = args.Contains("--provision", StringComparer.InvariantCultureIgnoreCase);
if (isProvisioning)
{
    await ProvisionRefreshToken.ProvisionAsync();
    return;
}

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

// Map OnedriveDownload__EntraId__Personal365RefreshToken environment variable to configuration
var personal365RefreshToken = Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__Personal365RefreshToken");
if (!string.IsNullOrEmpty(personal365RefreshToken))
{
    builder.Configuration["EntraId:Personal365RefreshToken"] = personal365RefreshToken;
}

// Add Application Insights for proper Azure logging FIRST
if (useStreamableHttp == true)
{
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Logging.AddApplicationInsights();
}

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

// Add authentication service (user-delegated with automatic token from HTTP context)
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();

// Add controllers
builder.Services.AddControllers();

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

// Debug: Log configuration values after app is built
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("*** [Config] TenantId: {TenantId}", builder.Configuration["OnedriveDownload:EntraId:TenantId"]);
logger.LogInformation("*** [Config] UserAssignedClientId: {UserAssignedClientId}", builder.Configuration["OnedriveDownload:EntraId:UserAssignedClientId"]);
logger.LogInformation("*** [Config] ClientId: {ClientId}", builder.Configuration["OnedriveDownload:EntraId:ClientId"]);
logger.LogInformation("*** [Config] ClientSecret: {ClientSecret}", string.IsNullOrEmpty(builder.Configuration["OnedriveDownload:EntraId:ClientSecret"]) ? "NOT SET" : "SET");

// Debug: Log environment variables directly
logger.LogInformation("*** [ENV] OnedriveDownload__EntraId__TenantId: {TenantId}", Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__TenantId"));
logger.LogInformation("*** [ENV] OnedriveDownload__EntraId__UserAssignedClientId: {UserAssignedClientId}", Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__UserAssignedClientId"));
logger.LogInformation("*** [ENV] OnedriveDownload__EntraId__ClientId: {ClientId}", Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__ClientId"));
logger.LogInformation("*** [ENV] OnedriveDownload__EntraId__ClientSecret: {ClientSecret}", Environment.GetEnvironmentVariable("OnedriveDownload__EntraId__ClientSecret"));

// Debug: Log all environment variables that start with "OnedriveDownload"
logger.LogInformation("*** [ENV] All OnedriveDownload variables:");
foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
{
    if (envVar.Key?.ToString()?.StartsWith("OnedriveDownload", StringComparison.OrdinalIgnoreCase) == true)
    {
        logger.LogInformation("*** [ENV] {Key}={Value}", envVar.Key, envVar.Value);
    }
}

if (useStreamableHttp == true)
{
    var webApp = (app as Microsoft.AspNetCore.Builder.WebApplication)!;
    webApp.MapOpenApi("/{documentName}.json");
    webApp.MapControllers();

    logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
    logger.LogInformation("║         MCP OneDrive Download Server 시작됨 (Started)         ║");
    logger.LogInformation("║     사용자 위임 방식으로 OneDrive에 접근합니다                 ║");
    logger.LogInformation("║     User-delegated access to OneDrive enabled                  ║");
    logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");
}

await app.RunAsync();