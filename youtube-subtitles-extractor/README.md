# MCP Server: Youtube Subtitles Extractor

There are two MCP servers that extract subtitles from a given YouTube link. Both works exactly the same as each other. The only difference is that one is running on [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview) using the official [C# SDK](https://www.nuget.org/packages/ModelContextProtocol) and the other is running on [Azure Functions](https://learn.microsoft.com/azure/azure-functions/functions-overview) using the [Azure Functions SDK](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker.Extensions.Mcp).

## 🛠️ Getting Started

| Sample Name | Description |
|-------------|-------------|
| [YouTube Subtitles Extractor on Azure Container Apps](./containerapp/) | A remote MCP server, hosted on [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview), that can extract subtitles from any given YouTube video's URL. |
| [YouTube Subtitles Extractor on Azure Functions](./functionapp/) | A remote MCP server, hosted on [Azure Functions](https://learn.microsoft.com/azure/azure-functions/functions-overview), that can extract subtitles from any given YouTube video's URL. |