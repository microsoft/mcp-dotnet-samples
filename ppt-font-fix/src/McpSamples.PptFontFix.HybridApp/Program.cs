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

if (app is WebApplication webApp)
{
    // Ensure wwwroot exists for static file hosting (e.g., generated files)
    var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    if (!Directory.Exists(wwwRootPath))
    {
        Directory.CreateDirectory(wwwRootPath);
    }

    webApp.UseStaticFiles();

    // 파일 업로드 엔드포인트
    // 로컬/리모트 어디에서든 사용자가 PPTX 파일을 업로드하면 tempId를 반환하고,
    // 이후 MCP 툴에서는 "temp:{tempId}" 형태로 해당 파일을 참조할 수 있습니다.
    var fileShareMountPath = webApp.Configuration["AZURE_FILE_SHARE_MOUNT_PATH"] ?? "/app/mounts";
    
    // PptFontFixService에서 파일이 실제로 저장되는 물리적 경로: [마운트 경로]/generated
    string physicalPathToGeneratedFiles = Path.Combine(fileShareMountPath, "generated");

    // 저장 경로가 물리적으로 존재하는지 확인하고 없으면 생성
    if (!Directory.Exists(physicalPathToGeneratedFiles))
    {
        webApp.Logger.LogInformation("Creating physical directory for generated files: {Path}", physicalPathToGeneratedFiles);
        Directory.CreateDirectory(physicalPathToGeneratedFiles);
    }

    // 마운트된 볼륨 경로를 웹 경로 '/generated'에 매핑하여 파일 서빙을 활성화합니다.
    webApp.UseStaticFiles(new StaticFileOptions
    {
        // 파일이 실제 저장된 물리적 경로를 지정합니다.
        FileProvider = new PhysicalFileProvider(physicalPathToGeneratedFiles),
        
        // FQDN/generated로 들어오는 요청을 위 물리적 경로로 연결합니다.
        RequestPath = "/generated" 
    });


    // 파일 업로드 엔드포인트
    // 로컬/리모트 어디에서든 사용자가 PPTX 파일을 업로드하면 tempId를 반환하고,
    // 이후 MCP 툴에서는 "temp:{tempId}" 형태로 해당 파일을 참조할 수 있습니다.
    webApp.MapPost("/upload", async (IFormFile file, IPptFontFixService service) =>
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file uploaded." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var tempId = await service.UploadPptFileAsync(stream, file.FileName);

            // LLM(또는 클라이언트)이 이 tempId를 받아서 "temp:{tempId}" 형태로
            // analyze_ppt_file / prompt 인자에 넘기면 됩니다.
            return Results.Ok(new { tempId });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    })
    .DisableAntiforgery();
}

await app.RunAsync();