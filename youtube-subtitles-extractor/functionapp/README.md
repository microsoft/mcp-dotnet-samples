# MCP Server: Youtube Subtitles Extractor on Azure Functions

This is an MCP server, hosted on [Azure Functions](https://learn.microsoft.com/azure/azure-functions/functions-overview), that extracts subtitles from a given YouTube link.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
  - [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions) extension
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local?pivots=programming-language-csharp#install-the-azure-functions-core-tools)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## Getting Started

- [Run Azure Functions MCP server locally](#run-azure-functions-mcp-server-locally)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)
  - [VS Code + Agent Mode + Local MCP server](#vs-code--agent-mode--local-mcp-server)
  - [MCP Inspector + Local MCP server](#mcp-inspector--local-mcp-server)

> **NOTE**: Due to the limitation of the [Aliencube.YouTubeSubtitlesExtractor](https://www.nuget.org/packages/Aliencube.YouTubeSubtitlesExtractor) used in this sample app, it doesn't support the remote server deployed on Azure.

### Run Azure Functions MCP server locally

1. Get the repository root

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

1. Run the function app

    ```bash
    cd $REPOSITORY_ROOT/youtube-subtitles-extractor/functionapp/src/McpYouTubeSubtitlesExtractor.FunctionApp
    func start
    ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

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
    cp $REPOSITORY_ROOT/youtube-subtitles-extractor/functionapp/.vscode/mcp.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/youtube-subtitles-extractor/functionapp/.vscode/mcp.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-youtube-subtitles-extractor-function-local` then click `Start Server`.
1. Enter prompt like:

    ```text
    Summarise this YouTube video link in 5 bullet points: https://youtu.be/XwnEtZxaokg?si=V39ta45iMni_Uc_m
    ```

1. It will ask you to run `get_available_languages` followed by `get_subtitle`. You might be asked to choose language for the subtitle.
1. Confirm the summary of the video.

#### MCP Inspector + Local MCP server

1. Run MCP Inspector

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://0.0.0.0:6274)
1. Set the transport type to `SSE` 
1. Set the URL to your running Function app's SSE endpoint and **Connect**:

    ```text
    http://0.0.0.0:7071/runtime/webhooks/mcp/sse
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with a YouTube link and language code like `en` or `ko`.
