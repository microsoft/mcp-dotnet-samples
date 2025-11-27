# Test Setup-Auth.ps1

$env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME = "func-onedrive-download-eaeffy232ns7u"
$env:AZURE_ENV_NAME = "test6"
$env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN = "func-onedrive-download-eaeffy232ns7u.azurewebsites.net"
$env:AZURE_TENANT_ID = "6decf767-7619-48b3-b0d5-a395f206d27f"

Write-Host "[INFO] Running Setup-Auth.ps1 with test environment variables..."
Write-Host "[DEBUG] AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME: $env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME"
Write-Host "[DEBUG] AZURE_ENV_NAME: $env:AZURE_ENV_NAME"
Write-Host "[DEBUG] AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN: $env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN"
Write-Host "[DEBUG] AZURE_TENANT_ID: $env:AZURE_TENANT_ID"

& ".\scripts\Setup-Auth.ps1"
