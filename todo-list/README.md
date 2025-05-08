# MCP Server: Todo List on ACA

This is an MCP server, hosted on [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview), that manages to-do lists.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## Getting Started

- [Run ASP.NET Core MCP server locally](#run-aspnet-core-mcp-server-locally)
- [Run ASP.NET Core MCP server locally in a container](#run-aspnet-core-mcp-server-locally-in-a-container)
- [Run ASP.NET Core MCP server remotely](#run-aspnet-core-mcp-server-remotely)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)
  - [VS Code + Agent Mode + Local MCP server](#vs-code--agent-mode--local-mcp-server)
  - [VS Code + Agent Mode + Local MCP server in a container](#vs-code--agent-mode--local-mcp-server-in-a-container)
  - [VS Code + Agent Mode + Remote MCP server](#vs-code--agent-mode--remote-mcp-server)
  - [MCP Inspector + Local MCP server](#mcp-inspector--local-mcp-server)
  - [MCP Inspector + Local MCP server in a container](#mcp-inspector--local-mcp-server-in-a-container)
  - [MCP Inspector + Remote MCP server](#mcp-inspector--remote-mcp-server)

### Run ASP.NET Core MCP server locally

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
    cd $REPOSITORY_ROOT/todo-list
    dotnet run --project ./src/McpTodoList.ContainerApp
    ```

### Run ASP.NET Core MCP server locally in a container

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
    cd $REPOSITORY_ROOT/todo-list
    docker build -t mcp-todo-list:latest .
    ```

1. Run the MCP server app in a container

    ```bash
    docker run -d -p 8080:8080 --name mcp-todo-list mcp-todo-list:latest
    ```

### Run ASP.NET Core MCP server remotely

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
     azd env get-value AZURE_RESOURCE_MCP_TODO_LIST_FQDN
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
    cp $REPOSITORY_ROOT/todo-list/.vscode/mcp.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/todo-list/.vscode/mcp.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-todo-list-aca-local` then click `Start Server`.
1. Enter prompts. These are just examples:

    ```text
    - Show me the list to do
    - Add "meeting at 11am"
    - Complete the to-do item #1
    - Delete the to-do item #2
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server in a container

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
    cp $REPOSITORY_ROOT/todo-list/.vscode/mcp.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/todo-list/.vscode/mcp.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-todo-list-aca-container` then click `Start Server`.
1. Enter prompts. These are just examples:

    ```text
    - Show me the list to do
    - Add "meeting at 11am"
    - Complete the to-do item #1
    - Delete the to-do item #2
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Remote MCP server

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-todo-list-aca-remote` then click `Start Server`.
1. Enter the Azure Container Apps FQDN.
1. Enter prompts. These are just examples:

    ```text
    - Show me the list to do
    - Add "meeting at 11am"
    - Complete the to-do item #1
    - Delete the to-do item #2
    ```

1. Confirm the result.

#### MCP Inspector + Local MCP server

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `SSE` 
1. Set the URL to your running Function app's SSE endpoint and **Connect**:

    ```text
    http://0.0.0.0:5242/sse
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server in a container

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

#### MCP Inspector + Remote MCP server

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
