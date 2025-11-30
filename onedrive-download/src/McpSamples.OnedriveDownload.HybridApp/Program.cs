using Azure.Core;
using Azure.Identity;
using System.Text.Json;

using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.OnedriveDownload.HybridApp.Authentication;
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

// ★ Token Passthrough: 클라이언트(VSCode)가 보낸 Authorization 헤더에서 토큰을 꺼내 사용
// VSCode에서 Microsoft 인증을 하면 팝업이 뜨고, 토큰을 요청 헤더에 포함시켜 보냄
builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor.HttpContext;

    // ★★★ [핵심] /authorize 요청이면 토큰 검사 건너뛰기 (리다이렉트 될 것이므로)
    if (httpContext != null && httpContext.Request.Path.Value!.StartsWith("/authorize", StringComparison.OrdinalIgnoreCase))
    {
        return new GraphServiceClient(new AnonymousTokenCredential());
    }

    // 요청 헤더에서 Authorization: Bearer <token> 형태로 토큰을 꺼냄
    string? authHeader = httpContext?.Request.Headers.Authorization.ToString();
    string? accessToken = null;

    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        accessToken = authHeader.Substring("Bearer ".Length).Trim();
    }

    // 토큰이 없으면 Anonymous 반환 (미들웨어에서 검증)
    if (string.IsNullOrEmpty(accessToken))
    {
        return new GraphServiceClient(new AnonymousTokenCredential());
    }

    Console.WriteLine("[인증 성공] 클라이언트가 토큰을 보냈습니다!");

    // 헤더에서 꺼낸 토큰을 사용하여 GraphServiceClient 생성
    TokenCredential credential = new BearerTokenCredential(accessToken);
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

    // ★ 토큰 검증 미들웨어: /authorize는 MS 로그인으로 리다이렉트, 나머지는 401 반환
    webApp.Use(async (context, next) =>
    {
        string? authHeader = context.Request.Headers.Authorization.ToString();

        // /authorize로 오는 요청이면 Microsoft 로그인 페이지로 리다이렉트
        if (context.Request.Path.Value!.StartsWith("/authorize", StringComparison.OrdinalIgnoreCase))
        {
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "4446b888-18c6-4739-99e7-663e14fb338e";
            var redirectUri = "http://localhost";
            var scopes = "https://graph.microsoft.com/.default";

            var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
                $"client_id={clientId}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                $"response_type=code&" +
                $"scope={Uri.EscapeDataString(scopes)}";

            context.Response.Redirect(authUrl);
            return;
        }

        // 일반 요청은 토큰 검사
        if (string.IsNullOrEmpty(authHeader))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Access Token is required.");
            return;
        }
        await next();
    });

    // ★ wwwroot 폴더의 정적 파일(HTML, CSS 등)을 URL로 접근 가능하게 함
    webApp.UseStaticFiles();

    // Azure Functions 환경에서 wwwroot 경로 무시 처리
    if (!System.IO.Directory.Exists(webApp.Environment.WebRootPath))
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"[WARNING] WebRootPath not found: {webApp.Environment.WebRootPath}");
    }

    // ★ OAuth2 콜백 엔드포인트: Microsoft에서 code를 받으면 여기로 리다이렉트됨
    webApp.MapGet("/", async (HttpContext context) =>
    {
        var code = context.Request.Query["code"].ToString();

        if (string.IsNullOrEmpty(code))
        {
            await context.Response.WriteAsync("OAuth2 callback endpoint. Waiting for authorization code...");
            return;
        }

        // 인증 코드를 토큰으로 교환
        using var client = new HttpClient();
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "4446b888-18c6-4739-99e7-663e14fb338e";
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "redirect_uri", "http://localhost" },
            { "grant_type", "authorization_code" },
            { "scope", "https://graph.microsoft.com/.default" }
        });

        try
        {
            var response = await client.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                tokenRequest);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();

                // VSCode로 토큰 반환 (로컬 저장소 또는 HTTP 헤더로)
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync($@"
                    <html>
                    <head><title>인증 성공</title></head>
                    <body>
                        <h1>인증 성공!</h1>
                        <p>이제 VSCode에서 MCP 요청을 사용할 수 있습니다.</p>
                        <p style='color: green; font-weight: bold;'>토큰이 획득되었습니다.</p>
                        <script>
                            // 토큰을 localStorage에 저장하거나 parent window에 전달
                            if (window.opener) {{
                                window.opener.postMessage({{
                                    type: 'oauth_token',
                                    token: '{accessToken}'
                                }}, '*');
                                window.close();
                            }} else {{
                                localStorage.setItem('accessToken', '{accessToken}');
                                console.log('Token saved to localStorage');
                            }}
                        </script>
                    </body>
                    </html>
                ");
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync($"Token exchange failed: {content}");
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    });

    webApp.MapOpenApi("/{documentName}.json");

    var logger2 = app.Services.GetRequiredService<ILogger<Program>>();
    logger2.LogInformation("╔════════════════════════════════════════════════════════════════╗");
    logger2.LogInformation("║         MCP OneDrive Download Server Started                   ║");
    logger2.LogInformation("║     Interactive Browser Authentication Enabled                ║");
    logger2.LogInformation("║     사용자 대화형 인증이 활성화되었습니다                      ║");
    logger2.LogInformation("╚════════════════════════════════════════════════════════════════╝");
}

await app.RunAsync();
