# MCP Server: Markdown to HTML

This is an MCP server that converts markdown text to HTML.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## Getting Started

- [Getting repository root](#getting-repository-root)
- [Running MCP server](#running-mcp-server)
  - [On a local machine](#on-a-local-machine)
  - [In a container](#in-a-container)
  - [On Azure](#on-azure)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)
  - [VS Code + Agent Mode + Local MCP server](#vs-code--agent-mode--local-mcp-server)

### Getting repository root

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

### Running MCP server

#### On a local machine

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    dotnet run --project ./src/McpMarkdownToHtml.HybridApp
    ```

   > Make sure take note the absolute directory of the `McpMarkdownToHtml.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5280`.
   - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`

   With these parameters, you can run the MCP server like:

   ```bash
   dotnet run --project ./src/McpMarkdownToHtml.HybridApp -- --http -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    docker build -f Dockerfile -t markdown-to-html:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 markdown-to-html:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/markdown-to-html:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.
   - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`

   With these parameters, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 markdown-to-html:latest --http -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/markdown-to-html:latest --http -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    ```

1. Login to Azure.

    ```bash
    # Login with Azure Developer CLI
    azd auth login
    ```

1. Deploy the MCP server app to Azure.

    ```bash
    azd up
    ```

   While provisioning and deploying, you'll be asked to provide subscription ID, location, environment name.

1. After the deployment is complete, get the information by running the following commands:

   - Azure Container Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_MD2HTML_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `markdown-to-html` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory of the `McpMarkdownToHtml.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to `converted.html`.
    ```

1. Confirm the result.
