using McpAssemblyAnalyzer.Common.Tools;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// builder.Services.AddAppSettings(builder.Configuration, args);

builder.Services.AddMcpServer()
                .WithHttpTransport(o => o.Stateless = true)
                .WithTools<AssemblyDetailsTool>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapMcp("/mcp");

app.Run();
