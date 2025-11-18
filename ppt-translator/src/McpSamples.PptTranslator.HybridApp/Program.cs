using McpSamples.PptTranslator.HybridApp.Configurations;
using McpSamples.PptTranslator.HybridApp.Services;
using McpSamples.PptTranslator.HybridApp.Prompts;
using McpSamples.PptTranslator.HybridApp.Tools;
using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
    ? WebApplication.CreateBuilder(args)
    : Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddAppSettings<PptTranslatorAppSettings>(builder.Configuration, args);

builder.Services.AddLogging();
builder.Services.AddScoped<ITextExtractService, TextExtractService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<ITranslationPrompt, TranslationPrompt>();
builder.Services.AddScoped<IFileRebuildService, FileRebuildService>();
builder.Services.AddScoped<IPptTranslateTool, PptTranslateTool>();

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();


