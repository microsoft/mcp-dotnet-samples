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

// ★ GraphServiceClient 등록 (InteractiveBrowserCredential 기반)
builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var settings = sp.GetRequiredService<OnedriveDownloadAppSettings>();
    var entraId = settings.EntraId;

    TokenCredential credential = entraId.UseManagedIdentity
                                   ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(entraId.UserAssignedClientId))
                                   : new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
                                   {
                                       TenantId = entraId.TenantId,
                                       ClientId = entraId.ClientId,
                                       AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                                   });

    string[] scopes = [ Constants.DefaultScope ];
    var client = new GraphServiceClient(credential, scopes);

    return client;
});

// Add Application Insights for proper Azure logging
if (useStreamableHttp == true)
{
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Logging.AddApplicationInsights();
}

if (useStreamableHttp == true)
{
    var port = Environment.GetEnvironmentVariable(Constants.AzureFunctionsCustomHandlerPortEnvironmentKey) ?? $"{Constants.DefaultAppPort}";
    (builder as Microsoft.AspNetCore.Builder.WebApplicationBuilder)!.WebHost.UseUrls(string.Format(Constants.DefaultAppUrl, port));

    Console.WriteLine($"[INFO] Listening on port {port}");
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
        Console.WriteLine($"[WARNING] Failed to load Key Vault: {ex.Message}");
    }
}

// Add authentication service
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();

// Add Azure File Share Sync Service
builder.Services.AddSingleton<AzureFileShareSyncService>();

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

    // ★ wwwroot 폴더의 정적 파일(HTML, CSS 등)을 URL로 접근 가능하게 함
    webApp.UseStaticFiles();

    // Azure Functions 환경에서 wwwroot 경로 무시 처리
    if (!System.IO.Directory.Exists(webApp.Environment.WebRootPath))
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"[WARNING] WebRootPath not found: {webApp.Environment.WebRootPath}");
    }

    webApp.MapOpenApi("/{documentName}.json");

    var logger2 = app.Services.GetRequiredService<ILogger<Program>>();
    logger2.LogInformation("╔════════════════════════════════════════════════════════════════╗");
    logger2.LogInformation("║         MCP OneDrive Download Server Started                   ║");
    logger2.LogInformation("║     Interactive Browser Authentication Enabled                ║");
    logger2.LogInformation("║     사용자 대화형 인증이 활성화되었습니다                      ║");
    logger2.LogInformation("╚════════════════════════════════════════════════════════════════╝");
}

await app.RunAsync();
