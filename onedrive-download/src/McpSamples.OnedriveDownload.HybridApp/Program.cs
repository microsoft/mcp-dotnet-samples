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
using System.Text.Json;

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

// ★ [주석 처리] 서버 시작 전에 token 확인 - token이 없으면 로그인 화면 띄우기
// 이 로직이 MCP 서버 시작을 막으므로 주석 처리
// var refreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");
// if (string.IsNullOrEmpty(refreshToken))
// {
//     Console.WriteLine("\n========================================");
//     Console.WriteLine("Refresh Token not found!");
//     Console.WriteLine("Starting authentication flow...");
//     Console.WriteLine("========================================\n");
//
//     // 임시 configuration 생성
//     var tempConfigBuilder = new ConfigurationBuilder()
//         .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
//     var tempConfig = tempConfigBuilder.Build();
//
//     // 로그인 화면 띄우기
//     await ProvisionRefreshToken.ProvisionAsync(tempConfig);
//
//     // token이 저장되었으니 환경변수 다시 로드
//     refreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");
//     Console.WriteLine($"\n✓ Token loaded successfully\n");
//
//     // ★ Azure Function App 설정에 token 저장 (Azure 배포 환경)
//     if (!string.IsNullOrEmpty(refreshToken))
//     {
//         await SaveTokenToAzureFunctionAppAsync(refreshToken);
//     }
// }

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

// ★ OAuthTokenStore 등록 (OAuth2 토큰 저장용)
var tokenStore = new OAuthTokenStore();
builder.Services.AddSingleton(tokenStore);

// ★ 환경 변수에서 EntraId 설정값 로드
// postprovision hook에서 설정한 값을 먼저 확인
var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
    ?? builder.Configuration["EntraId:TenantId"]
    ?? Environment.GetEnvironmentVariable("TENANT_ID");
var clientId = builder.Configuration["EntraId:ClientId"]  // postprovision에서 설정한 ClientId
    ?? Environment.GetEnvironmentVariable("OAUTH_CLIENT_ID");
var clientSecret = builder.Configuration["EntraId:ClientSecret"]
    ?? Environment.GetEnvironmentVariable("CLIENT_SECRET");
var personal365RefreshToken = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");

// Configuration에 환경 변수 값 설정
if (!string.IsNullOrEmpty(tenantId))
{
    builder.Configuration["EntraId:TenantId"] = tenantId;
    Console.WriteLine($"[DEBUG] EntraId:TenantId = {tenantId}");
}

if (!string.IsNullOrEmpty(clientId))
{
    builder.Configuration["EntraId:ClientId"] = clientId;
    Console.WriteLine($"[DEBUG] EntraId:ClientId loaded");
}

if (!string.IsNullOrEmpty(clientSecret))
{
    builder.Configuration["EntraId:ClientSecret"] = clientSecret;
    Console.WriteLine($"[DEBUG] EntraId:ClientSecret loaded");
}

