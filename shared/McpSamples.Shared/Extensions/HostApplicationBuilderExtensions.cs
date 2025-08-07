using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace McpSamples.Shared.Extensions;

/// <summary>
/// This represents the extension entity for <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Builds the application with the specified <paramref name="useStreamableHttp"/> option.
    /// </summary>
    /// <param name="builder"><see cref="IHostApplicationBuilder"/> instance.</param>
    /// <param name="useStreamableHttp">Value indicating whether to use streamable HTTP or not.</param>
    /// <returns>Returns the <see cref="IHost"/> instance.</returns>
    public static IHost BuildApp(this IHostApplicationBuilder builder, bool useStreamableHttp)
    {
        if (useStreamableHttp == true)
        {
            builder.Services.AddMcpServer()
                            .WithHttpTransport(o => o.Stateless = true)
                            .WithPromptsFromAssembly(Assembly.GetEntryAssembly())
                            .WithResourcesFromAssembly(Assembly.GetEntryAssembly())
                            .WithToolsFromAssembly(Assembly.GetEntryAssembly());

            var webApp = (builder as WebApplicationBuilder)!.Build();

            // Configure the HTTP request pipeline.
            webApp.UseHttpsRedirection();

            webApp.MapMcp("/mcp");

            return webApp;
        }

        builder.Services.AddMcpServer()
                        .WithStdioServerTransport()
                        .WithPromptsFromAssembly(Assembly.GetEntryAssembly())
                        .WithResourcesFromAssembly(Assembly.GetEntryAssembly())
                        .WithToolsFromAssembly(Assembly.GetEntryAssembly());

        var consoleApp = (builder as HostApplicationBuilder)!.Build();

        return consoleApp;
    }
}