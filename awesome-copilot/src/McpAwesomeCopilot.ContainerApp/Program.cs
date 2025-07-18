using System.Reflection;
using System.Text.Json;

using McpAwesomeCopilot.Common.Services;
using McpAwesomeCopilot.ContainerApp.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi("swagger", o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
    o.AddDocumentTransformer<McpDocumentTransformer>();
});
builder.Services.AddOpenApi("openapi", o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    o.AddDocumentTransformer<McpDocumentTransformer>();
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
                .WithHttpTransport(o => o.Stateless = true)
                .WithToolsFromAssembly(Assembly.GetAssembly(typeof(IMetadataService)) ?? Assembly.GetExecutingAssembly());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapOpenApi("/{documentName}.json");

app.MapMcp("/mcp");

app.Run();
