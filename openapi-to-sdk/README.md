# MCP Server: OpenAPI to SDK Generator

This is an MCP server that generates client SDKs from OpenAPI specifications using Microsoft Kiota.

## Install

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)]() [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)]() [![Install in Visual Studio](https://img.shields.io/badge/Visual_Studio-Install-C16FDE?logo=visualstudio&logoColor=white)]()

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## What's Included

OpenAPI to SDK MCP server includes:

| Building Block | Name           | Description                                                                                             | Usage                 |
|----------------|----------------|---------------------------------------------------------------------------------------------------------|-----------------------|
| Tools          | `generate_sdk` | Generates a client SDK from an OpenAPI specification (URL or raw content) and returns a download link.  | `#generate_sdk`       |
| Prompts        | `generate_sdk` | A structured prompt that guides the LLM to generate an SDK, handling language normalization and inputs. | `/mcp.openapi-to-sdk.generate_sdk` |

## Getting Started

- [Getting repository root](#getting-repository-root)
- [Running MCP server](#running-mcp-server)
  - [On a local machine](#on-a-local-machine)
  - [In a container](#in-a-container)
  - [On Azure](#on-azure)
- [Connect MCP server to an MCP host/client](#connect-mcp-server-to-an-mcp-hostclient)

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

##### Prerequisites
- [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/install?tabs=bash)

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/openapi-to-sdk
    dotnet run --project ./src/McpSamples.OpenApiToSdk.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.OpenApiToSdk.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5220`.

   Example running in HTTP mode:

   ```bash
   dotnet run --project ./src/McpSamples.OpenApiToSdk.HybridApp -- --http

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.openapi-to-sdk -t openapi-to-sdk:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 openapi-to-sdk:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/openapi-to-sdk:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With this parameter, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 openapi-to-sdk:latest --http
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/openapi-to-sdk:latest --http
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/openapi-to-sdk
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
     azd env get-value AZURE_RESOURCE_MCP_OPENAPI_TO_SDK_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/openapi-to-sdk/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `openapi-to-sdk` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.OpenApiToSdk.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Use a prompt by typing `/mcp.openapi-to-sdk.generate_sdk` and enter keywords to search. You'll get a prompt like:

    ```text
    You are an expert SDK generator using Microsoft Kiota.

    1. User Input Analysis
    - OpenAPI Source: "{openApiSource}"
    - Target Language: "{language}"
    - Configuration:
        - Class Name: {className ?? "Default (ApiClient)"}
        - Namespace: {namespaceName ?? "Default (ApiSdk)"}
        - Options: {additionalOptions ?? "None"}

    ---
    2. Execution Strategy (Follow Strictly)

    Step 1: Validate & Normalize Language
    Match the input to a valid Kiota identifier: [ CSharp, Go, Java, PHP, Python, Ruby, Shell, Swift, TypeScript ].
    - If a match or alias is found (e.g., "ts" -> "TypeScript", "golang" -> "Go"), use the valid identifier.
    - If NO match is found (e.g., "Rust", "C++", "asdf"), STOP immediately and ask the user to provide a supported language.

    Step 2: Resolve OpenAPI Source (CRITICAL)
    The 'generate_sdk' tool accepts either a URL or Raw Content, but NOT a file path.
    Analyze the [OpenAPI Source] provided above:
    
    - CASE A: It is a URL (starts with http/https)
        - Action: Pass the URL string directly to the `specSource` argument.
    
    - CASE B: It looks like a File Path (e.g., "C:\specs\api.json", "./swagger.yaml")
        - Action: You MUST first read the content of this file using your available tools (e.g., `filesystem` tool).
        - Then, pass the file content (JSON/YAML text) to the `specSource` argument.
        - If you cannot read the file, ask the user to paste the content directly.

    - CASE C: It is Raw JSON/YAML Content
        - Action: Pass the content string directly to the `specSource` argument.

    Step 3: Call Tool
    Call the `generate_sdk` tool with the prepared arguments:
    - `language`: (The normalized identifier from Step 1)
    - `specSource`: (The resolved URL or Content from Step 2)
    - `className`: (As provided)
    - `namespaceName`: (As provided)
    - `additionalOptions`: (As provided)

    Step 4: Report Results
    - If a download link is returned, display it clearly.
    - If a local path is returned, provide the path.
    ```

1. Confirm the result.