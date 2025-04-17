using Aliencube.YouTubeSubtitlesExtractor;
using Aliencube.YouTubeSubtitlesExtractor.Abstractions;

using McpYouTubeSubtitlesExtractor.ContainerApp.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add YouTube Subtitles Extractor service.
builder.Services.AddHttpClient<IYouTubeVideo, YouTubeVideo>();

// Add MCP server.
builder.Services.AddMcpServer()
                .WithToolsFromAssembly();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable API key validation.
app.UseApiKeyValidation();

app.MapGet("/", () => "Hello, this is the MCP Server that extracts subtitles from a YouTube video!");
app.MapMcp();

app.Run();
