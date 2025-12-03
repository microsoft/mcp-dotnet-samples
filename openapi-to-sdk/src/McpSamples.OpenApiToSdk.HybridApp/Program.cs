using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using McpSamples.OpenApiToSdk.HybridApp.Configurations;
using McpSamples.OpenApiToSdk.HybridApp.Prompts;
using McpSamples.OpenApiToSdk.HybridApp.Services;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;

// 1. 실행 모드 감지 (Shared 기능 사용)
var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// 2. 호스트 빌더 생성
IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

// 3. 설정(AppSettings) 등록 (Shared 기능 사용)
// appsettings.json 로드 및 객체 바인딩 처리
builder.Services.AddAppSettings<OpenApiToSdkAppSettings>(builder.Configuration, args);

// [추가] HttpContext에 접근하기 위해 등록 (HTTP 모드일 때만 필수지만, 안전하게 항상 등록해도 무방함)
builder.Services.AddHttpContextAccessor();

// 4. 서비스 등록
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true
};
builder.Services.AddSingleton(options);

// 핵심 비즈니스 로직 서비스 및 프롬프트 서비스 등록
builder.Services.AddSingleton<IOpenApiService, OpenApiService>();
builder.Services.AddSingleton<ISdkGenerationPrompt, SdkGenerationPrompt>();

// 5. 앱 빌드 (Shared 기능 사용)
// 이 단계에서 [McpServerToolType], [McpServerPromptType] 어트리뷰트가 있는 클래스를 자동으로 스캔하여 등록합니다.
IHost app = builder.BuildApp(useStreamableHttp);

// --------------------------------------------------------------------------
// [Runtime Configuration] 실행 환경(Local/Docker/Azure)에 따른 경로 설정
// --------------------------------------------------------------------------
var appSettings = app.Services.GetRequiredService<OpenApiToSdkAppSettings>();
InitializeRuntimeSettings(appSettings, useStreamableHttp);

// --------------------------------------------------------------------------
// [HTTP Mode Only] 다운로드를 위한 정적 파일 서빙 설정
// --------------------------------------------------------------------------
if (useStreamableHttp)
{
    var webApp = (app as WebApplication)!;

    // 저장 경로가 없으면 생성
    if (!Directory.Exists(appSettings.GeneratedPath))
    {
        Directory.CreateDirectory(appSettings.GeneratedPath);
    }

    // '/download' 경로를 물리적 폴더에 매핑
    webApp.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(appSettings.GeneratedPath),
        RequestPath = "/download",
        ServeUnknownFileTypes = true
    });

}

// 6. 앱 실행
await app.RunAsync();


// --------------------------------------------------------------------------
// Helper: 런타임 환경 변수 감지 및 경로 주입
// --------------------------------------------------------------------------
void InitializeRuntimeSettings(OpenApiToSdkAppSettings settings, bool isHttp)
{
    // Docker/Azure 환경 변수 확인
    bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    string? azureAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
    bool isAzure = !string.IsNullOrEmpty(azureAppName);

    string baseDirectory;

    if (isContainer)
    {
        // 컨테이너 환경: 항상 /app 사용
        baseDirectory = "/app";
    }
    else
    {
        // 로컬 환경: 'openapi-to-sdk' 루트 폴더 찾기
        // 1. 현재 실행 위치(CurrentDirectory)에서 시작
        // 2. 상위로 이동하며 'Dockerfile.openapi-to-sdk' 파일이 있는 곳을 찾음
        baseDirectory = TryFindProjectRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
        baseDirectory = Path.Combine(baseDirectory, "openapi-to-sdk");

        // (디버깅용 로그: 실행 시 콘솔에 출력됨)
        Console.WriteLine($"[Init] Local Base Directory resolved to: {baseDirectory}");
    }

    string workspacePath = Path.Combine(baseDirectory, "workspace");

    // 설정 객체에 값 주입
    settings.WorkspacePath = workspacePath;
    settings.GeneratedPath = Path.Combine(workspacePath, "generated");
    settings.SpecsPath = Path.Combine(workspacePath, "specs");
    settings.IsHttpMode = isHttp;
    settings.IsContainer = isContainer;
    settings.IsAzure = isAzure;

    // 필수 폴더 생성
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
        // 이 파일이 있는 곳을 루트(openapi-to-sdk 폴더)로 간주
        if (dir.GetFiles("Dockerfile.openapi-to-sdk").Length > 0)
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return null; // 못 찾으면 null 반환 (현재 위치 사용)
}