using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Personal 365 Refresh Token 프로비저닝 도구 (MSAL 기반)
/// Node.js 로직을 C#로 포팅했습니다.
/// </summary>
public class ProvisionRefreshToken
{
    private const string TenantId = "consumers"; // 개인 계정 필수
    private static readonly string DefaultClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e"; // Microsoft 공개 Client ID

    public static async Task ProvisionAsync(IConfiguration? configuration = null)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Personal 365 Refresh Token Provisioning");
        Console.WriteLine("========================================\n");

        try
        {
            // Step 0: ClientId 설정에서 읽기
            var clientId = configuration?["EntraId:ClientId"]
                        ?? configuration?["OnedriveDownload:EntraId:ClientId"]
                        ?? DefaultClientId;
            Console.WriteLine($"Using ClientId: {clientId}\n");

            // Step 1: 환경 파일 경로 확인
            var envName = Environment.GetEnvironmentVariable("AZURE_ENV_NAME");
            string envFilePath;

            if (!string.IsNullOrEmpty(envName))
            {
                envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".azure", envName, ".env");
            }
            else
            {
                envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
            }

            Console.WriteLine($"Step 1: Checking for existing token in {envFilePath}");

            // Check both Azure env file and .env.local
            var filesToCheck = new List<string>();

            if (!string.IsNullOrEmpty(envName))
            {
                var possibleRoots = new[]
                {
                    Directory.GetCurrentDirectory(),
                    Path.Combine(Directory.GetCurrentDirectory(), ".."),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", ".."),
                };

                foreach (var root in possibleRoots)
                {
                    var path = Path.Combine(root, ".azure", envName, ".env");
                    if (File.Exists(path))
                    {
                        filesToCheck.Add(path);
                        break;
                    }
                }
            }

