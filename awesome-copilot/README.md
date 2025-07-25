# MCP Server: Awesome Copilot

This is an MCP server that retrieves GitHub Copilot customizations from the [awesome-copilot](https://github.com/github/awesome-copilot) repository.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## Getting Started

- [Build ASP.NET Core MCP server (STDIO) locally in a container](#build-aspnet-core-mcp-server-stdio-locally-in-a-container)
- [Run ASP.NET Core MCP server (Streamable HTTP) locally](#run-aspnet-core-mcp-server-streamable-http-locally)
- [Run ASP.NET Core MCP server (Streamable HTTP) locally in a container](#run-aspnet-core-mcp-server-streamable-http-locally-in-a-container)
- [Run ASP.NET Core MCP server (Streamable HTTP) remotely](#run-aspnet-core-mcp-server-streamable-http-remotely)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)
  - [VS Code + Agent Mode + Local MCP server (STDIO)](#vs-code--agent-mode--local-mcp-server-stdio)
  - [VS Code + Agent Mode + Local MCP server (STDIO) in a container](#vs-code--agent-mode--local-mcp-server-stdio-in-a-container)
  - [VS Code + Agent Mode + Local MCP server (Streamable HTTP)](#vs-code--agent-mode--local-mcp-server-streamable-http)
  - [VS Code + Agent Mode + Local MCP server (Streamable HTTP) in a container](#vs-code--agent-mode--local-mcp-server-streamable-http-in-a-container)
  - [VS Code + Agent Mode + Remote MCP server (Streamable HTTP)](#vs-code--agent-mode--remote-mcp-server-streamable-http)
  - [MCP Inspector + Local MCP server (STDIO)](#mcp-inspector--local-mcp-server-stdio)
  - [MCP Inspector + Local MCP server (STDIO) in a container](#mcp-inspector--local-mcp-server-stdio-in-a-container)
  - [MCP Inspector + Local MCP server (Streamable HTTP)](#mcp-inspector--local-mcp-server-streamable-http)
  - [MCP Inspector + Local MCP server (Streamable HTTP) in a container](#mcp-inspector--local-mcp-server-streamable-http-in-a-container)
  - [MCP Inspector + Remote MCP server (Streamable HTTP)](#mcp-inspector--remote-mcp-server-streamable-http)
  - [Copilot Studio + Remote MCP server](#copilot-studio--remote-mcp-server)

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
    cd $REPOSITORY_ROOT/awesome-copilot
    docker build -f Dockerfile.stdio -t mcp-awesome-copilot-stdio:latest .
    ```

### Run ASP.NET Core MCP server (Streamable HTTP) locally

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
    cd $REPOSITORY_ROOT/awesome-copilot
    dotnet run --project ./src/McpAwesomeCopilot.ContainerApp
    ```

### Run ASP.NET Core MCP server (Streamable HTTP) locally in a container

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
    cd $REPOSITORY_ROOT/awesome-copilot
    docker build -f Dockerfile.http -t mcp-awesome-copilot-http:latest .
    ```

1. Run the MCP server app in a container

    ```bash
    docker run -d -p 8080:8080 --name mcp-awesome-copilot-http mcp-awesome-copilot-http:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -d -p 8080:8080 --name mcp-awesome-copilot-http ghcr.io/microsoft/mcp-dotnet-samples/awesome-copilot:http
    ```

### Run ASP.NET Core MCP server (Streamable HTTP) remotely

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
     azd env get-value AZURE_RESOURCE_MCP_AWESOME_COPILOT_FQDN
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
    cp $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-awesome-copilot-stdio-local` then click `Start Server`.
1. When prompted, enter the absolute directory of the `McpAwesomeCopilot.ConsoleApp` project.
1. Enter prompt like:

    ```text
    Show me the list of copilot instructions about react.
    Save the react instruction to .github/copilot-instructions.md
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
    cp $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   > **NOTE**: If you want to use the container image from the container registry, replace `mcp-awesome-copilot-stdio:latest` with `ghcr.io/microsoft/mcp-dotnet-samples/awesome-copilot:stdio` in the `.vscode/mcp.json` file

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-awesome-copilot-stdio-container` then click `Start Server`.
1. Enter prompt like:

    ```text
    Show me the list of copilot instructions about react.
    Save the react instruction to .github/copilot-instructions.md
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server (Streamable HTTP)

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
    cp $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-awesome-copilot-http-local` then click `Start Server`.
1. Enter prompt like:

    ```text
    Show me the list of copilot instructions about react.
    Save the react instruction to .github/copilot-instructions.md
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Local MCP server (Streamable HTTP) in a container

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
    cp $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-awesome-copilot-http-container` then click `Start Server`.
1. Enter prompt like:

    ```text
    Show me the list of copilot instructions about react.
    Save the react instruction to .github/copilot-instructions.md
    ```

1. Confirm the result.

#### VS Code + Agent Mode + Remote MCP server (Streamable HTTP)

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
    cp $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-copilot/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `mcp-awesome-copilot-http-remote` then click `Start Server`.
1. Enter the Azure Container Apps FQDN.
1. Enter prompt like:

    ```text
    Show me the list of copilot instructions about react.
    Save the react instruction to .github/copilot-instructions.md
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
    run --project {{absolute/path/to/awesome-copilot}}/src/McpAwesomeCopilot.ConsoleApp
    ```

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
    run -i --rm mcp-awesome-copilot-stdio:latest
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server (Streamable HTTP)

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `Streamable HTTP`.
1. Set the URL to your running Function app's Streamable HTTP endpoint and **Connect**:

    ```text
    http://0.0.0.0:5280/mcp
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Local MCP server (Streamable HTTP) in a container

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://localhost:6274)
1. Set the transport type to `Streamable HTTP`.
1. Set the URL to your running Function app's Streamable HTTP endpoint and **Connect**:

    ```text
    http://0.0.0.0:8080/mcp
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### MCP Inspector + Remote MCP server (Streamable HTTP)

1. Run MCP Inspector.

    ```bash
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. Open a web browser and navigate to the MCP Inspector web app from the URL displayed by the app (e.g. http://0.0.0.0:6274)
1. Set the transport type to `Streamable HTTP`.
1. Set the URL to your running Function app's Streamable HTTP endpoint and **Connect**:

    ```text
    https://<acaapp-server-fqdn>/mcp
    ```

1. Click **List Tools**.
1. Click on a tool and **Run Tool** with appropriate values.

#### Copilot Studio + Remote MCP server

1. The remote MCP server renders a Swagger document at

    ```text
    https://<acaapp-server-fqdn>/swagger.json
    ```

   Alternatively, you've got `swagger.json` on this app built at your project root directory on your local machine.

1. Create a custom connector with this Swagger document on either [Power Automate](https://make.powerautomate.com) or [Power Apps](https://make.powerapps.com).
1. Go to [Copilot Studio](https://copilotstudio.microsoft.com) and create a new agent.
1. Add MCP connector to the agent.
1. Run the agent with appropriate values.
