# MCP Server: PPT Translator

This is an MCP server that translates PowerPoint presentations to different languages using OpenAI API and ShapeCrawler. This MCP server has been redesigned with a modernized architecture for improved performance and maintainability.

## Install

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)]() [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)]()

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)
- [OpenAI API Key](https://platform.openai.com/api-keys)

## What's Included

PPT Translator MCP server includes:

| Building Block | Name                       | Description                         | Usage                       |
|----------------|----------------------------|-------------------------------------|-----------------------------|
| Tools          | `translate_ppt_file`       | Translates a PowerPoint file to target language | `#translate_ppt_file` |
| Prompts        | `ppt_translator`           | Structured workflow to guide translation process | `/mcp.ppt-translator.ppt_translator` |

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

1. Set your OpenAI API key.

    ```bash
    export OPENAI_API_KEY="your-openai-api-key"
    ```

    ```powershell
    $env:OPENAI_API_KEY="your-openai-api-key"
    ```

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/ppt-translator
    dotnet run --project ./src/McpSamples.PptTranslator.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.PptTranslator.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5280`.

   With these parameters, you can run the MCP server like:

   ```bash
   dotnet run --project ./src/McpSamples.PptTranslator.HybridApp -- --http
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.ppt-translator -t ppt-translator:latest .
    ```
    > Make sure take note the absolute directory path of the `ppt-translator` project.

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm \
      -e OPENAI_API_KEY=$OPENAI_API_KEY \
      -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
      -v /Users/yourname/ppt-files:/files \
      ppt-translator:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm \
      -e OPENAI_API_KEY=$OPENAI_API_KEY \
      -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
      -v /Users/yourname/ppt-files:/files \
      ghcr.io/microsoft/mcp-dotnet-samples/ppt-translator:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With these parameters, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 \
     -e OPENAI_API_KEY=$OPENAI_API_KEY \
     -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
     -v /Users/yourname/ppt-files:/files \
     ppt-translator:latest -- --http
   ```

   ```bash
   # use container image from the container registry
   docker run -it --rm -p 8080:8080 \
     -e OPENAI_API_KEY=$OPENAI_API_KEY \
     -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
     -v /Users/yourname/ppt-files:/files \
     ghcr.io/microsoft/mcp-dotnet-samples/ppt-translator:latest -- --http
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/ppt-translator
    ```

1. Login to Azure.

    ```bash
    # Login with Azure Developer CLI
    azd auth login
    ```

1. Set OpenAI API Key.

    ```bash
    azd env set OPENAI_API_KEY "your-openai-api-key"
    # 확인
    azd env get-values
    ```

1. Deploy the MCP server app to Azure.

    ```bash
    azd up
    ```

   While provisioning and deploying, you'll be asked to provide subscription ID, location, environment name.

1. After the deployment is complete, get the information by running the following commands:

   - Azure Container Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_PPT_TRANSLATOR_FQDN
     ```

     If you want to use Azure, you must upload the file to the running server first:

     ```bash
     curl -F "file=@sample.pptx" https://{YOUR_FQDN}/upload
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/ppt-translator/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `ppt-translator` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.PptTranslator.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Enter prompt like:

    ```text
    Translate /path/to/presentation.pptx to Korean
    ```

1. Confirm the result.

## Features

- **Multi-language Support**: Translate to any language (ko, en, ja, etc.)
- **Multiple File Format Support**: Enhanced to support various document formats
- **Preserves Formatting**: Maintains original PPT structure and styling
- **OpenAI Integration**: Uses GPT models for high-quality translation
- **Integrated Prompt System**: Built-in MCP prompts for enhanced user guidance
- **Streamlined Architecture**: Refactored services for better performance
- **5 Execution Modes**: stdio.local, http.local, stdio.container, http.container, http.remote

## Tool Reference

### translate_ppt_file

Translates a PowerPoint file to target language.

**Parameters:**
- `filePath` (required): Path to PPT file
  - Local: absolute path (e.g., `/Users/name/file.pptx`)
  - Container: filename only (e.g., `sample.pptx`)
  - Azure: filename only (e.g., `sample.pptx`)
- `targetLang` (required): Target language code (e.g., `ko`, `en`, `ja`)
- `outputPath` (optional): Custom output directory (local modes only)

## Architecture

### Modernized Service Architecture

This version features a completely refactored service architecture:

- **Streamlined Services**: Removed legacy components and consolidated file processing
- **Enhanced Tool System**: Improved translation tools with better error handling
- **Integrated Prompt System**: Added structured prompts for better MCP integration
- **Optimized File Processing**: Direct file handling for improved performance

### File Storage

| Mode | Input | Output | Download |
|------|-------|--------|----------|
| stdio.local | Absolute path | `wwwroot/generated` | File path |
| http.local | Absolute path | `wwwroot/generated` | `/download/{filename}` |
| stdio.container | `/files/{filename}` | `/files/{filename}` | File path (host) |
| http.container | `/files/{filename}` | `/files/{filename}` | `/download/{filename}` |
| http.remote | `/files/{filename}` | `/files/{filename}` | `/download/{filename}` |

### Azure Infrastructure

- **Container Registry**: Stores Docker image
- **Storage Account**: Azure File Share (`ppt-files`, 5TB)
- **Container Apps**: Runs server with auto-scaling (1-10 replicas)
- **Volume Mount**: `/files` mapped to Azure File Share
- **Monitoring**: Application Insights + Log Analytics

## Troubleshooting

### ARM64 Mac (M1/M2/M3)

SkiaSharp requires x86_64 platform:

```bash
docker buildx build --platform linux/amd64 -t ppt-translator:latest -f Dockerfile.ppt-translator .
```

### Container File Not Found

Ensure file is in mounted directory:

```bash
# 🍎/🐧 macOS & Linux
cp "/path/to/file.pptx" "/path/to/mount/folder/file.pptx"

# 💻 Windows Command Prompt
copy "\path\to\file.pptx" "\path\to\mount\folder\file.pptx"

# 💻 Windows PowerShell
Copy-Item "/path/to/file.pptx" -Destination "/path/to/mount/folder/file.pptx"
```

## Development

### Recent Changes (v2.0)

This version includes major architectural improvements:

- **Refactored Service Layer**: Removed legacy components (`TempFileResolver`, deprecated tools)
- **Enhanced Prompt System**: Added `PptTranslatorPrompt` for better MCP integration
- **Improved File Processing**: Streamlined file handling and processing pipeline
- **Multi-format Foundation**: Architecture prepared for supporting multiple document formats

### Build

```bash
dotnet build
```

### Test Locally

```bash
export OPENAI_API_KEY="your-key"
dotnet run -- --http
```

### Build Docker Image

```bash
docker buildx build --platform linux/amd64 -t ppt-translator:latest -f Dockerfile.ppt-translator .
```

### Deploy to Azure

```bash
azd up
```

## License

MIT