if (!string.IsNullOrEmpty(personal365RefreshToken))
{
    builder.Configuration["EntraId:Personal365RefreshToken"] = personal365RefreshToken;
    Console.WriteLine($"[DEBUG] EntraId:Personal365RefreshToken loaded");
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

    // Azure Functions 환경에서 wwwroot 경로 무시 처리
    if (!System.IO.Directory.Exists(webApp.Environment.WebRootPath))
    {
        logger.LogWarning($"WebRootPath not found: {webApp.Environment.WebRootPath}");
    }

    webApp.MapOpenApi("/{documentName}.json");

    // ★ OAuth2 Authorization Code Flow 로그인
    webApp.MapGet("/login", (HttpContext context, IConfiguration config) =>
    {
        var settings = app.Services.GetRequiredService<OnedriveDownloadAppSettings>();
        var tenantId = settings.EntraId?.TenantId ?? string.Empty;
        var clientId = settings.EntraId?.ClientId ?? string.Empty;

        // Always use HTTPS for Azure App Service
        var scheme = context.Request.Host.Host.Contains("azurewebsites.net") ? "https" : context.Request.Scheme;
        var redirectUri = $"{scheme}://{context.Request.Host}/auth/callback";
        var scope = "Files.Read offline_access"; // ★ Delegated Scope (내 파일만 접근)

        // Microsoft 로그인 페이지 주소 생성
        var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
                      $"?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&response_type=code" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&scope={Uri.EscapeDataString(scope)}" +
                      $"&response_mode=query";

        // 사용자를 Microsoft 로그인 페이지로 리디렉트
        context.Response.Redirect(authUrl);
        return Task.CompletedTask;
    });

    // ★ OAuth2 Callback (Microsoft에서 Authorization Code 받기)
    webApp.MapGet("/auth/callback", async (HttpContext context, IConfiguration config, OAuthTokenStore store) =>
    {
        var code = context.Request.Query["code"];
        var error = context.Request.Query["error"];

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogError($"❌ Microsoft 로그인 오류: {error}");
            return Results.Content($"<h1>로그인 실패</h1><p>오류: {error}</p>", "text/html");
        }

        if (string.IsNullOrEmpty(code))
        {
            return Results.BadRequest("Authorization code가 없습니다.");
        }

        try
        {
            var settings = app.Services.GetRequiredService<OnedriveDownloadAppSettings>();
            var tenantId = settings.EntraId?.TenantId ?? string.Empty;
            var clientId = settings.EntraId?.ClientId ?? string.Empty;
            var clientSecret = settings.EntraId?.ClientSecret ?? string.Empty;

            // Always use HTTPS for Azure App Service
            var scheme = context.Request.Host.Host.Contains("azurewebsites.net") ? "https" : context.Request.Scheme;
            var redirectUri = $"{scheme}://{context.Request.Host}/auth/callback";

            logger.LogInformation($"[DEBUG] TenantId: {tenantId}");
            logger.LogInformation($"[DEBUG] ClientId: {clientId}");
            logger.LogInformation($"[DEBUG] RedirectUri: {redirectUri}");

            // ★ Authorization Code를 Token으로 교환
            using var httpClient = new HttpClient();

            // Client Secret이 있으면 기밀 클라이언트, 없으면 공개 클라이언트 흐름
            var tokenParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code.ToString()),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("scope", "Files.Read offline_access")
            };

            // Client Secret이 있으면 추가
            if (!string.IsNullOrEmpty(clientSecret))
            {
                tokenParams.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
            }

            var tokenRequest = new FormUrlEncodedContent(tokenParams);

            var tokenResponse = await httpClient.PostAsync(
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                tokenRequest);

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogError($"❌ 토큰 교환 실패: {tokenContent}");
                return Results.Content($"<h1>토큰 교환 실패</h1><p>{tokenContent}</p>", "text/html");
            }

            using var doc = JsonDocument.Parse(tokenContent);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var refreshToken = doc.RootElement.GetProperty("refresh_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            // ★ 토큰을 저장소에 저장
            store.AccessToken = accessToken ?? string.Empty;
            store.RefreshToken = refreshToken ?? string.Empty;
            store.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

            logger.LogInformation("✅ OAuth2 로그인 성공! 토큰 저장됨.");

            return Results.Content(@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>OneDrive MCP 로그인 성공</title>
                    <style>
                        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; }
                        .success { color: green; font-size: 18px; }
                        .info { margin-top: 20px; color: #666; }
                    </style>
                </head>
                <body>
                    <h1>✅ 로그인 성공!</h1>
                    <p class='success'>Microsoft OneDrive 인증이 완료되었습니다.</p>
                    <p class='info'>MCP 서버가 이제 OneDrive에 접근할 수 있습니다.</p>
                    <p class='info'>이 창을 닫으셔도 됩니다.</p>
                </body>
                </html>
            ", "text/html");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ 콜백 처리 중 오류 발생");
            return Results.Content($"<h1>오류 발생</h1><p>{ex.Message}</p>", "text/html");
        }
    });

    // ★ OAuth 인증 상태 확인 엔드포인트 - Azure 환경에서 브라우저 로그인 불가능할 때 사용
    webApp.MapGet("/auth/status", async (HttpContext context) =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        var token = Environment.GetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN");

        if (!string.IsNullOrEmpty(token))
        {
            await context.Response.WriteAsync("""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8">
                    <title>인증 완료</title>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 50px; text-align: center; }
                        .success { color: green; font-size: 18px; }
                    </style>
                </head>
                <body>
                    <h1>✓ 인증 완료 (Authentication Complete)</h1>
                    <p class="success">Refresh Token이 이미 설정되었습니다.</p>
                    <p>The Refresh Token is already configured.</p>
                </body>
                </html>
                """);
        }
        else
        {
            await context.Response.WriteAsync("""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8">
                    <title>인증 대기 중</title>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 50px; text-align: center; }
                        .info { font-size: 16px; color: #333; }
                    </style>
                </head>
                <body>
                    <h1>OneDrive 인증 필요</h1>
                    <p class="info">Refresh Token이 설정되지 않았습니다.</p>
                    <p class="info">애플리케이션을 재시작하면 인증 화면이 표시됩니다.</p>
                    <p class="info">로그를 확인해주세요.</p>
                </body>
                </html>
                """);
        }
    });

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

// ★ OAuth2 토큰을 환경 변수에 저장하는 클래스
public class OAuthTokenStore
{
    public string? AccessToken
    {
        get => Environment.GetEnvironmentVariable("OAUTH_ACCESS_TOKEN");
        set => Environment.SetEnvironmentVariable("OAUTH_ACCESS_TOKEN", value);
    }

    public string? RefreshToken
    {
        get => Environment.GetEnvironmentVariable("OAUTH_REFRESH_TOKEN");
        set => Environment.SetEnvironmentVariable("OAUTH_REFRESH_TOKEN", value);
    }

    public DateTime ExpiresAt
    {
        get
        {
            var expiresAtStr = Environment.GetEnvironmentVariable("OAUTH_EXPIRES_AT");
            if (DateTime.TryParse(expiresAtStr, out var expiresAt))
            {
                return expiresAt;
            }
            return DateTime.MinValue;
        }
        set => Environment.SetEnvironmentVariable("OAUTH_EXPIRES_AT", value.ToString("O"));
    }
}