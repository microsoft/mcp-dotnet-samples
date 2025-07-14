using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace McpTodoList.ContainerApp.OpenApi;

public class McpDocumentTransformer(IHttpContextAccessor accessor) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MCP Todo Management",
            Version = "1.0.0",
            Description = "A simple MCP server for managing todo list items."
        };
        document.Servers =
        [
            new OpenApiServer
            {
                Url = accessor.HttpContext != null
                    ? $"{accessor.HttpContext.Request.Scheme}://{accessor.HttpContext.Request.Host}/"
                    : "http://localhost:8080/"
            }
        ];
        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(OperationType.Post, new OpenApiOperation
        {
            Summary = "Todo List",
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-ms-agentic-protocol"] = new OpenApiString("mcp-streamable-1.0")
            },
            OperationId = "InvokeMCP",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Success",
                }
            }
        });

        document.Paths ??= [];
        document.Paths.Add("/mcp", pathItem);

        return Task.CompletedTask;
    }
}