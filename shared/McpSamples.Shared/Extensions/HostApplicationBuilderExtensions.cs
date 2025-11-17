using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace McpSamples.Shared.Extensions;

/// <summary>
/// This represents the extension entity for <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Determines if the application is running in Azure Functions environment.
    /// </summary>
    /// <returns>True if running in Azure Functions, false otherwise.</returns>
    private static bool IsAzureFunctionsEnvironment()
    {
        // Azure Functions always has these environment variables set
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsStorage")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
    }
    /// <summary>
    /// Builds the application with the specified <paramref name="useStreamableHttp"/> option.
    /// </summary>
    /// <param name="builder"><see cref="IHostApplicationBuilder"/> instance.</param>
    /// <param name="useStreamableHttp">Value indicating whether to use streamable HTTP or not.</param>
    /// <returns>Returns the <see cref="IHost"/> instance.</returns>
    public static IHost BuildApp(this IHostApplicationBuilder builder, bool useStreamableHttp)
    {
        // Get the entry assembly, fallback to calling assembly if null (for Azure Functions)
        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();

        if (useStreamableHttp == true)
        {
            builder.Services.AddMcpServer()
                            .WithHttpTransport(o => o.Stateless = true)
                            .WithPromptsFromAssembly(entryAssembly)
                            .WithResourcesFromAssembly(entryAssembly)
                            .WithToolsFromAssembly(entryAssembly);

            var webApp = (builder as WebApplicationBuilder)!.Build();

            // Configure the HTTP request pipeline.
            // Note: Skip HTTPS redirection in Azure Functions environment (uses HTTP only)
            if (!IsAzureFunctionsEnvironment())
            {
                webApp.UseHttpsRedirection();
            }

            // Map MCP endpoint
            webApp.MapMcp("/mcp");

            // Handle other routes - return 404
            webApp.MapFallback(async context =>
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Not Found");
            });

            return webApp;
        }

        builder.Services.AddMcpServer()
                        .WithStdioServerTransport()
                        .WithPromptsFromAssembly(entryAssembly)
                        .WithResourcesFromAssembly(entryAssembly)
                        .WithToolsFromAssembly(entryAssembly);

        var consoleApp = (builder as HostApplicationBuilder)!.Build();

        return consoleApp;
    }
}