using System.Reflection;

using McpSamples.Shared.Configurations;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace McpSamples.Shared.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHost BuildApp(this IHostApplicationBuilder builder, bool useStreamableHttp)
    {
        if (useStreamableHttp == true)
        {
            builder.Services.AddMcpServer()
                            .WithHttpTransport(o => o.Stateless = true)
                            .WithToolsFromAssembly(Assembly.GetEntryAssembly());

            var webApp = (builder as WebApplicationBuilder)!.Build();

            // Configure the HTTP request pipeline.
            webApp.UseHttpsRedirection();

            webApp.MapMcp("/mcp");

            return webApp;
        }

        builder.Services.AddMcpServer()
                        .WithStdioServerTransport()
                            .WithToolsFromAssembly(Assembly.GetEntryAssembly());

        var consoleApp = (builder as HostApplicationBuilder)!.Build();

        return consoleApp;
    }
}