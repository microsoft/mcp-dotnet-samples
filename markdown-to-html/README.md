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

- [Build ASP.NET Core MCP server (STDIO) locally in a container](#build-aspnet-core-mcp-server-stdio-locally-in-a-container)
- [Run ASP.NET Core MCP server (SSE) locally](#run-aspnet-core-mcp-server-sse-locally)
- [Run ASP.NET Core MCP server (SSE) locally in a container](#run-aspnet-core-mcp-server-sse-locally-in-a-container)
- [Run ASP.NET Core MCP server (SSE) remotely](#run-aspnet-core-mcp-server-sse-remotely)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)
  - [VS Code + Agent Mode + Local MCP server (STDIO)](#vs-code--agent-mode--local-mcp-server-stdio)
  - [VS Code + Agent Mode + Local MCP server (STDIO) in a container](#vs-code--agent-mode--local-mcp-server-stdio-in-a-container)
  - [VS Code + Agent Mode + Local MCP server (SSE)](#vs-code--agent-mode--local-mcp-server-sse)
  - [VS Code + Agent Mode + Local MCP server (SSE) in a container](#vs-code--agent-mode--local-mcp-server-sse-in-a-container)
  - [VS Code + Agent Mode + Remote MCP server (SSE)](#vs-code--agent-mode--remote-mcp-server-sse)
  - [MCP Inspector + Local MCP server (STDIO)](#mcp-inspector--local-mcp-server-stdio)
  - [MCP Inspector + Local MCP server (STDIO) in a container](#mcp-inspector--local-mcp-server-stdio-in-a-container)
  - [MCP Inspector + Local MCP server (SSE)](#mcp-inspector--local-mcp-server-sse)
  - [MCP Inspector + Local MCP server (SSE) in a container](#mcp-inspector--local-mcp-server-sse-in-a-container)
  - [MCP Inspector + Remote MCP server (SSE)](#mcp-inspector--remote-mcp-server-sse)

### Build ASP.NET Core MCP server (STDIO) locally in a container

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    docker build -f Dockerfile.stdio -t mcp-md2html-stdio:latest .
    ```

### Run ASP.NET Core MCP server (SSE) locally

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    dotnet run --project ./src/McpMarkdownToHtml.ContainerApp
    ```

   > **NOTE**: If you're converting the markdown text for [Microsoft Tech Community](https://techcommunity.microsoft.com/), the following parameters are helpful to pass.
   >
   > - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   > - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   > - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`
   >
   > With these parameters, you can run the MCP server like:
   >
   > ```bash
   > dotnet run --project ./src/McpMarkdownToHtml.ContainerApp -- -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   > ```

### Run ASP.NET Core MCP server (SSE) locally in a container

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    docker build -f Dockerfile.sse -t mcp-md2html-sse:latest .
    ```

1. Run the MCP server app in a container

    ```bash
    docker run -d -p 8080:8080 --name mcp-md2html-sse mcp-md2html-sse:latest
    ```

   > **NOTE**: If you're converting the markdown text for [Microsoft Tech Community](https://techcommunity.microsoft.com/), the following parameters are helpful to pass.
   >
   > - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   > - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   > - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`
   >
   > With these parameters, you can run the MCP server like:
   >
   > ```bash
   > docker run -d -p 8080:8080 --name mcp-md2html-sse mcp-md2html-sse:latest -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   > ```

### Run ASP.NET Core MCP server (SSE) remotely

1. Login to Azure

    ```bash
    # Login with Azure Developer CLI
    azd auth login
    ```

1. Deploy the MCP server app to Azure

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

#### VS Code + Agent Mode + Local MCP server (STDIO)

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Copy `mcp.json` to the repository root.

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

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-md2html-stdio-local` then click `Start Server`.
1. When prompted, enter the absolute directory of the `McpMarkdownToHtml.ConsoleApp` project.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to converted.html
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server (STDIO) in a container

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Copy `mcp.json` to the repository root.

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

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-md2html-stdio-container` then click `Start Server`.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to converted.html
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server (SSE)

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Copy `mcp.json` to the repository root.

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-md2html-sse-local` then click `Start Server`.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to converted.html
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server (SSE) in a container

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Copy `mcp.json` to the repository root.

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-md2html-sse-container` then click `Start Server`.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to converted.html
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Remote MCP server (SSE)

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Copy `mcp.json` to the repository root.

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.sse.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-md2html-sse-remote` then click `Start Server`.
1. Enter the Azure Container Apps FQDN.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to converted.html
    ```

1. Confirm the result.

#### MCP Inspector + Local MCP server (STDIO)

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `STDIO`
1. Set the command to `dotnet`
1. Set the arguments that pointing to the console app project and **Connect**:

    ```text
    run --project {{absolute/path/to/markdown-to-html}}/src/McpMarkdownToHtml.ConsoleApp
    ```

   > **NOTE**:
   >
   > 1. If you're converting the markdown text for [Microsoft Tech Community](https://techcommunity.microsoft.com/), the following parameters are helpful to pass.
   >
   >    - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   >    - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   >    - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`
   >
   >    With these parameters, the arguments value can be:
   >
   >     ```bash
   >     run --project {{absolute/path/to/markdown-to-html}}/src/McpMarkdownToHtml.ConsoleApp -- -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   >     ```
   >
   > 1. The project path MUST be the absolute path.

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server (STDIO) in a container

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `STDIO`
1. Set the command to `docker`
1. Set the arguments that pointing to the console app project and **Connect**:

    ```text
    run -i --rm mcp-md2html-stdio:latest
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server (SSE)

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `SSE` 
1. Set the URL to your running Function app's SSE endpoint and **Connect**:

    ```text
    http://0.0.0.0:5280/sse
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server (SSE) in a container

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `SSE` 
1. Set the URL to your running Function app's SSE endpoint and **Connect**:

    ```text
    http://0.0.0.0:8080/sse
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Remote MCP server (SSE)

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://0.0.0.0:6274)
1. Set the transport type to `SSE` 
1. Set the URL to your running Function app's SSE endpoint and **Connect**:

    ```text
    https://<acaapp-server-fqdn>/sse
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.
