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
public sealed class McpDocumentTransformer<T>(T appsettings, IHttpContextAccessor accessor) : IOpenApiDocumentTransformer where T : AppSettings, new()
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

        // Register JSON-RPC schemas as components
        var jsonRpcRequest = await context.GetOrCreateSchemaAsync(typeof(JsonRpcRequest), cancellationToken: cancellationToken);
        var jsonRpcNotification = await context.GetOrCreateSchemaAsync(typeof(JsonRpcNotification), cancellationToken: cancellationToken);
        var jsonRpcResponse = await context.GetOrCreateSchemaAsync(typeof(JsonRpcResponse), cancellationToken: cancellationToken);
        var jsonRpcError = await context.GetOrCreateSchemaAsync(typeof(JsonRpcError), cancellationToken: cancellationToken);

        document.AddComponent(nameof(JsonRpcRequest), jsonRpcRequest);
        document.AddComponent(nameof(JsonRpcNotification), jsonRpcNotification);
        document.AddComponent(nameof(JsonRpcResponse), jsonRpcResponse);
        document.AddComponent(nameof(JsonRpcError), jsonRpcError);

        // Build oneOf schema for request body per MCP Streamable HTTP spec:
        // "The body of the POST request MUST be a single JSON-RPC request, notification, or response."
        var jsonRpcMessage = new OpenApiSchema
        {
            OneOf =
            [
                new OpenApiSchemaReference(nameof(JsonRpcRequest), document),
                new OpenApiSchemaReference(nameof(JsonRpcNotification), document),
                new OpenApiSchemaReference(nameof(JsonRpcResponse), document),
            ]
        };
        document.AddComponent("JsonRpcMessage", jsonRpcMessage);

        var pathItem = new OpenApiPathItem();

        // POST /mcp - Send a JSON-RPC request, notification, or response
        pathItem.AddOperation(HttpMethod.Post, new OpenApiOperation
        {
            Summary = "Invoke operation",
            Description = "Send a JSON-RPC request, notification, or response to the MCP server.",
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-ms-agentic-protocol"] = new JsonNodeExtension(JsonValue.Create("mcp-streamable-1.0"))
            },
            OperationId = "InvokeMCP",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Success - returned when the input is a JSON-RPC request",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcResponse), document),
                        },
                        [MediaTypeNames.Text.EventStream] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Description = "Server-Sent Events stream containing JSON-RPC responses",
                            },
                        },
                    },
                },
                ["202"] = new OpenApiResponse
                {
                    Description = "Accepted - returned when the input is a JSON-RPC response or notification",
                },
                ["400"] = new OpenApiResponse
                {
                    Description = "Bad Request - invalid JSON-RPC message, unsupported protocol version, or missing/invalid session ID",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["403"] = new OpenApiResponse
                {
                    Description = "Forbidden - invalid Origin header, or the authenticated user does not match the user who initiated the session",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["404"] = new OpenApiResponse
                {
                    Description = "Not Found - the specified session ID was not found",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["406"] = new OpenApiResponse
                {
                    Description = "Not Acceptable - client must accept both application/json and text/event-stream",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
            },
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.Json] = new()
                    {
                        Schema = new OpenApiSchemaReference("JsonRpcMessage", document),
                    },
                },
            },
        });

        // GET /mcp - Open SSE stream for server-initiated messages (stateful mode only)
        pathItem.AddOperation(HttpMethod.Get, new OpenApiOperation
        {
            Summary = "Open SSE stream",
            Description = "Open a Server-Sent Events stream to receive server-initiated JSON-RPC messages. Only available in stateful mode.",
            OperationId = "OpenMCPStream",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "SSE stream opened successfully",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Text.EventStream] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Description = "Server-Sent Events stream containing JSON-RPC messages",
                            },
                        },
                    },
                },
                ["400"] = new OpenApiResponse
                {
                    Description = "Bad Request - missing session ID, unsupported protocol version, or invalid Last-Event-ID",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["404"] = new OpenApiResponse
                {
                    Description = "Not Found - the specified session ID was not found",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["405"] = new OpenApiResponse
                {
                    Description = "Method Not Allowed - server does not offer an SSE stream at this endpoint",
                },
                ["406"] = new OpenApiResponse
                {
                    Description = "Not Acceptable - client must accept text/event-stream",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
            },
        });

        // DELETE /mcp - Terminate a session (stateful mode only)
        pathItem.AddOperation(HttpMethod.Delete, new OpenApiOperation
        {
            Summary = "Terminate session",
            Description = "Terminate an active MCP session and clean up server-side resources. Only available in stateful mode.",
            OperationId = "TerminateMCPSession",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Session terminated successfully",
                },
                ["400"] = new OpenApiResponse
                {
                    Description = "Bad Request - missing session ID or unsupported protocol version",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["404"] = new OpenApiResponse
                {
                    Description = "Not Found - the specified session ID was not found",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchemaReference(nameof(JsonRpcError), document),
                        },
                    },
                },
                ["405"] = new OpenApiResponse
                {
                    Description = "Method Not Allowed - server does not allow clients to terminate sessions",
                },
            },
        });

        document.Paths ??= [];
        document.Paths.Add("/mcp", pathItem);
    }
}
