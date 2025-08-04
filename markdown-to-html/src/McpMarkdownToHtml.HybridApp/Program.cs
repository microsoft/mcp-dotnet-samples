using System.Reflection;

using McpMarkdownToHtml.Common.Configurations;
using McpMarkdownToHtml.Common.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(args);
IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

// Add services to the container.
builder.Services.AddAppSettings(builder.Configuration, args);

if (useStreamableHttp)
{
    builder.Services.AddMcpServer()
                    .WithHttpTransport(o => o.Stateless = true)
                    .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

    var app = (builder as WebApplicationBuilder)!.Build();

    // Configure the HTTP request pipeline.
    app.UseHttpsRedirection();

    app.MapMcp("/mcp");

    app.Run();

}
else
{
    builder.Services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

    await (builder as HostApplicationBuilder)!.Build().RunAsync();
}
