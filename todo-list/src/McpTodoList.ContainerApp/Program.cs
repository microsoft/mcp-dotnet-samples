using McpTodoList.ContainerApp.Data;
using McpTodoList.ContainerApp.OpenApi;
using McpTodoList.ContainerApp.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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
    o.AddDocumentTransformer<McpDocumentTransformer>();
});
builder.Services.AddOpenApi("openapi", o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    o.AddDocumentTransformer<McpDocumentTransformer>();
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
