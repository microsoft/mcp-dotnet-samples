using McpSamples.PptTranslator.HybridApp.Configurations;
using McpSamples.PptTranslator.HybridApp.Services;
using McpSamples.PptTranslator.HybridApp.Tools;
using McpSamples.PptTranslator.HybridApp.Models;
using McpSamples.PptTranslator.HybridApp.Prompts;
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
builder.Services.AddSingleton<IPptTranslatorPrompt, PptTranslatorPrompt>();

IHost appHost = builder.BuildApp(useStreamableHttp);

// HTTP MODE
if (useStreamableHttp)
{
    var app = (WebApplication)appHost;

    // ---------------------------
    // 1) Upload (Azure only - uploads to /files/input)
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
            // input 폴더에 저장하여 일관성 보장
            string inputDir = Path.Combine(FilesDir, "input");
            Directory.CreateDirectory(inputDir);

            string fileName = file.FileName; // 원본 파일명 사용
            string savePath = Path.Combine(inputDir, fileName);

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
    // 4) Download by filename (Primary endpoint for all modes)
    // ---------------------------
    app.MapGet("/download/{filename}", (string filename) =>
    {
        string filePath = executionMode.IsContainerMode()
            ? Path.Combine(FilesDir, "output", filename)  // Container/Azure 모드: /files/output 폴더 사용
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "generated", filename);  // 로컬 모드
        
        return File.Exists(filePath)
            ? Results.File(filePath,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                filename)
            : Results.NotFound();
    });



    await app.RunAsync();
}
else
{
    await appHost.RunAsync();
}
