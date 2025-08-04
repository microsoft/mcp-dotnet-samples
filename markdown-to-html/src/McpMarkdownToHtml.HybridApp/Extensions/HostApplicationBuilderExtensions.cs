using System.Reflection;

using McpMarkdownToHtml.Common.Configurations;

namespace McpMarkdownToHtml.HybridApp.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHost BuildApp(this IHostApplicationBuilder builder, bool useStreamableHttp)
    {
        if (useStreamableHttp)
        {
            builder.Services.AddMcpServer()
                            .WithHttpTransport(o => o.Stateless = true)
                            .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

            var webApp = (builder as WebApplicationBuilder)!.Build();

            // Configure the HTTP request pipeline.
            webApp.UseHttpsRedirection();

            webApp.MapMcp("/mcp");

            return webApp;
        }

        builder.Services.AddMcpServer()
                        .WithStdioServerTransport()
                        .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

        var consoleApp = (builder as HostApplicationBuilder)!.Build();

        return consoleApp;
    }
}