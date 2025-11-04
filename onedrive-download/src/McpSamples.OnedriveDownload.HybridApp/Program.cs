using McpSamples.OnedriveDownload.HybridApp.Configurations;
using McpSamples.OnedriveDownload.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Identity.Web;

var envs = Environment.GetEnvironmentVariables();
var useStreamableHttp = AppSettings.UseStreamableHttp(envs, args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OnedriveDownloadAppSettings>(builder.Configuration, args);

// Entra ID and Microsoft Graph API authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraId"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
    .AddInMemoryTokenCaches();

builder.Services.AddScoped<IOneDriveTool, OneDriveTool>();

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
