# MCP Server: Awesome Copilot

This is an MCP server that integrates with [Kiota](https://github.com/microsoft/kiota) to generate an SDK from OpenAPI documents.

```bash
    docker run -i --rm -p 8080:8080 -v "$(pwd)/output:/app/generated" openapi-to-sdk:latest
```

```bash
    docker run -i --rm -p 8080:8080 -v "$(pwd)/output:/app/wwwroot/generated" openapi-to-sdk:latest
```