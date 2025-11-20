using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;

namespace McpSamples.OnedriveDownload.HybridApp.Services;

/// <summary>
/// Service for handling user authentication using Azure DefaultCredential and OAuth.
/// Supports both Azure credentials (azd auth login) and Personal Microsoft account authentication.
/// </summary>
public interface IUserAuthenticationService
{
    /// <summary>
    /// Gets the current user's access token from Azure credentials.
    /// </summary>
    Task<string?> GetCurrentUserAccessTokenAsync();

    /// <summary>
    /// Gets the Personal OneDrive access token using OAuth (MSAL).
    /// </summary>
    Task<string?> GetPersonalOneDriveAccessTokenAsync();

    /// <summary>
    /// Gets GraphServiceClient using Azure credentials.
    /// </summary>
    Task<GraphServiceClient> GetUserGraphClientAsync();

    /// <summary>
    /// Gets GraphServiceClient using Personal OneDrive token.
    /// </summary>
    Task<GraphServiceClient> GetPersonalOneDriveGraphClientAsync();
}

/// <summary>
/// Implementation of user authentication service using Azure DefaultCredential and MSAL.
/// Automatically retrieves tokens from azd auth login or other Azure credential sources.
/// Also supports Personal OneDrive access via OAuth.
/// </summary>
public class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ILogger<UserAuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };
    private readonly string[] _oneDriveScopes = { "https://graph.microsoft.com/Files.ReadWrite.All" };

    public UserAuthenticationService(
        ILogger<UserAuthenticationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string?> GetCurrentUserAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("=== GetCurrentUserAccessTokenAsync called ===");
            _logger.LogInformation("Attempting to get token from Azure DefaultCredential");

            // Use DefaultAzureCredential which includes:
            // - azd auth login token
            // - Environment variables
            // - Visual Studio credentials
            // - Azure PowerShell credentials
            // - Azure CLI credentials
            var credential = new DefaultAzureCredential();

            var token = await credential.GetTokenAsync(
                new TokenRequestContext(_scopes));

            _logger.LogInformation("Access token acquired from Azure DefaultCredential");
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token from Azure DefaultCredential: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    public async Task<string?> GetPersonalOneDriveAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("=== GetPersonalOneDriveAccessTokenAsync called ===");

            // Step 1: Azure CLI에서 홈 테넌트 ID 획득
            string? homeTenantId = await GetHomeTenantIdFromAzureCliAsync();

            if (string.IsNullOrEmpty(homeTenantId))
            {
                _logger.LogWarning("Could not determine home tenant ID");
                return null;
            }

            _logger.LogInformation("Found home tenant ID: {HomeTenantId}", homeTenantId);

            // Step 2: DefaultAzureCredential에 homeTenantId 지정하여 Personal 365 토큰 획득
            _logger.LogInformation("Acquiring token for home tenant {HomeTenantId}", homeTenantId);

            var credentialOptions = new DefaultAzureCredentialOptions
            {
                TenantId = homeTenantId,
                VisualStudioTenantId = homeTenantId,
                SharedTokenCacheTenantId = homeTenantId
            };

            var m365Credential = new DefaultAzureCredential(credentialOptions);

            var token = await m365Credential.GetTokenAsync(
                new TokenRequestContext(_oneDriveScopes));

            _logger.LogInformation("✓ Personal OneDrive token acquired successfully");
            return token.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Personal OneDrive access token: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Azure CLI에서 홈 테넌트 ID 획득
    /// </summary>
    private async Task<string?> GetHomeTenantIdFromAzureCliAsync()
    {
        try
        {
            _logger.LogInformation("Getting home tenant ID from Azure CLI...");

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "az",
                    Arguments = "account show --output json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Azure CLI failed with exit code {ExitCode}. Error: {Error}",
                    process.ExitCode, errorOutput);
                return null;
            }

            _logger.LogInformation("Azure CLI output: {Output}", output);

            // JSON 파싱
            using var doc = System.Text.Json.JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.TryGetProperty("homeTenantId", out var homeTenantIdElement))
            {
                var homeTenantId = homeTenantIdElement.GetString();
                _logger.LogInformation("✓ Home tenant ID extracted: {HomeTenantId}", homeTenantId);
                return homeTenantId;
            }

            if (root.TryGetProperty("tenantId", out var tenantIdElement))
            {
                var tenantId = tenantIdElement.GetString();
                _logger.LogWarning("homeTenantId not found, using tenantId: {TenantId}", tenantId);
                return tenantId;
            }

            _logger.LogWarning("Could not find tenant ID in Azure CLI output");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting home tenant ID from Azure CLI: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 사용자가 속한 테넌트 중 Microsoft 365가 있는 테넌트 찾기
    /// </summary>
    private async Task<string?> FindM365TenantAsync(string accessToken, string userOid)
    {
        try
        {
            _logger.LogInformation("=== FindM365TenantAsync called ===");
            _logger.LogInformation("Querying user's tenants...");

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            // Step 1: 사용자 정보 조회
            _logger.LogInformation("Step 1: Getting user info from /me");
            var userResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/me");
            _logger.LogInformation("User info response status: {StatusCode}", userResponse.StatusCode);

            if (!userResponse.IsSuccessStatusCode)
            {
                var errorContent = await userResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get user info. Status: {StatusCode}, Error: {Error}",
                    userResponse.StatusCode, errorContent);
                return null;
            }

            var userContent = await userResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("User info retrieved: {UserInfo}", userContent);

            // Step 2: OneDrive 직접 확인
            _logger.LogInformation("Step 2: Checking for OneDrive at /me/drive");
            var driveResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/me/drive");
            _logger.LogInformation("Drive response status: {StatusCode}", driveResponse.StatusCode);

            if (driveResponse.IsSuccessStatusCode)
            {
                var driveContent = await driveResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("OneDrive found! Response: {DriveInfo}", driveContent);

                // OneDrive가 있으면 현재 테넌트가 M365 테넌트
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(accessToken) as JwtSecurityToken;
                var currentTenantId = jwtToken?.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

                if (!string.IsNullOrEmpty(currentTenantId))
                {
                    _logger.LogInformation("✓ M365 tenant found: {TenantId}", currentTenantId);
                    return currentTenantId;
                }
            }
            else
            {
                var errorContent = await driveResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("No OneDrive found at /me/drive. Status: {StatusCode}, Error: {Error}",
                    driveResponse.StatusCode, errorContent);
            }

            // Step 3: memberOf 조회 (백업 방법)
            _logger.LogInformation("Step 3: Checking memberOf as fallback");
            var memberOfResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/me/memberOf?$select=id");
            _logger.LogInformation("MemberOf response status: {StatusCode}", memberOfResponse.StatusCode);

            if (!memberOfResponse.IsSuccessStatusCode)
            {
                var errorContent = await memberOfResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get user's memberOf. Status: {StatusCode}, Error: {Error}",
                    memberOfResponse.StatusCode, errorContent);
                return null;
            }

            var memberOfContent = await memberOfResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("User's memberOf: {MemberOf}", memberOfContent);

            _logger.LogWarning("No M365 tenant found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== Error finding M365 tenant: {ErrorMessage} ===", ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return null;
        }
    }

    public async Task<GraphServiceClient> GetUserGraphClientAsync()
    {
        try
        {
            _logger.LogInformation("=== GetUserGraphClientAsync called ===");

            var accessToken = await GetCurrentUserAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token available from Azure credentials");
                throw new InvalidOperationException("No access token found. Please ensure you are authenticated via 'azd auth login'.");
            }

            _logger.LogInformation("Access token acquired successfully");

            // Create GraphServiceClient with user's token
            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            _logger.LogInformation("GraphServiceClient created for authenticated user");
            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GraphServiceClient: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    public async Task<GraphServiceClient> GetPersonalOneDriveGraphClientAsync()
    {
        try
        {
            _logger.LogInformation("=== GetPersonalOneDriveGraphClientAsync called ===");

            var accessToken = await GetPersonalOneDriveAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token available for Personal OneDrive");
                throw new InvalidOperationException("Failed to acquire Personal OneDrive access token");
            }

            _logger.LogInformation("Personal OneDrive token acquired successfully");

            // Create GraphServiceClient with Personal OneDrive token
            var authProvider = new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.CompletedTask;
            });

            var graphClient = new GraphServiceClient(authProvider);
            _logger.LogInformation("GraphServiceClient created for Personal OneDrive");
            return graphClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Personal OneDrive GraphServiceClient: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
