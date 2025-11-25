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

// ============================================================
// 1. [.env 로더 추가] 프로그램 시작하자마자 환경 변수 파일 로드
// ============================================================
LoadEnvFile(".env.local");

// azd 환경(.azure/...)도 로드하려면 아래 로직 추가 가능 (선택)
var azdEnvName = Environment.GetEnvironmentVariable("AZURE_ENV_NAME");
if (!string.IsNullOrEmpty(azdEnvName))
{
    var azdPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".azure", azdEnvName, ".env");
    LoadEnvFile(azdPath);
}

// Check if running in provisioning mode (during azd provision)
var isProvisioning = args.Contains("--provision", StringComparer.InvariantCultureIgnoreCase);
if (isProvisioning)
{
    // Load configuration for provisioning
    var configBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    var config = configBuilder.Build();

    await ProvisionRefreshToken.ProvisionAsync(config);
    return;
}

// ★ 서버 시작 전에 token 확인 - token이 없으면 로그인 화면 띄우기
var refreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");
if (string.IsNullOrEmpty(refreshToken))
{
    Console.WriteLine("\n========================================");
    Console.WriteLine("Refresh Token not found!");
    Console.WriteLine("Starting authentication flow...");
    Console.WriteLine("========================================\n");

    // 임시 configuration 생성
    var tempConfigBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
    var tempConfig = tempConfigBuilder.Build();

    // 로그인 화면 띄우기
    await ProvisionRefreshToken.ProvisionAsync(tempConfig);

    // token이 저장되었으니 환경변수 다시 로드
    refreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");
    Console.WriteLine($"\n✓ Token loaded successfully\n");
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

// Map PERSONAL_365_REFRESH_TOKEN environment variable to configuration
var personal365RefreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");
Console.WriteLine($"[DEBUG] PERSONAL_365_REFRESH_TOKEN env var: {(string.IsNullOrEmpty(personal365RefreshToken) ? "NOT SET" : "SET (" + personal365RefreshToken.Length + " chars)")}");
if (!string.IsNullOrEmpty(personal365RefreshToken))
{
    builder.Configuration["EntraId:Personal365RefreshToken"] = personal365RefreshToken;
    Console.WriteLine($"[DEBUG] Set EntraId:Personal365RefreshToken in configuration");
}
else
{
    Console.WriteLine($"[DEBUG] PERSONAL_365_REFRESH_TOKEN is empty or null - not setting configuration");
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

// Add Azure File Share Sync Service
builder.Services.AddSingleton<AzureFileShareSyncService>();

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

    // ★ wwwroot 폴더의 정적 파일(HTML, CSS, 다운로드 파일 등)을 URL로 접근 가능하게 함
    webApp.UseStaticFiles();

    webApp.MapOpenApi("/{documentName}.json");
    webApp.MapControllers();

    logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
    logger.LogInformation("║         MCP OneDrive Download Server 시작됨 (Started)         ║");
    logger.LogInformation("║     사용자 위임 방식으로 OneDrive에 접근합니다                 ║");
    logger.LogInformation("║     User-delegated access to OneDrive enabled                  ║");
    logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");
}

await app.RunAsync();

// ============================================================
// Helper 함수: .env 파일 로더
// ============================================================
static void LoadEnvFile(string filePath)
{
    if (!System.IO.File.Exists(filePath)) return;

    foreach (var line in System.IO.File.ReadAllLines(filePath))
    {
        // 주석이나 빈 줄 무시
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

        // KEY=VALUE 분리
        var parts = line.Split('=', 2);
        if (parts.Length != 2) continue;

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        // 값에 따옴표가 있으면 제거 ("VALUE" -> VALUE)
        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
        {
            value = value.Substring(1, value.Length - 2);
        }

        // 환경 변수 설정 (현재 프로세스 내에서만 유효)
        Environment.SetEnvironmentVariable(key, value);
    }
    Console.WriteLine($"[INFO] 환경 변수 파일 로드 완료: {filePath}");
}