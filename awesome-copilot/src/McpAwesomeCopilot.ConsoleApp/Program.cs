using System.Reflection;
using System.Text.Json;

using McpAwesomeCopilot.Common.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true
};
builder.Services.AddSingleton(options);

builder.Services.AddHttpClient<IMetadataService, MetadataService>();

builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(Assembly.GetAssembly(typeof(MetadataService)) ?? Assembly.GetExecutingAssembly())
                .WithPromptsFromAssembly(Assembly.GetAssembly(typeof(MetadataService)) ?? Assembly.GetExecutingAssembly());

await builder.Build().RunAsync();
