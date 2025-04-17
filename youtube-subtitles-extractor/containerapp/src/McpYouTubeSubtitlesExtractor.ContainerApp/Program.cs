using Aliencube.YouTubeSubtitlesExtractor;
using Aliencube.YouTubeSubtitlesExtractor.Abstractions;

using McpYouTubeSubtitlesExtractor.ContainerApp.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add YouTube Subtitles Extractor service.
builder.Services.AddHttpClient<IYouTubeVideo, YouTubeVideo>();

// Add MCP server.
builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Enable API key validation.
app.UseMcpAuth();

app.MapMcp();

app.Run();
