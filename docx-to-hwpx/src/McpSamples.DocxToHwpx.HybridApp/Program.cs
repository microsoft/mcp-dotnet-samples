using CSnakes.Runtime;

using McpSamples.DocxToHwpx.HybridApp.Configurations;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<DocxToHwpxAppSettings>(builder.Configuration, args);

var home = Path.Join(AppContext.BaseDirectory, ".");
var venvPath = Path.Join(home, ".venv");

builder.Services
    .WithPython()
    .WithHome(home)
    .FromRedistributable()
    .WithVirtualEnvironment(venvPath)
    .WithUvInstaller("requirements.txt");

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
