using Azure.Core;
using Azure.Identity;

using McpSamples.OutlookEmail.HybridApp.Configurations;
using McpSamples.OutlookEmail.HybridApp.Services;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

using Microsoft.Graph;

using Constants = McpSamples.OutlookEmail.HybridApp.Constants;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OutlookEmailAppSettings>(builder.Configuration, args);
builder.Services.AddScoped<IOutlookEmailService, OutlookEmailService>();
builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var settings = sp.GetRequiredService<OutlookEmailAppSettings>();
    var entraId = settings.EntraId;

    TokenCredential credential = entraId.UseManagedIdentity
                                   ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(entraId.UserAssignedClientId))
                                   : new ClientSecretCredential(entraId.TenantId, entraId.ClientId, entraId.ClientSecret);

    string[] scopes = [ Constants.DefaultScope ];
    var client = new GraphServiceClient(credential, scopes);

    return client;
});

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
