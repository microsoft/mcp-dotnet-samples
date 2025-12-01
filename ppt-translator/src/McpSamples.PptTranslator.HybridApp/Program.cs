using McpSamples.PptTranslator.HybridApp.Configurations;
using McpSamples.PptTranslator.HybridApp.Services;
using McpSamples.PptTranslator.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// HTTP 모드(WebApplication) 또는 STDIO 모드(Host)
IHostApplicationBuilder builder = useStreamableHttp
    ? WebApplication.CreateBuilder(args)
    : Host.CreateApplicationBuilder(args);

// 환경 변수 로드
builder.Configuration.AddEnvironmentVariables();

// AppSettings 바인딩
builder.Services.AddAppSettings<PptTranslatorAppSettings>(builder.Configuration, args);

// Logging
builder.Services.AddLogging();

// Blob Storage, Web URL 생성을 위한 HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// DI 등록
builder.Services.AddScoped<ITextExtractService, TextExtractService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IFileRebuildService, FileRebuildService>();
builder.Services.AddScoped<IPptTranslateTool, PptTranslateTool>();

// MCP Server 초기화
IHost app = builder.BuildApp(useStreamableHttp);

// Run
await app.RunAsync();
