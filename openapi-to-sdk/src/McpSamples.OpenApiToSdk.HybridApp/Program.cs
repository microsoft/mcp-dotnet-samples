using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.OpenApiToSdk.HybridApp.Prompts;
using McpSamples.OpenApiToSdk.HybridApp.Services;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<OpenApiToSdkAppSettings>(builder.Configuration, args);
builder.Services.AddHttpContextAccessor();

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true
};
builder.Services.AddSingleton(options);

builder.Services.AddSingleton<IOpenApiService, OpenApiService>();
builder.Services.AddSingleton<ISdkGenerationPrompt, SdkGenerationPrompt>();

IHost app = builder.BuildApp(useStreamableHttp);

var appSettings = app.Services.GetRequiredService<OpenApiToSdkAppSettings>();
InitializeRuntimeSettings(appSettings, useStreamableHttp);

if (useStreamableHttp)
{
    var webApp = (app as WebApplication)!;

    if (!Directory.Exists(appSettings.GeneratedPath))
    {
        Directory.CreateDirectory(appSettings.GeneratedPath);
    }

    webApp.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(appSettings.GeneratedPath),
        RequestPath = "/download",
        ServeUnknownFileTypes = true
    });

    webApp.MapPost("/upload", async (IFormFile file, OpenApiToSdkAppSettings settings) =>
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file uploaded.");

        if (!Directory.Exists(settings.SpecsPath))
            Directory.CreateDirectory(settings.SpecsPath);

        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(settings.SpecsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Results.Ok(new { Message = "File uploaded successfully.", SavedPath = filePath });
    })
    .DisableAntiforgery();
}

await app.RunAsync();

void InitializeRuntimeSettings(OpenApiToSdkAppSettings settings, bool isHttp)
{
    string baseDirectory;

    if (settings.Runtime.Mode.Equals("Local", StringComparison.OrdinalIgnoreCase))
    {
        baseDirectory = TryFindProjectRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();

        Console.WriteLine($"[Init] Local Base Directory resolved to: {baseDirectory}");
    }
    else
    {
        baseDirectory = Directory.GetCurrentDirectory();
    }

    string workspacePath = Path.Combine(baseDirectory, "workspace");

    settings.WorkspacePath = workspacePath;
    settings.GeneratedPath = Path.Combine(workspacePath, "generated");
    settings.SpecsPath = Path.Combine(workspacePath, "specs");
    settings.IsHttpMode = isHttp;

    if (!Directory.Exists(settings.WorkspacePath)) Directory.CreateDirectory(settings.WorkspacePath);
    if (!Directory.Exists(settings.SpecsPath)) Directory.CreateDirectory(settings.SpecsPath);
    if (!Directory.Exists(settings.GeneratedPath)) Directory.CreateDirectory(settings.GeneratedPath);
}

string? TryFindProjectRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir != null)
    {
        if (dir.Name.Equals("openapi-to-sdk", StringComparison.OrdinalIgnoreCase))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return null;
}