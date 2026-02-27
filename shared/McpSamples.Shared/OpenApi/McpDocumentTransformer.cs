using System.Net.Mime;
using System.Text.Json.Nodes;

using McpSamples.Shared.Configurations;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using ModelContextProtocol.Protocol;

namespace McpSamples.Shared.OpenApi;

/// <summary>
/// This represents a transformer entity that defines the OpenAPI document for the MCP server.
/// </summary>
/// <param name="appsettings"><see cref="AppSettings"/> instance.</param>
/// <param name="accessor"><see cref="IHttpContextAccessor"/> instance.</param>
public class McpDocumentTransformer<T>(T appsettings, IHttpContextAccessor accessor) : IOpenApiDocumentTransformer where T : AppSettings, new()
{
    /// <inheritdoc />
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = appsettings.OpenApi.Title ?? "MCP Server",
            Version = appsettings.OpenApi.Version ?? "1.0.0",
            Description = appsettings.OpenApi.Description ?? "An MCP server"
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

        var jsonRpcResponse = await context.GetOrCreateSchemaAsync(typeof(JsonRpcResponse), cancellationToken: cancellationToken);

        var jsonRpcRequest = await context.GetOrCreateSchemaAsync(typeof(JsonRpcRequest), cancellationToken: cancellationToken);

        document.AddComponent(nameof(JsonRpcResponse), jsonRpcResponse);

        document.AddComponent(nameof(JsonRpcRequest), jsonRpcRequest);

        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(HttpMethod.Post, new OpenApiOperation
        {
            Summary = "Invoke operation",
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-ms-agentic-protocol"] = new JsonNodeExtension(JsonValue.Create("mcp-streamable-1.0"))
            },
            OperationId = "InvokeMCP",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Success",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcResponse), document),
                        },
                    },
                }
            },
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = new OpenApiSchemaReference(nameof(JsonRpcRequest), document),
                    },
                },
            },
        });

        document.Paths ??= [];
        document.Paths.Add("/mcp", pathItem);
    }
}