# MCP Server: PPT Font Fix

This is an MCP server that finds fonts in a .pptx file, analyzes their usages - used, unused, inconsistently used, and fixes it.

## Install

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)]() [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)]()

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## What's Included

PPT Font Fix MCP server includes:

| Building Block | Name                       | Description                         | Usage                       |
|----------------|----------------------------|-------------------------------------|-----------------------------|
| Tools          | `analyze_ppt_file`         | Opens and analyzes fonts used in a PPTX file.       | `#analyze_ppt_file` |
| Tools          | `update_ppt_file`          | Replaces inconsistent fonts and saves the modified file.     | `#update_ppt_file` |
| Prompts        | `fix_ppt_fonts`            | Structured workflow to guide file upload and repair process. | `/mcp.ppt-font-fix.fix_ppt_fonts ` |

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
    cd $REPOSITORY_ROOT/ppt-font-fix
    dotnet run --project ./src/McpSamples.PptFontFix.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.PptFontFix.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5166`.

   With these parameters, you can run the MCP server like:

   ```bash
   dotnet run --project ./src/McpSamples.PptFontFix.HybridApp -- --http
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.ppt-font-fix -t ppt-font-fix:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 ppt-font-fix:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/ppt-font-fix:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With these parameters, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -it --rm -p 8080:8080 -u 0 --name ppt-font-fix ppt-font-fix:latest --http
   ```

   ```bash
   # use container image from the container registry
   docker run -it --rm -p 8080:8080 -u 0 --name ppt-font-fix ghcr.io/microsoft/mcp-dotnet-samples/ppt-font-fix:latest --http
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/ppt-font-fix
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
     azd env get-value AZURE_RESOURCE_MCP_PPT_FONT_FIX_FQDN
     ```

     If you want to use Azure, you must copy the account key or upload the file to the blob storage on the running server, and copy the URL of the uploaded file.

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-font-fix/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `ppt-font-fix` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.PptFontFix.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Enter prompt like:

    ```text
    Analyze and Fix the fonts in C:\path\to\presentation.pptx
    ```

1. Confirm the result.
