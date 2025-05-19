using System.Reflection;

using McpMarkdownToHtml.Common.Configurations;
using McpMarkdownToHtml.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAppSettings(builder.Configuration, args);

builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly(Assembly.GetAssembly(typeof(AppSettings)) ?? Assembly.GetExecutingAssembly());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapMcp();

app.Run();
