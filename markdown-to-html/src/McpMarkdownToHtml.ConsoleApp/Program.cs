using System.Reflection;

using McpMarkdownToHtml.Common.Configurations;
using McpMarkdownToHtml.Common.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var filepath = $"{AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}appsettings.json";
builder.Configuration
       .AddJsonFile(filepath, optional: false, reloadOnChange: true)
       .AddEnvironmentVariables();

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddAppSettings(builder.Configuration, args);

builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

await builder.Build().RunAsync();
