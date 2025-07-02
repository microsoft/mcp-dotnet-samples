using McpAssemblyAnalyzer.Common.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<AssemblyDetailsTool>();

await builder.Build().RunAsync();
