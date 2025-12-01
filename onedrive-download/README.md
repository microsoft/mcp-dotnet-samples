# MCP Server: OneDrive Download

This is an MCP server that downloads files from OneDrive and provides secure access through temporary SAS tokens.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)

## What's Included

- OneDrive Download MCP server with:
  - User token passthrough authentication with Microsoft Entra ID
  - Secure file download with time-bound SAS tokens
  - Azure File Share integration for file storage

  | Building Block | Name                          | Description                                              | Usage                               |
  |----------------|-------------------------------|----------------------------------------------------------|------------------------------------|
  | Tools          | `download_file_from_onedrive_url` | Download a file from OneDrive URL and return SAS link. | `#download_file_from_onedrive_url` |

## Getting Started

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

### Running MCP server on Azure

1. **IMPORTANT** Check whether you have the necessary permissions:
   - Your Azure account must have the `Microsoft.Authorization/roleAssignments/write` permission, such as [Role Based Access Control Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#role-based-access-control-administrator), [User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#user-access-administrator), or [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) at the subscription level.
   - Your Azure account must also have the `Microsoft.Resources/deployments/write` permission at the subscription level.

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/onedrive-download
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

   - Azure Functions Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN
     ```

   - Azure API Management FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_GATEWAY_FQDN
     ```

## Connect MCP server to an MCP host/client

### VS Code + Agent Mode + Remote MCP server

1. **For remotely running MCP server as Function app (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/onedrive-download/.vscode/mcp.http.remote-func.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/onedrive-download/.vscode/mcp.http.remote-func.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. **For remotely running MCP server via API Management (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/onedrive-download/.vscode/mcp.http.remote-apim.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/onedrive-download/.vscode/mcp.http.remote-apim.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `onedrive-download` then click `Start Server`.
1. When prompted, enter the following values:
   - The FQDN of Azure Functions Apps (from `AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN`).
   - The FQDN of Azure API Management (from `AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_GATEWAY_FQDN`).
1. Enter a OneDrive sharing URL and the tool will download the file and return a secure download link.
