using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Personal 365 Refresh Token 프로비저닝 도구
/// 사용자를 Microsoft 로그인으로 리다이렉트하고 리프레시 토큰을 .env 파일에 저장합니다.
/// 동적 포트 할당을 사용하여 포트 충돌을 피합니다.
/// </summary>
class ProvisionRefreshToken
{
    private const string ClientId = "44609b96-b8ed-48cd-ae81-75abbd52ffd1";
    private const string TenantId = "consumers"; // 개인 계정 필수
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string AuthEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";

    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Personal 365 Refresh Token Provisioning");
        Console.WriteLine("========================================\n");

        try
        {
            // Step 1: .env 파일 경로 확인
            string envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
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

            // Step 2: 사용 가능한 포트 찾기
            Console.WriteLine("\nStep 2: Finding available port...");
            int port = GetAvailablePort();
            string redirectUri = $"http://localhost:{port}/";
            Console.WriteLine($"✓ Using port: {port}\n");

            // Step 3: 로컬 HTTP 서버 시작
            Console.WriteLine("Step 3: Starting local HTTP server for authentication callback...");
            var server = new HttpListener();
            server.Prefixes.Add($"http://localhost:{port}/");
            server.Start();
            Console.WriteLine($"✓ Server started on http://localhost:{port}/\n");

            // Step 4: 인증 URL 생성 및 브라우저 열기
            Console.WriteLine("Step 4: Opening browser for authentication...");
            string authUrl = GenerateAuthUrl(redirectUri);
            Console.WriteLine($"Auth URL: {authUrl}\n");

            // 브라우저 자동 열기 (Windows의 경우)
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                Console.WriteLine("브라우저를 자동으로 열 수 없습니다.");
                Console.WriteLine($"다음 URL을 브라우저에서 열어주세요:\n{authUrl}\n");
            }

            Console.WriteLine("대기 중... (브라우저에서 로그인해주세요)\n");

            // Step 5: 인증 콜백 대기
            var context = server.GetContext();
            var request = context.Request;
            var response = context.Response;

            // 쿼리 스트링에서 'code' 파라미터 추출
            string? code = request.QueryString["code"];

            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("✗ Authorization code를 받지 못했습니다.");
                response.StatusCode = 400;
                WriteResponse(response, "Error: No authorization code received");
                server.Stop();
                return;
            }

            Console.WriteLine($"✓ Authorization code received: {code.Substring(0, 20)}...\n");

            // Step 6: Code를 Refresh Token으로 교환
            Console.WriteLine("Step 5: Exchanging authorization code for refresh token...");
            string? refreshToken = await ExchangeCodeForToken(code, redirectUri);

            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("✗ Refresh token을 얻을 수 없습니다.");
                response.StatusCode = 400;
                WriteResponse(response, "Error: Could not obtain refresh token");
                server.Stop();
                return;
            }

            Console.WriteLine("✓ Refresh token obtained successfully\n");

            // Step 7: .env 파일에 토큰 저장
            Console.WriteLine("Step 6: Saving refresh token to .env file...");
            SaveRefreshToken(envFilePath, refreshToken);
            Console.WriteLine($"✓ Token saved to {envFilePath}\n");

            // 성공 응답
            response.StatusCode = 200;
            WriteResponse(response, "Authentication successful! You can close this window.\nToken has been saved to .env file.");
            server.Stop();

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
    /// 사용 가능한 포트를 찾습니다 (OS가 자동으로 할당한 사용 가능한 포트)
    /// </summary>
    private static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var endPoint = socket.LocalEndPoint as IPEndPoint;
        return endPoint?.Port ?? 0;
    }

    /// <summary>
    /// 인증 URL 생성
    /// </summary>
    private static string GenerateAuthUrl(string redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "response_type", "code" },
            { "redirect_uri", redirectUri },
            { "scope", "https://graph.microsoft.com/.default offline_access" },
            { "response_mode", "query" }
        };

        var queryString = string.Join("&",
            parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        return $"{AuthEndpoint}?{queryString}";
    }

    /// <summary>
    /// Authorization Code를 Refresh Token으로 교환
    /// </summary>
    private static async Task<string?> ExchangeCodeForToken(string code, string redirectUri)
    {
        try
        {
            using var client = new HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "grant_type", "authorization_code" },
                { "scope", "https://graph.microsoft.com/.default offline_access" }
            };

            var content = new FormUrlEncodedContent(requestBody);

            var response = await client.PostAsync(TokenEndpoint, content);

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
    /// Refresh token을 .env 파일에 저장
    /// </summary>
    private static void SaveRefreshToken(string envFilePath, string refreshToken)
    {
        if (!Directory.Exists(Path.GetDirectoryName(envFilePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(envFilePath)!);
        }

        if (File.Exists(envFilePath))
        {
            // 기존 PERSONAL_365_REFRESH_TOKEN 행 제거
            var lines = File.ReadAllLines(envFilePath)
                .Where(line => !line.StartsWith("PERSONAL_365_REFRESH_TOKEN="))
                .ToList();

            File.WriteAllLines(envFilePath, lines);
        }

        // 새 토큰 추가
        File.AppendAllText(envFilePath, $"PERSONAL_365_REFRESH_TOKEN={refreshToken}\n");
    }

    /// <summary>
    /// HTTP 응답 작성
    /// </summary>
    private static void WriteResponse(HttpListenerResponse response, string message)
    {
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);
        response.ContentLength64 = buffer.Length;
        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        response.Close();
    }
}
