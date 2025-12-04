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
    // A. 정적 파일 서빙 (다운로드용)
    // GeneratedPath(수정된 파일 저장소)가 없으면 생성
    string actualGeneratedPath = Path.Combine(webApp.Environment.WebRootPath, "generated");
    if (!Directory.Exists(actualGeneratedPath))
    {
        Directory.CreateDirectory(actualGeneratedPath);
    }

    // '/generated' 경로로 들어오는 요청을 WebRootPath의 generated 폴더에 매핑
    webApp.UseStaticFiles(new StaticFileOptions
    {
        // 🚨 수정: appSettings.GeneratedPath 대신 실제 wwwroot 경로를 사용
        FileProvider = new PhysicalFileProvider(actualGeneratedPath), 
        RequestPath = "/generated",
        ServeUnknownFileTypes = true
    });

    // B. 파일 업로드 엔드포인트 추가 (/upload)
    webApp.MapPost("/upload", async (IFormFile file, IPptFontFixService service) =>
    {
        // 1. 입력 유효성 검사 (오류 시 반환)
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file uploaded." });
        }

        // 2. 입력 파일 저장소 (SpecsPath) 확인 및 생성
        var appSettings = webApp.Services.GetRequiredService<PptFontFixAppSettings>(); // AppSettings 주입
        if (!Directory.Exists(appSettings.SpecsPath))
            Directory.CreateDirectory(appSettings.SpecsPath);

        // 3. 파일명 추출 및 최종 경로 설정
        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(appSettings.SpecsPath, fileName);

        try
        {
            // 4. 파일 스트림을 디스크에 직접 저장
            using (var stream = file.OpenReadStream())
            using (var outputStream = File.Create(filePath))
            {
                await stream.CopyToAsync(outputStream);
            }

            // 5. [수정] 업로드 성공 후, LLM이 참조할 수 있는 최종 파일 경로 반환
            //    (Tool이 이 경로를 받아 AnalyzePptFileAsync를 호출함)
            return Results.Ok(new { message = "File uploaded successfully.", filePath = filePath });
        }
        catch (Exception ex)
        {
            // 6. 예외 발생 시 반환
            return Results.Problem($"File upload failed: {ex.Message}");
        }
    })
    .DisableAntiforgery();
}

await app.RunAsync();

void InitializeRuntimeSettings(PptFontFixAppSettings settings, bool isHttp)
{
    // Docker/Azure 환경 변수 확인
    bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    string? azureAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
    bool isAzure = !string.IsNullOrEmpty(azureAppName);

    string baseDirectory;

    if (isContainer)
    {
        baseDirectory = "/app"; 
        settings.SpecsPath = Path.Combine(baseDirectory, "files");
        settings.GeneratedPath = Path.Combine(baseDirectory, "workspace", "generated");
        
        // WorkspacePath는 SpecsPath의 상위 개념으로 설정
        settings.WorkspacePath = Path.GetDirectoryName(settings.SpecsPath) ?? baseDirectory;
    }
    
    else
    {
        // [로컬 환경]: 프로젝트 루트 폴더 찾기
        baseDirectory = TryFindProjectRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
    }

    // 작업 공간 경로 설정
    string workspacePath = Path.Combine(baseDirectory, "workspace");
    
    // 설정 객체에 값 주입
    settings.WorkspacePath = workspacePath;
    settings.SpecsPath = Path.Combine(workspacePath, "inputs"); // 입력 파일 저장소
    settings.GeneratedPath = Path.Combine(workspacePath, "generated"); // 출력 파일 저장소
    settings.IsHttpMode = isHttp;
    settings.IsContainer = isContainer;
    settings.IsAzure = isAzure;

    // 디렉토리 생성 보장
    if (!Directory.Exists(settings.WorkspacePath)) Directory.CreateDirectory(settings.WorkspacePath);
    if (!Directory.Exists(settings.SpecsPath)) Directory.CreateDirectory(settings.SpecsPath);
    if (!Directory.Exists(settings.GeneratedPath)) Directory.CreateDirectory(settings.GeneratedPath);
}

// 프로젝트 루트 탐색 헬퍼 메서드
string? TryFindProjectRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir != null)
    {
        // Dockerfile.ppt-font-fix 파일이 있는 곳을 루트로 간주
        if (dir.GetFiles("Dockerfile.ppt-font-fix").Length > 0)
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return null; 
}
