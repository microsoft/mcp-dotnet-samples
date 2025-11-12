using Azure.Core;
using Azure.Identity;

using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

using Microsoft.Graph;

var envs = Environment.GetEnvironmentVariables();
var useStreamableHttp = AppSettings.UseStreamableHttp(envs, args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

if (useStreamableHttp == true)
{
    var port = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT") ?? "7071";
    (builder as WebApplicationBuilder)!.WebHost.UseUrls($"http://localhost:{port}");

    Console.WriteLine($"Listening on port {port}");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddAppSettings<OnedriveDownloadAppSettings>(builder.Configuration, args);

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var settings = sp.GetRequiredService<OnedriveDownloadAppSettings>();
    var entraId = settings.EntraId;

    TokenCredential credential;
    if (entraId.UseManagedIdentity)
    {
        credential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(entraId.UserAssignedClientId));
    }
    else
    {
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        if (httpContextAccessor.HttpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available. This service is intended to be called via HTTP.");
        }

        var authHeader = httpContextAccessor.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            throw new InvalidOperationException("Authorization header with Bearer token is required.");
        }
        var userToken = authHeader.Substring("Bearer ".Length);

        var oboOptions = new OnBehalfOfCredentialOptions
        {
            SendCertificateChain = true, // Recommended for service-to-service calls
        };

        credential = new OnBehalfOfCredential(
            tenantId: entraId.TenantId,
            clientId: entraId.ClientId,
            clientSecret: entraId.ClientSecret,
            userAssertion: userToken,
            options: oboOptions
        );
    }

    string[] scopes = { "https://graph.microsoft.com/.default" };
    var client = new GraphServiceClient(credential, scopes);

    return client;
});

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();