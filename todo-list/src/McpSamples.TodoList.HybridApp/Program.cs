using McpSamples.Shared.Configurations;
using McpSamples.Shared.Extensions;
using McpSamples.Shared.OpenApi;

using McpSamples.TodoList.HybridApp.Data;
using McpSamples.TodoList.HybridApp.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAppSettings<TodoListAppSettings>(builder.Configuration, args);

var connection = new SqliteConnection("Filename=:memory:");
connection.Open();

builder.Services.AddSingleton(connection);

builder.Services.AddDbContext<TodoDbContext>(options => options.UseSqlite(connection));
builder.Services.AddScoped<ITodoRepository, TodoRepository>();

builder.Services.AddMcpServer()
                .WithHttpTransport(o => o.Stateless = true)
                .WithToolsFromAssembly();

builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi("swagger", o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
    o.AddDocumentTransformer<McpDocumentTransformer<TodoListAppSettings>>();
});
builder.Services.AddOpenApi("openapi", o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    o.AddDocumentTransformer<McpDocumentTransformer<TodoListAppSettings>>();
});

var app = builder.Build();

// Initialise the database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapOpenApi("/{documentName}.json");

app.MapMcp("/mcp");

app.Run();
