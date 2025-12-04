using McpSamples.PptTranslator.HybridApp.Configurations;
using McpSamples.PptTranslator.HybridApp.Services;
using McpSamples.PptTranslator.HybridApp.Tools;
using McpSamples.PptTranslator.HybridApp.Models;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// MCP_HTTP_MODE 환경변수 설정 (ExecutionMode 감지용)
if (useStreamableHttp)
{
    Environment.SetEnvironmentVariable("MCP_HTTP_MODE", "true");
}

var executionMode = ExecutionModeDetector.DetectExecutionMode();

bool isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_APP_NAME")) 
               || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));

const string FilesDir = "/files";  // 단일 마운트 폴더

IHostApplicationBuilder builder = useStreamableHttp
    ? WebApplication.CreateBuilder(args)
    : Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddAppSettings<PptTranslatorAppSettings>(builder.Configuration, args);
builder.Services.AddLogging();
builder.Services.AddHttpContextAccessor();

// Services
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<ITextExtractService, TextExtractService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IFileRebuildService, FileRebuildService>();
builder.Services.AddScoped<IPptTranslateTool, PptTranslateTool>();

IHost appHost = builder.BuildApp(useStreamableHttp);

// HTTP MODE
if (useStreamableHttp)
{
    var app = (WebApplication)appHost;

    // ---------------------------
    // 1) Upload (Azure only - uploads to /files)
    // ---------------------------
    app.MapPost("/upload", async (HttpRequest req) =>
    {
        if (!req.HasFormContentType)
            return Results.BadRequest("multipart/form-data required");

        var file = (await req.ReadFormAsync()).Files["file"];
        if (file == null)
            return Results.BadRequest("file required");

        if (isAzure)
        {
            Directory.CreateDirectory(FilesDir);

            string fileName = file.FileName; // 원본 파일명 사용
            string savePath = Path.Combine(FilesDir, fileName);

            using var fs = File.Create(savePath);
            await file.CopyToAsync(fs);

            return Results.Ok(new { id = fileName, path = fileName });
        }
        else
        {
            return Results.BadRequest("Upload endpoint is only available in Azure mode. For local mode, provide absolute file path directly.");
        }
    });

    // ---------------------------
    // 2) Input File Access (deprecated - kept for backward compatibility)
    // ---------------------------
    app.MapGet("/input/{id}", (string id) =>
    {
        if (isAzure)
        {
            string path = Path.Combine(FilesDir, id);
            return File.Exists(path)
                ? Results.File(path)
                : Results.NotFound();
        }
        else
        {
            return Results.BadRequest("Input endpoint is only available in Azure mode.");
        }
    });

    // ---------------------------
    // 3) Output File Download (deprecated - use /download/{filename} instead)
    // ---------------------------
    app.MapGet("/output/{id}/{lang}", (string id, string lang) =>
    {
        string fileName = $"{id}_translated_{lang}.pptx";

        if (isAzure)
        {
            string path = Path.Combine(FilesDir, fileName);
            return File.Exists(path)
                ? Results.File(path,
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    fileName)
                : Results.NotFound();
        }
        else
        {
            string? path = Directory
                .GetFiles(Path.GetTempPath())
                .FirstOrDefault(f => Path.GetFileName(f).Contains(fileName));

            return path != null
                ? Results.File(path,
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    fileName)
                : Results.NotFound();
        }
    });

    // ---------------------------
    // 4) Download by filename (Primary endpoint for all modes)
    // ---------------------------
    app.MapGet("/download/{filename}", (string filename) =>
    {
        string filePath;
        
        switch (executionMode)
        {
            case ExecutionMode.HttpContainer:
            case ExecutionMode.HttpRemote:
                // Container/Azure 모드: /files에서 찾기
                filePath = Path.Combine(FilesDir, filename);
                break;
                
            case ExecutionMode.HttpLocal:
            default:
                // 로컬 모드: wwwroot/generated에서 찾기
                filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "generated", filename);
                break;
        }
        
        return File.Exists(filePath)
            ? Results.File(filePath,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                filename)
            : Results.NotFound();
    });

    // ---------------------------
    // 5) Direct download endpoint (legacy - deprecated)
    // ---------------------------
    app.MapGet("/download", (HttpRequest req) =>
    {
        string? id = req.Query["id"];
        if (string.IsNullOrEmpty(id))
            return Results.BadRequest("Missing id");

        if (isAzure)
        {
            string path = Path.Combine(FilesDir, id);
            return File.Exists(path)
                ? Results.File(path,
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    id)
                : Results.NotFound();
        }
        else
        {
            string filePath = Path.Combine(Path.GetTempPath(), id);
            return File.Exists(filePath)
                ? Results.File(filePath)
                : Results.NotFound();
        }
    });

    await app.RunAsync();
}
else
{
    await appHost.RunAsync();
}