            filesToCheck.Add(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));

            // Check if token exists in any file
            foreach (var fileToCheck in filesToCheck)
            {
                Console.WriteLine($"[DEBUG] Checking file: {fileToCheck}");
                if (File.Exists(fileToCheck))
                {
                    Console.WriteLine($"[DEBUG] File exists, reading content...");
                    string envContent = File.ReadAllText(fileToCheck);
                    if (envContent.Contains("PERSONAL_365_REFRESH_TOKEN="))
                    {
                        Console.WriteLine($"✓ 이미 Refresh Token이 존재합니다 ({Path.GetFileName(fileToCheck)}). 건너뜁니다.\n");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Token not found in {Path.GetFileName(fileToCheck)}");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] File does not exist");
                }
            }

            // Step 2: HTTP 서버 시작 및 동적 포트 할당 (Kestrel - macOS/Linux/Windows 호환)
            Console.WriteLine("\nStep 2: Starting HTTP server on dynamic port...");
            int port = GetAvailablePort();
            string redirectUri = $"http://localhost:{port}";

            Console.WriteLine($"✓ Will listen on port: {port}");
            Console.WriteLine($"Redirect URI: {redirectUri}\n");

            // 인증 코드를 받을 TaskCompletionSource
            var authCodeTaskSource = new TaskCompletionSource<string>();

            // ASP.NET Core 최소 HTTP 서버 생성
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            // 인증 콜백 엔드포인트
            app.MapGet("/", async context =>
            {
                var code = context.Request.Query["code"].ToString();
                var state = context.Request.Query["state"].ToString();

                if (string.IsNullOrEmpty(code))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "No authorization code received" });
                    authCodeTaskSource.TrySetException(new Exception("No authorization code"));
                    return;
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync("인증 성공! 터미널을 확인하고 이 창을 닫으세요.");

                authCodeTaskSource.TrySetResult(code);
            });

            // 서버를 백그라운드에서 시작
            var serverTask = app.RunAsync($"http://localhost:{port}");
            await Task.Delay(500); // 서버가 시작될 때까지 대기

            // Step 3: MSAL 공개 클라이언트 애플리케이션 생성
            Console.WriteLine("Step 3: Creating MSAL PublicClientApplication...");
            IPublicClientApplication pca = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                .Build();

            Console.WriteLine("✓ MSAL app created\n");

            // Step 4: 인증 URL 생성
            Console.WriteLine("Step 4: Opening browser for authentication...");
            var authUrl = $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize?" +
                         $"client_id={Uri.EscapeDataString(clientId)}&" +
                         $"response_type=code&" +
                         $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                         $"response_mode=query&" +
                         $"scope={Uri.EscapeDataString("Files.Read User.Read offline_access")}";

            Console.WriteLine($"Auth URL: {authUrl}\n");

            // 브라우저 실행 로직 개선 (URL 잘림 방지)
            try
            {
                // Windows: URL을 따옴표로 감싸야 & 기호가 명령 구분자로 인식되지 않음
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start \"\" \"{authUrl}\"",
                        CreateNoWindow = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    System.Diagnostics.Process.Start("xdg-open", authUrl);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    System.Diagnostics.Process.Start("open", authUrl);
                }
            }
            catch
            {
                Console.WriteLine("브라우저를 자동으로 열 수 없습니다.");
                Console.WriteLine($"아래 URL로 리디렉션됩니다:\n{authUrl}\n");

                // ★ HTTP 리디렉션: Azure 환경에서 브라우저 자동 실행 불가능할 때
                // 엔드포인트를 통해 로그인 URL로 리디렉션
                app.MapGet("/auth/redirect", (HttpContext context) =>
                {
                    context.Response.Redirect(authUrl);
                });
            }

            Console.WriteLine("대기 중... (브라우저에서 로그인해주세요)\n");

            // Step 5: 인증 콜백 대기 (타임아웃 설정)
            string code;
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(5));
                code = await authCodeTaskSource.Task;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("✗ 인증 타임아웃 (5분 초과)");
                await app.StopAsync();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Authorization code를 받지 못했습니다: {ex.Message}");
                await app.StopAsync();
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("✗ Authorization code가 비어있습니다.");
                await app.StopAsync();
                return;
            }

            Console.WriteLine($"✓ Authorization code received: {code.Substring(0, 20)}...\n");

            // Step 6: 토큰 교환
            Console.WriteLine("Step 5: Exchanging authorization code for tokens...");
            string? refreshToken = await ExchangeCodeForToken(pca, code, redirectUri, clientId);

            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("✗ Refresh token을 얻을 수 없습니다.");
                await app.StopAsync();
                return;
            }

            Console.WriteLine("✓ Refresh token obtained successfully\n");

            // Step 7: .env.local 파일에 저장
            Console.WriteLine("Step 6: Saving refresh token to .env.local...");
            SaveRefreshToken(refreshToken);
            Console.WriteLine($"✓ Token saved to .env.local\n");

            // Step 8: .azure/{env}/.env에도 복사
            Console.WriteLine("Step 7: Copying refresh token to .azure environment file...");
            CopyTokenToAzureEnv(refreshToken);
            Console.WriteLine($"✓ Token copied to .azure environment\n");

            // 서버 정지
            await app.StopAsync();

            Console.WriteLine("========================================");
            Console.WriteLine("✓ Provisioning completed successfully!");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 사용 가능한 포트 찾기
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Authorization Code를 Refresh Token으로 교환
    /// </summary>
    private static async Task<string?> ExchangeCodeForToken(IPublicClientApplication pca, string code, string redirectUri, string clientId)
    {
        // 직접 REST API를 통해 token 교환
        return await GetRefreshTokenDirectly(code, redirectUri, clientId);
    }

    /// <summary>
    /// REST API를 통해 직접 Refresh Token 획득
    /// </summary>
    private static async Task<string?> GetRefreshTokenDirectly(string code, string redirectUri, string clientId)
    {
        try
        {
            using var client = new HttpClient();

            // Authorization code 교환 요청
            // scope은 authorization 단계에서 이미 설정되었으므로 여기서는 필요 없음
            var requestBodyString = $"client_id={Uri.EscapeDataString(clientId)}&" +
                                   $"code={Uri.EscapeDataString(code)}&" +
                                   $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                                   $"grant_type=authorization_code";

            var content = new StringContent(requestBodyString, Encoding.UTF8, "application/x-www-form-urlencoded");

            Console.WriteLine("Exchanging authorization code for token...");
            var response = await client.PostAsync(
                $"https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✗ Token endpoint error: {response.StatusCode}");
                Console.WriteLine($"Response: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("refresh_token", out var refreshTokenElement))
            {
                return refreshTokenElement.GetString();
            }

            Console.WriteLine("✗ No refresh_token in response");
            Console.WriteLine($"Response: {responseContent}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception during token exchange: {ex.Message}");
            return null;
        }
    }


    /// <summary>
    /// Refresh token을 azd 환경 파일과 .env.local에 저장
    /// </summary>
    private static void SaveRefreshToken(string refreshToken)
    {
        var envName = Environment.GetEnvironmentVariable("AZURE_ENV_NAME");

        // 목표 경로들
        List<string> targetPaths = new();

        // 1. .azure/{env}/.env (주요 대상)
        if (!string.IsNullOrEmpty(envName))
        {
            var possibleRoots = new[]
            {
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), ".."),
                Path.Combine(Directory.GetCurrentDirectory(), "..", ".."),
            };

            foreach (var root in possibleRoots)
            {
                var path = Path.Combine(root, ".azure", envName, ".env");
                if (File.Exists(path))
                {
                    targetPaths.Add(path);
                    break;
                }
            }
        }

        // 2. .env.local (개발 환경용)
        targetPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));

        // 모든 대상 경로에 토큰 저장
        foreach (var envFilePath in targetPaths)
        {
            try
            {
                // 디렉토리 생성
                var dirPath = Path.GetDirectoryName(envFilePath);
                if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // 파일에서 기존 PERSONAL_365_REFRESH_TOKEN 행 제거
                if (File.Exists(envFilePath))
                {
                    var lines = File.ReadAllLines(envFilePath)
                        .Where(line => !line.StartsWith("PERSONAL_365_REFRESH_TOKEN="))
                        .ToList();

                    File.WriteAllLines(envFilePath, lines);
                }

                // 새 토큰 추가
                File.AppendAllText(envFilePath, $"PERSONAL_365_REFRESH_TOKEN={refreshToken}\n");

                Console.WriteLine($"✓ Token saved to: {envFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Failed to save token to {envFilePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Refresh token을 .azure/{env}/.env에 복사
    /// </summary>
    private static void CopyTokenToAzureEnv(string refreshToken)
    {
        var envName = Environment.GetEnvironmentVariable("AZURE_ENV_NAME");
        if (string.IsNullOrEmpty(envName))
        {
            Console.WriteLine("[SKIP] AZURE_ENV_NAME not set - skipping copy to .azure env");
            return;
        }

        // 여러 경로에서 찾기: 현재 경로, 상위 경로들
        string[] possibleRoots = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".."),
        };

        Console.WriteLine($"[DEBUG] Looking for .azure env file with AZURE_ENV_NAME={envName}");
        string? azureEnvPath = null;
        foreach (var root in possibleRoots)
        {
            var path = Path.Combine(root, ".azure", envName, ".env");
            Console.WriteLine($"[DEBUG] Checking: {Path.GetFullPath(path)}");
            if (File.Exists(path))
            {
                azureEnvPath = path;
                Console.WriteLine($"[DEBUG] Found at: {azureEnvPath}");
                break;
            }
        }

        if (azureEnvPath == null)
        {
            Console.WriteLine($"[ERROR] Azure env file not found in any possible location for env: {envName}");
            return;
        }

        try
        {
            // 기존 PERSONAL_365_REFRESH_TOKEN 행 제거
            var lines = File.ReadAllLines(azureEnvPath)
                .Where(line => !line.StartsWith("PERSONAL_365_REFRESH_TOKEN="))
                .ToList();

            File.WriteAllLines(azureEnvPath, lines);

            // 새 토큰 추가
            File.AppendAllText(azureEnvPath, $"PERSONAL_365_REFRESH_TOKEN={refreshToken}\n");

            Console.WriteLine($"✓ Token copied to: {azureEnvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to copy token to Azure env: {ex.Message}");
        }
    }
}
