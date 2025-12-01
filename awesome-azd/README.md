# MCP Server: Awesome Azd

This is an MCP server that provides search functionality for Awesome AZD templates from the [awesome-azd](https://github.com/Azure/awesome-azd) repository.

## Install


## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## What's Included

Awesome Copilot MCP server includes:

| Building Block | Name                           | Description                                                           | Usage                                         |
|----------------|--------------------------------|-----------------------------------------------------------------------|-----------------------------------------------|
| Tools          | `get_templates`                | Searches templates based on keywords in their descriptions.           | `#get_templates`                              |
| Tools          | `make_command`                 | Generates the azd init command to be executed.                        | `#make_command`                               |
| Prompts        | `get_template_search_prompt`   | Get a prompt for searching azd templates.                             | `/mcp.awesome-azd.get_template_search_prompt` |

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
    cd $REPOSITORY_ROOT/awesome-azd
    dotnet run --project ./src/McpSamples.AwesomeAzd.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.AwesomeAzd.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5201`.

   With this parameter, you can run the MCP server like:

   ```bash
   dotnet run --project ./src/McpSamples.AwesomeAzd.HybridApp -- --http
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.awesome-azd -t awesome-azd:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 awesome-azd:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.

   With this parameter, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 awesome-azd:latest --http
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/awesome-azd
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
     azd env get-value AZURE_RESOURCE_MCP_AWESOME_AZD_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/awesome-azd/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `awesome-azd` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.AwesomeAzd.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Use a prompt by typing `/mcp.awesome-azd.get_template_search_prompt` and enter keywords to search. You'll get a prompt like:

    ```text
    Please search all Azure templates related to the keyword `{keyword}`.

    ------------------------------------------------------------
    1. Search templates
    ------------------------------------------------------------
    - Use the `awesome-azd` MCP server.
    - Call the `get_templates` tool with the keyword `{keyword}`.
    - Return the results as a table with the columns: Title | Description | Source.
    - Only include templates whose metadata matches the keyword.

    ------------------------------------------------------------
    2. Collect user input for executi   on and Backend execution
    ------------------------------------------------------------
    After displaying the search results, ask the user to provide:

    1. **Selected Template Source** (must match the Source column; e.g., GitHub URL)
    2. **Working Directory** (optional; leave empty for default/null)
    3. **Environment Name** (optional; leave empty for default/null)

    - If the user provides a **Selected Template Source**, you may proceed to execution immediately.

    **IMPORTANT** - If the user does not provide a working directory or an environment name, keep asking repeatedly until they provide one.

    Example input:

    https://github.com/Azure-Samples/azure-search-openai-demo-csharp ; C:\Users\<UserProfile>\Projects ; AzureEnv


    - Call the `make_command` MCP tool with a JSON object like this:

        "srcPath": "Selected Template Source",
        "workingDirectory",
        "envName"

    - The tool will return an `AzdCommand` object.
    - Use `run in terminal` to execute it:
    
    - mkdir "<AzdCommand.WorkingDirectory>" -Force; cd "<AzdCommand.WorkingDirectory>"; <AzdCommand.Command> (Window)
    - mkdir -p "<AzdCommand.WorkingDirectory>" && cd "<AzdCommand.WorkingDirectory>" && <AzdCommand.Command> (MacOS, Linux)

    - Once the command is generated, execute it immediately without further confirmation.
    - The command will initialize the template in the specified directory and return success status, output, and any errors.
    
    ```

1. After the search results appear, enter the following three values when prompted:

   - **Selected Template Source**: The template URL shown in the search result (Source column)  
   - **Working Directory**: The directory where the azd template will be initialized  
   - **Environment Name**: The name of the Azure Developer CLI environment to create or use

   Once you provide these values and confirm, the MCP server will automatically generate
   the appropriate `azd init` command and execute it in your terminal.


1. Confirm the result.
