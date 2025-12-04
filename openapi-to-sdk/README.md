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
- [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/install?tabs=bash)

## What's Included

OpenAPI to SDK MCP server includes:

| Building Block | Name           | Description                                                                                             | Usage                 |
|----------------|----------------|---------------------------------------------------------------------------------------------------------|-----------------------|
| Tools          | `generate_sdk` | Generates a client SDK from an OpenAPI specification (URL or raw content) and returns a download link.  | `#generate_sdk`       |
| Prompts        | `generate_sdk_prompt` | A structured prompt that guides the LLM to generate an SDK, handling language normalization and inputs. | `/mcp.openapi-to-sdk.generate_sdk_prompt` |

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
    docker run -i --rm -p 8080:8080 -v "$REPOSITORY_ROOT/openapi-to-sdk/workspace:/app/workspace" -e HOST_ROOT_PATH="$REPOSITORY_ROOT" openapi-to-sdk:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 -v "$REPOSITORY_ROOT/openapi-to-sdk/workspace:/app/workspace" -e HOST_ROOT_PATH="$REPOSITORY_ROOT" ghcr.io/microsoft/mcp-dotnet-samples/openapi-to-sdk:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With this parameter, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 -v "$REPOSITORY_ROOT/openapi-to-sdk/workspace:/app/workspace"  -e HOST_ROOT_PATH="$REPOSITORY_ROOT" openapi-to-sdk:latest --http
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 -v "$REPOSITORY_ROOT/openapi-to-sdk/workspace:/app/workspace"  -e HOST_ROOT_PATH="$REPOSITORY_ROOT" ghcr.io/microsoft/mcp-dotnet-samples/openapi-to-sdk:latest --http
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

        Your task is to generate a client SDK based on the following inputs:
        - OpenAPI Source: `{specSource}`
        - Target Language: `{language}`
        - Configuration:
            - Class Name: {clientClassName}
            - Namespace: {namespaceName}
            - Additional Options: {additionalOptions}

        ---
        ### Execution Rules (Follow Strictly)

        1. **Smart Language Normalization**:
           The `generate_sdk` tool ONLY accepts the following language identifiers:
           [ CSharp, Java, TypeScript, PHP, Python, Go, Ruby, Dart, HTTP ]

           You MUST intelligently map the user's input to one of these valid identifiers.
           
           - **Handle Aliases & Variations**:
             - "C#", "c#", ".NET", "dotnet", "chsarp" (typo) -> Use CSharp
             - "TS", "Ts", "ts", "node", "typoscript" (typo) -> Use TypeScript
             - "Golang", "Goo" (typo) -> Use Go
             - "py", "pyton" (typo), "python3" -> Use Python
             - "jav", "Jave" (typo) -> Use Java
           
           - **Auto-Correction**:
             - If the user makes a minor typo or uses a common abbreviation, automatically correct it to the nearest valid identifier from the list above.

           - **Validation**:
             - If the input refers to a completely unsupported language (e.g., "Rust", "C++", "Assembly"), STOP and politely inform the user that it is not currently supported by Kiota.

        2. **Handle Output Path**:
           - The `generate_sdk` tool manages the output path internally to create a ZIP file.
           - NEVER pass `-o` or `--output` in the `additionalOptions` argument, even if the user asks to save it to a specific location (e.g., "Generate to D:/Work").
           - Instead, follow this workflow:
             1. Call `generate_sdk` WITHOUT the output path option.
             2. Once the tool returns the ZIP file path (or download link), tell the user: "I have generated the SDK. Would you like me to move/extract it to [User's Requested Path]?"
             3. If the user agrees, use your filesystem tools to move the file.

        3. **Call the Tool**:
           Use the `generate_sdk` tool with the normalized language and filtered options (excluding -o).
           
        4. **Report**:
           Provide the download link or file path returned by the tool.
    ```

1. Confirm the result.