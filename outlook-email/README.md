# MCP Server: Outlook Email

This is an MCP server that sends an email through Outlook. It also covers **authentication** scenarios.

## Install

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect?url=vscode%3Amcp%2Finstall%3F%7B%22name%22%3A%22outlook-email%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Foutlook-email%3Alatest%22%5D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect?url=vscode-insiders%3Amcp%2Finstall%3F%7B%22name%22%3A%22outlook-email%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Foutlook-email%3Alatest%22%5D%7D)

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## What's Included

- Outlook Email MCP server runs in the following scenarios:
  - **OAuth authentication with Azure API Management** as a remote MCP server
  - **API key authentication with Azure Functions** as a remote MCP server
  - No authentication as a locally running MCP server
- Outlook Email MCP server includes:

  | Building Block | Name         | Description                  | Usage         |
  |----------------|--------------|------------------------------|---------------|
  | Tools          | `send_email` | Send an Email to recipients. | `#send_email` |

## Getting Started

- [Getting repository root](#getting-repository-root)
- [Registering an app on Entra ID](#registering-an-app-on-entra-id)
- [Running MCP server](#running-mcp-server)
  - [On a local machine](#on-a-local-machine)
  - [On a local machine as a Function app](#on-a-local-machine-as-a-function-app)
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

### Registering an app on Entra ID

> This section is for running the MCP server on your local machine or in a local container. If you deploy this MCP server to Azure, you can skip this section.

1. Run the following script.

    ```bash
    # bash/zsh
    cd $REPOSITORY_ROOT/outlook-email
    ./register-app.sh
    ```

    ```powershell
    # PowerShell
    cd $REPOSITORY_ROOT/outlook-email
    ./Register-App.ps1
    ```

1. Take notes for tenant ID, client ID and client secret values.

### Running MCP server

#### On a local machine

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/outlook-email
    dotnet run --project ./src/McpSamples.OutlookEmail.HybridApp
    ```

   > Make sure take note the absolute directory path of the `McpSamples.OutlookEmail.HybridApp` project.

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:5260`.
   - `--tenant-id`/`-t`: The tenant ID for sign-in.
   - `--client-id`/`-c`: The client ID for sign-in.
   - `--client-secret`/`-s`: The client secret for sign-in.

   With these parameters, you can run the MCP server like:

    ```bash
    dotnet run --project ./src/McpSamples.OutlookEmail.HybridApp -- --http -t "{{TENANT_ID}}" -c "{{CLIENT_ID}}" -s "{{CLIENT_SECRET}}"
    ```

   Instead of providing those tenant ID, client ID and client secret values through the command-line, they can be stored as the user secrets.

    ```bash
    dotnet user-secrets --project ./src/McpSamples.OutlookEmail.HybridApp set EntraId:TenantId "{{TENANT_ID}}"
    dotnet user-secrets --project ./src/McpSamples.OutlookEmail.HybridApp set EntraId:ClientId "{{CLIENT_ID}}"
    dotnet user-secrets --project ./src/McpSamples.OutlookEmail.HybridApp set EntraId:ClientSecret "{{CLIENT_SECRET}}"
    ```

#### On a local machine as a Function app

1. Rename `local.settings.sample.json` to `local.settings.json`.

    ```bash
    # bash/zsh
    cp $REPOSITORY_ROOT/outlook-email/src/McpSamples.OutlookEmail.HybridApp/local.settings.sample.json \
       $REPOSITORY_ROOT/outlook-email/src/McpSamples.OutlookEmail.HybridApp/local.settings.json
    ```

    ```powershell
    # PowerShell
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/src/McpSamples.OutlookEmail.HybridApp/local.settings.sample.json `
              -Destination $REPOSITORY_ROOT/outlook-email/src/McpSamples.OutlookEmail.HybridApp/local.settings.json -Force
    ```

1. Open `local.settings.json` and replace `{{TENANT_ID}}`, `{{CLIENT_ID}}` and `{{CLIENT_SECRET}}` with the tenant ID, client ID and client secret values respectively.

    ```jsonc
    {
      "IsEncrypted": false,
      "Values": {
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "AzureWebJobsFeatureFlags": "DisableDiagnosticEventLogging",
    
        "UseHttp": "true",
    
        "EntraId__TenantId": "{{TENANT_ID}}",
        "EntraId__ClientId": "{{CLIENT_ID}}",
        "EntraId__ClientSecret": "{{CLIENT_SECRET}}",
        "EntraId__UseManagedIdentity": false
      }
    }
    ```

1. Run the MCP server app.

    ```bash
    cd $REPOSITORY_ROOT/outlook-email/src/McpSamples.OutlookEmail.HybridApp
    func start
    ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile.outlook-email -t outlook-email:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 outlook-email:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/outlook-email:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.
   - `--tenant-id`/`-t`: The tenant ID for sign-in.
   - `--client-id`/`-c`: The client ID for sign-in.
   - `--client-secret`/`-s`: The client secret for sign-in.

   With these parameters, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 outlook-email:latest --http -t "{{TENANT_ID}}" -c "{{CLIENT_ID}}" -s "{{CLIENT_SECRET}}"
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/outlook-email:latest --http -t "{{TENANT_ID}}" -c "{{CLIENT_ID}}" -s "{{CLIENT_SECRET}}"
   ```

#### On Azure

1. **IMPORTANT** Check whether you have the necessary permissions:
   - Your Azure account must have the `Microsoft.Authorization/roleAssignments/write` permission, such as [Role Based Access Control Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#role-based-access-control-administrator), [User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#user-access-administrator), or [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) at the subscription level.
   - Your Azure account must also have the `Microsoft.Resources/deployments/write` permission at the subscription level.

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/outlook-email
    ```

1. Login to Azure.

    ```bash
    # Login with Azure Developer CLI
    azd auth login
    ```

<!-- 1. As a default, the MCP server will be deployed as an Azure Functions. If you want to deploy this MCP server to Azure Container Apps, add an environment variable, `USE_ACA`.

    ```bash
    azd env set USE_ACA true
    ``` -->

1. Deploy the MCP server app to Azure.

    ```bash
    azd up
    ```

   While provisioning and deploying, you'll be asked to provide subscription ID, location, environment name.

1. After the deployment is complete, get the information by running the following commands:

   - Azure Functions Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_FQDN
     ```

   <!-- - Azure Container Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_ACA_FQDN
     ``` -->

   - Azure API Management FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server as Function app (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.local-func.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.local-func.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server as Function app (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote-func.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote-func.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   <!-- **For remotely running MCP server as a container app (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ``` -->

   **For remotely running MCP server via API Management (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote-apim.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/outlook-email/.vscode/mcp.http.remote-apim.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `outlook-email` then click `Start Server`.
1. When prompted, enter the following values:
   - The absolute directory path of the `McpSamples.OutlookEmail.HybridApp` project.
   - The FQDN of Azure Container Apps.
   - The FQDN of Azure Functions Apps.
   - Tenant ID.
   - Client ID.
   - Client secret.
1. Enter prompt like:

    ```text
    Send an email to abc@contoso.com from xyz@contoso.com with the subject of "lorem ipsum" and body of "hello world".
    ```

1. Confirm the result.
