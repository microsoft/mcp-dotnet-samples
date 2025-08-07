# MCP Server: Awesome Copilot

This is an MCP server that retrieves GitHub Copilot customizations from the [awesome-copilot](https://github.com/github/awesome-copilot) repository.

## Install

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect?url=vscode%3Amcp%2Finstall%3F%7B%22name%22%3A%22awesome-copilot%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fawesome-copilot%3Alatest%22%5D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect?url=vscode-insiders%3Amcp%2Finstall%3F%7B%22name%22%3A%22awesome-copilot%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fawesome-copilot%3Alatest%22%5D%7D)

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## What's Included

Awesome Copilot MCP server includes:

| Building Block | Name                  | Description                                                           | Usage                                    |
|----------------|-----------------------|-----------------------------------------------------------------------|------------------------------------------|
| Tools          | `search_instructions` | Searches custom instructions based on keywords in their descriptions. | `#search_instructions`                   |
| Tools          | `load_instruction`    | Loads a custom instruction from the repository.                       | `#load_instruction`                      |
| Prompts        | `get_search_prompt`   | Get a prompt for searching copilot instructions.                      | `/mcp.awesome-copilot.get_search_prompt` |

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
    cd $REPOSITORY_ROOT/awesome-copilot
    dotnet run --project ./src/McpSamples.AwesomeCopilot.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.AwesomeCopilot.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5250`.

   With this parameter, you can run the MCP server like:

   ```bash
   dotnet run --project ./src/McpSamples.AwesomeCopilot.HybridApp -- --http
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.awesome-copilot -t awesome-copilot:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 awesome-copilot:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/awesome-copilot:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With this parameter, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 awesome-copilot:latest --http
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/awesome-copilot:latest --http
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/awesome-copilot
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
     azd env get-value AZURE_RESOURCE_MCP_AWESOME_COPILOT_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

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

   **For locally running MCP server (HTTP):**

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

   **For locally running MCP server in a container (STDIO):**

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

   **For locally running MCP server in a container (HTTP):**

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

   **For remotely running MCP server in a container (HTTP):**

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
1. Choose `awesome-copilot` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.AwesomeCopilot.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Use a prompt by typing `/mcp.awesome-copilot.get_search_prompt` and enter keywords to search. You'll get a prompt like:

    ```text
    Please search all the chatmodes, instructions and prompts that are related to the search keyword, `{keyword}`.

    Here's the process to follow:

    1. Use the `awesome-copilot` MCP server.
    1. Search all chatmodes, instructions, and prompts for the keyword provided.
    1. DO NOT load any chatmodes, instructions, or prompts from the MCP server until the user asks to do so.
    1. Scan local chatmodes, instructions, and prompts markdown files in `.github/chatmodes`, `.github/instructions`, and `.github/prompts` directories respectively.
    1. Compare existing chatmodes, instructions, and prompts with the search results.
    1. Provide a structured response in a table format that includes the already exists, mode (chatmodes, instructions or prompts), filename, title and description of each item found. Here's an example of the table format:

        | Exists | Mode         | Filename               | Title         | Description   |
        |--------|--------------|------------------------|---------------|---------------|
        | ✅    | chatmodes    | chatmode1.json         | ChatMode 1    | Description 1 |
        | ❌    | instructions | instruction1.json      | Instruction 1 | Description 1 |
        | ✅    | prompts      | prompt1.json           | Prompt 1      | Description 1 |

        ✅ indicates that the item already exists in this repository, while ❌ indicates that it does not.

    1. If any item doesn't exist in the repository, ask which item the user wants to save.
    1. If the user wants to save it, save the item in the appropriate directory (`.github/chatmodes`, `.github/instructions`, or `.github/prompts`) using the mode and filename, with NO modification.
    ```

1. Confirm the result.
