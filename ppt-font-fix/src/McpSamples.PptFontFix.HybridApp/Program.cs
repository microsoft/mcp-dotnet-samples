using McpSamples.PptFontFix.HybridApp.Configurations;
using McpSamples.PptFontFix.HybridApp.Services;
using McpSamples.PptFontFix.HybridApp.Tools;
using McpSamples.PptFontFix.HybridApp.Prompts;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<PptFontFixAppSettings>(builder.Configuration, args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPptFontFixService, PptFontFixService>();

builder.Services.AddTransient<IPptFontFixTool, PptFontFixTool>();
builder.Services.AddSingleton<IPptFontFixPrompt, PptFontFixPrompt>();

IHost app = builder.BuildApp(useStreamableHttp);

var appSettings = app.Services.GetRequiredService<PptFontFixAppSettings>();
InitializeRuntimeSettings(appSettings, useStreamableHttp);

if (useStreamableHttp && app is WebApplication webApp)
{
    string actualGeneratedPath = Path.Combine(webApp.Environment.WebRootPath, "generated");
    if (!Directory.Exists(actualGeneratedPath))
    {
        Directory.CreateDirectory(actualGeneratedPath);
    }

    webApp.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(actualGeneratedPath), 
        RequestPath = "/generated",
        ServeUnknownFileTypes = true
    });
    webApp.MapPost("/upload", async (IFormFile file, IPptFontFixService service) =>
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file uploaded." });
        }

        var appSettings = webApp.Services.GetRequiredService<PptFontFixAppSettings>(); // AppSettings 주입
        if (!Directory.Exists(appSettings.InputPath))
            Directory.CreateDirectory(appSettings.InputPath);

        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(appSettings.InputPath, fileName);

        try
        {
            using (var stream = file.OpenReadStream())
            using (var outputStream = File.Create(filePath))
            {
                await stream.CopyToAsync(outputStream);
            }

            return Results.Ok(new { message = "File uploaded successfully.", filePath = filePath });
        }
        catch (Exception ex)
        {
            return Results.Problem($"File upload failed: {ex.Message}");
        }
    })
    .DisableAntiforgery();
}

await app.RunAsync();

void InitializeRuntimeSettings(PptFontFixAppSettings settings, bool isHttp)
{
    bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    string? azureAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
    bool isAzure = !string.IsNullOrEmpty(azureAppName);

    string baseDirectory;

    if (isAzure)
    {
        baseDirectory = "/workspace";
        settings.InputPath = Path.Combine(baseDirectory);
        settings.GeneratedPath = Path.Combine(baseDirectory,"generated");
        settings.WorkspacePath = Path.GetDirectoryName(settings.InputPath) ?? baseDirectory;
    }
    else if (isContainer)
    {
        baseDirectory = "/files"; 
        settings.InputPath = Path.Combine(baseDirectory, "input");
        settings.GeneratedPath = Path.Combine(baseDirectory, "generated");
        
        settings.WorkspacePath = Path.GetDirectoryName(settings.InputPath) ?? baseDirectory;
    }
    else
    {
        baseDirectory = TryFindProjectRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
        string workspacePath = Path.Combine(baseDirectory, "workspace");
        settings.WorkspacePath = workspacePath;
        settings.InputPath = Path.Combine(workspacePath, "input");
        settings.GeneratedPath = Path.Combine(workspacePath, "generated");
    }
    
    settings.IsHttpMode = isHttp;
    settings.IsContainer = isContainer;
    settings.IsAzure = isAzure;

    if (!Directory.Exists(settings.WorkspacePath)) Directory.CreateDirectory(settings.WorkspacePath);
    if (!Directory.Exists(settings.InputPath)) Directory.CreateDirectory(settings.InputPath);
    if (!Directory.Exists(settings.GeneratedPath)) Directory.CreateDirectory(settings.GeneratedPath);
}

string? TryFindProjectRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir != null)
    {
        if (dir.GetFiles("Dockerfile.ppt-font-fix").Length > 0)
        {
            return Path.Combine(dir.FullName, "ppt-font-fix");
        }
        dir = dir.Parent;
    }
    return null; 
}
