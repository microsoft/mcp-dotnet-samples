# PPT Translator MCP Server

Translates PowerPoint presentations to different languages using OpenAI API and ShapeCrawler. This MCP server has been redesigned with a modernized architecture for improved performance and maintainability.

## Features

- **5 Execution Modes**: stdio.local, http.local, stdio.container, http.container, http.remote
- **Multi-language Support**: Translate to any language (ko, en, ja, etc.)
- **Multiple File Format Support**: Enhanced to support various document formats
- **Preserves Formatting**: Maintains original PPT structure and styling
- **OpenAI Integration**: Uses GPT models for high-quality translation
- **Integrated Prompt System**: Built-in MCP prompts for enhanced user guidance
- **Streamlined Architecture**: Refactored services for better performance

## Prerequisites

- .NET 9.0 SDK
- OpenAI API Key
- Docker (for container modes)
- Azure subscription (for http.remote mode)

## File Structure (Container/Azure modes)

When running in container or Azure modes, files are organized in subfolders:

```
HOST_MOUNT_PATH/
├── input/     # Input PPT files (.pptx)
├── output/    # Translated PPT files (.pptx)
└── tmp/       # Temporary processing files (.json)
```

This structure keeps files organized and separates input, output, and temporary files.

## Installation

### 1. stdio.local (Local STDIO)

```bash
cd ppt-translator/src/McpSamples.PptTranslator.HybridApp
dotnet run
```

**MCP Config**: `.vscode/mcp.stdio.local.json`

### 2. http.local (Local HTTP)

```bash
cd ppt-translator/src/McpSamples.PptTranslator.HybridApp
dotnet run -- --http
```

Access at: `http://localhost:5280`

**MCP Config**: `.vscode/mcp.http.local.json`

### 3. stdio.container (Docker STDIO)

```bash
# Build image
docker buildx build --platform linux/amd64 -t ppt-translator:latest -f Dockerfile.ppt-translator .

# Run container
docker run -i --rm \
  -e OPENAI_API_KEY=$OPENAI_API_KEY \
  -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
  -v /Users/yourname/ppt-files:/files \
  ppt-translator:latest
```

**MCP Config**: `.vscode/mcp.stdio.container.json`

### 4. http.container (Docker HTTP)

```bash
# Build image (same as above)
docker buildx build --platform linux/amd64 -t ppt-translator:latest -f Dockerfile.ppt-translator .

# Run container
docker run -i --rm \
  -p 8080:8080 \
  -e OPENAI_API_KEY=$OPENAI_API_KEY \
  -e HOST_MOUNT_PATH=/Users/yourname/ppt-files \
  -v /Users/yourname/ppt-files:/files \
  ppt-translator:latest \
  -- --http
```

Access at: `http://localhost:8080`

**MCP Config**: `.vscode/mcp.http.container.json`

### 5. http.remote (Azure Container Apps)

#### Deploy to Azure

```bash
cd ppt-translator

# Login to Azure
azd auth login

# Set OpenAI API Key
azd env set OPENAI_API_KEY "your-openai-api-key"
# 확인
azd env get-values

# Deploy
azd up
```

#### Get FQDN

```bash
azd env get-value AZURE_RESOURCE_PPT_TRANSLATOR_FQDN
```

Example output: `ppt-translator.victoriousocean-12345678.koreacentral.azurecontainerapps.io`

#### Configure MCP

Update `.vscode/mcp.http.remote.json` with your FQDN when prompted.

**MCP Config**: `.vscode/mcp.http.remote.json`

## Usage

### File Upload (http.remote only)

Upload PPT file to Azure File Share:

```bash
curl -F "file=@sample.pptx" https://{YOUR_FQDN}/upload
```

Response:
```json
{
  "id": "abc123_sample.pptx",
  "path": "abc123_sample.pptx"
}
```

### Usage via MCP Prompts

The server now includes built-in MCP prompts for enhanced user experience:

1. **Translation Guidance**: Structured prompts help users understand translation workflow
2. **Error Handling**: Automated error handling with detailed instructions
3. **Multi-format Support**: Enhanced prompts for various file types

Use GitHub Copilot with MCP:

```
Translate sample.pptx to Japanese
```

The modernized tool will:
1. Automatically detect file format and execution mode
2. Extract text from PPT using optimized processing
3. Translate using OpenAI with enhanced prompt system
4. Rebuild PPT with translated text
5. Provide download link with improved error handling

### Download Translated File

#### Local modes:
```bash
# File path returned in success message

# 🍎/🐧 macOS & Linux
cp "/path/to/output/sample_ja.pptx" "/destination/"

# 💻 Windows Command Prompt
copy "\path\to\output\sample_ja.pptx" "\destination\\"

# 💻 Windows PowerShell
Copy-Item "/path/to/output/sample_ja.pptx" -Destination "/destination/"
```

#### Container modes:
```bash
# HTTP download
curl -o "sample_ja.pptx" http://localhost:8080/download/sample_ja.pptx
```

#### Azure mode:
```bash
# HTTP download (example FQDN)
curl -o "sample_ja.pptx" https://ppt-translator.nicepebble-4f85ae45.southeastasia.azurecontainerapps.io/download/sample_ja.pptx
```

## Tool Reference

### translate_ppt_file

Translates a PowerPoint file to target language.

**Parameters:**
- `filePath` (required): Path to PPT file
  - Local: absolute path (e.g., `/Users/name/file.pptx`)
  - Container: filename only (e.g., `sample.pptx`)
  - Azure: filename only (e.g., `abc123_sample.pptx`)
- `targetLang` (required): Target language code (e.g., `ko`, `en`, `ja`)
- `outputPath` (optional): Custom output directory (local modes only)

**Example:**
```json
{
  "filePath": "sample.pptx",
  "targetLang": "ko"
}
```

## Architecture

### Execution Mode Detection

The server automatically detects execution mode based on environment variables:

- `DOTNET_RUNNING_IN_CONTAINER`: Container vs Local
- `CONTAINER_APP_NAME`: Azure Container Apps
- `MCP_HTTP_MODE`: HTTP vs STDIO
- `HOST_MOUNT_PATH`: Host mount path for containers

### Modernized Service Architecture

This version features a completely refactored service architecture:

- **Streamlined Services**: Removed legacy `TempFileResolver` and consolidated file processing
- **Enhanced Tool System**: Improved translation tools with better error handling
- **Integrated Prompt System**: Added `PptTranslatorPrompt` for better MCP integration
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

Ensure file is in mounted input directory:

```bash
# For container modes

# 🍎/🐧 macOS & Linux
cp "/path/to/file.pptx" "/path/to/mount/folder/input/file.pptx"

# 💻 Windows Command Prompt
copy "\path\to\file.pptx" "\path\to\mount\folder\input\file.pptx"

# 💻 Windows PowerShell
Copy-Item "/path/to/file.pptx" -Destination "/path/to/mount/folder/input/file.pptx"
```

### Azure Upload Fails

Check FQDN and File Share permissions:

```bash
# Get Storage Account name
azd env get-value AZURE_STORAGE_ACCOUNT_NAME

# Verify File Share exists
az storage share show \
  --account-name <storage-account-name> \
  --name ppt-files
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