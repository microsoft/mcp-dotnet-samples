using System.Net;

namespace McpYouTubeSubtitlesExtractor.ContainerApp.Middlewares;

public class ApiKeyValidationMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string ApiKeyHeaderName = "x-api-key";
    private const string ApiKeyQueryParameterName = "code";

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly string _expectedApiKey = config["Mcp:ApiKey"] ?? throw new ArgumentNullException(nameof(config), "API key not found in configuration.");

    public async Task InvokeAsync(HttpContext context)
    {
        // Bypass localhost requests
        var localhost = IsLocalhost(context.Connection);
        if (localhost == true)
        {
            await this._next(context);
            return;
        }

        var apiKey = GetApiKeyFromRequest(context.Request);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("API key not found.");
            await this._next(context);
            return;
        }
        if (string.Equals(apiKey, this._expectedApiKey, StringComparison.InvariantCultureIgnoreCase) == false)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid API key.");
            await this._next(context);
            return;
        }

        await this._next(context);
    }

    private static bool IsLocalhost(ConnectionInfo connection)
    {
        if (connection.RemoteIpAddress == null && connection.LocalIpAddress == null)
        {
            return true;
        }

        if (connection.RemoteIpAddress != null)
        {
            return connection.LocalIpAddress != null
                ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                : IPAddress.IsLoopback(connection.RemoteIpAddress);
        }

        return false;
    }

    private static string? GetApiKeyFromRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue))
        {
            return headerValue.ToString();
        }

        if (request.Query.TryGetValue(ApiKeyQueryParameterName, out var queryValue))
        {
            return queryValue.ToString();
        }

        return null;
    }
}

public static class ApiKeyValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyValidation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<ApiKeyValidationMiddleware>();

        return app;
    }
}
