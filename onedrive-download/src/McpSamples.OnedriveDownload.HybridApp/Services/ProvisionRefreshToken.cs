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

/// <summary>
/// Personal 365 Refresh Token 프로비저닝 도구 (MSAL 기반)
/// Node.js 로직을 C#로 포팅했습니다.
/// </summary>
public class ProvisionRefreshToken
{
    private const string ClientId = "44609b96-b8ed-48cd-ae81-75abbd52ffd1";
    private const string TenantId = "consumers"; // 개인 계정 필수

    public static async Task ProvisionAsync()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Personal 365 Refresh Token Provisioning");
        Console.WriteLine("========================================\n");

        try
        {
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

            if (File.Exists(envFilePath))
            {
                string envContent = File.ReadAllText(envFilePath);
                if (envContent.Contains("PERSONAL_365_REFRESH_TOKEN="))
                {
                    Console.WriteLine("✓ 이미 Refresh Token이 존재합니다. 건너뜁니다.\n");
                    return;
                }
            }

            // Step 2: HTTP 서버 시작 및 동적 포트 할당
            Console.WriteLine("\nStep 2: Starting HTTP listener on dynamic port...");
            var listener = new HttpListener();

            // 포트 0을 사용해 동적 포트 할당
            int port = 0;
            HttpListenerContext? context = null;

            // 동적 포트를 찾기 위해 여러 번 시도
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    port = GetAvailablePort();
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Start();
                    Console.WriteLine($"✓ Listening on port: {port}\n");
                    break;
                }
                catch
                {
                    if (i == 9) throw;
                }
            }

            string redirectUri = $"http://localhost:{port}";
            Console.WriteLine($"Redirect URI: {redirectUri}\n");

            // Step 3: MSAL 공개 클라이언트 애플리케이션 생성
            Console.WriteLine("Step 3: Creating MSAL PublicClientApplication...");
            IPublicClientApplication pca = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                .Build();

            Console.WriteLine("✓ MSAL app created\n");

            // Step 4: 인증 URL 생성
            Console.WriteLine("Step 4: Opening browser for authentication...");
            var authUrl = $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize?" +
                         $"client_id={Uri.EscapeDataString(ClientId)}&" +
                         $"response_type=code&" +
                         $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                         $"response_mode=query&" +
                         $"scope={Uri.EscapeDataString("Files.Read User.Read offline_access")}";

            Console.WriteLine($"Auth URL: {authUrl}\n");

            // 브라우저 실행 로직 개선 (URL 잘림 방지)
            try
            {
                // Windows: cmd /c start 명령 사용 시 '&'를 '^&'로 이스케이프 해야 함
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var escapedUrl = authUrl.Replace("&", "^&");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start {escapedUrl}",
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
                Console.WriteLine($"아래 URL을 복사해서 직접 접속해주세요:\n{authUrl}\n");
            }

            Console.WriteLine("대기 중... (브라우저에서 로그인해주세요)\n");

            // Step 5: 인증 콜백 대기
            context = listener.GetContext();
            var queryString = context.Request.QueryString;
            var code = queryString["code"];

            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("✗ Authorization code를 받지 못했습니다.");
                SendResponse(context.Response, 400, "Error: No authorization code received");
                listener.Stop();
                return;
            }

            Console.WriteLine($"✓ Authorization code received: {code.Substring(0, 20)}...\n");

            // Step 6: 토큰 교환
            Console.WriteLine("Step 5: Exchanging authorization code for tokens...");
            string? refreshToken = await ExchangeCodeForToken(pca, code, redirectUri);

            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("✗ Refresh token을 얻을 수 없습니다.");
                SendResponse(context.Response, 400, "Error: Could not obtain refresh token");
                listener.Stop();
                return;
            }

            Console.WriteLine("✓ Refresh token obtained successfully\n");

            // Step 7: .env.local 파일에 저장
            Console.WriteLine("Step 6: Saving refresh token to .env.local...");
            SaveRefreshToken(refreshToken);
            Console.WriteLine($"✓ Token saved to .env.local\n");

            // 성공 응답
            SendResponse(context.Response, 200, "인증 성공! 터미널을 확인하고 이 창을 닫으세요.");
            listener.Stop();

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
    private static async Task<string?> ExchangeCodeForToken(IPublicClientApplication pca, string code, string redirectUri)
    {
        // 직접 REST API를 통해 token 교환
        return await GetRefreshTokenDirectly(code, redirectUri);
    }

    /// <summary>
    /// REST API를 통해 직접 Refresh Token 획득
    /// </summary>
    private static async Task<string?> GetRefreshTokenDirectly(string code, string redirectUri)
    {
        try
        {
            using var client = new HttpClient();

            // Authorization code 교환 요청
            // scope은 authorization 단계에서 이미 설정되었으므로 여기서는 필요 없음
            var requestBodyString = $"client_id={Uri.EscapeDataString(ClientId)}&" +
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
    /// Refresh token을 azd 환경 파일에 저장
    /// </summary>
    private static void SaveRefreshToken(string refreshToken)
    {
        // AZURE_ENV_NAME 환경 변수에서 현재 azd 환경 이름 가져오기
        var envName = Environment.GetEnvironmentVariable("AZURE_ENV_NAME");

        string envFilePath;
        if (!string.IsNullOrEmpty(envName))
        {
            // azd가 관리하는 .azure/{환경이름}/.env 파일에 저장
            string projectRoot = Directory.GetCurrentDirectory();
            envFilePath = Path.Combine(projectRoot, ".azure", envName, ".env");
        }
        else
        {
            // Fallback: 프로젝트 루트의 .env.local (개발 시)
            string projectRoot = Directory.GetCurrentDirectory();
            envFilePath = Path.Combine(projectRoot, ".env.local");
        }

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

    /// <summary>
    /// HTTP 응답 작성
    /// </summary>
    private static void SendResponse(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";

        var html = $"<html><body><h1>{message}</h1><script>window.close();</script></body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;

        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
    }
}
