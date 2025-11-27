# Setup-Auth.ps1 - Automatic OAuth app registration script

Write-Host "[INFO] Starting authentication setup automation..." -ForegroundColor Cyan

# 1. Load environment variables
$AppName = $env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME
$ResourceGroup = "rg-$env:AZURE_ENV_NAME"
$FunctionAppUrl = $env:AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN
$TenantId = $env:AZURE_TENANT_ID
$SubscriptionId = $env:AZURE_SUBSCRIPTION_ID

Write-Host "[DEBUG] AppName: $AppName" -ForegroundColor Gray
Write-Host "[DEBUG] ResourceGroup: $ResourceGroup" -ForegroundColor Gray
Write-Host "[DEBUG] FunctionAppUrl: $FunctionAppUrl" -ForegroundColor Gray

if (-not $AppName -or -not $ResourceGroup -or -not $FunctionAppUrl) {
    Write-Host "[ERROR] Required environment variables are missing" -ForegroundColor Red
    Write-Host "[ERROR] AppName: $AppName" -ForegroundColor Red
    Write-Host "[ERROR] ResourceGroup: $ResourceGroup" -ForegroundColor Red
    Write-Host "[ERROR] FunctionAppUrl: $FunctionAppUrl" -ForegroundColor Red
    exit 1
}

Write-Host "[INFO] App Name: $AppName" -ForegroundColor Gray
Write-Host "[INFO] Resource Group: $ResourceGroup" -ForegroundColor Gray
Write-Host "[INFO] Function App URL: $FunctionAppUrl" -ForegroundColor Gray

# 2. Create Redirect URI
$RedirectUri = "https://$FunctionAppUrl/auth/callback"
Write-Host "[INFO] Redirect URI: $RedirectUri" -ForegroundColor Gray

# 3. Create App Registration
Write-Host "[INFO] Creating App Registration..." -ForegroundColor Yellow
$AppRegName = "mcp-oauth-$env:AZURE_ENV_NAME-$(Get-Random -Maximum 9999)"

try {
    $AppOutput = az ad app create `
        --display-name $AppRegName `
        --public-client-redirect-uris $RedirectUri `
        --sign-in-audience "AzureADandPersonalMicrosoftAccount" `
        --query "{ appId: appId }" -o json

    if (-not $AppOutput) {
        Write-Host "[ERROR] Failed to create App Registration" -ForegroundColor Red
        exit 1
    }

    $AppId = ($AppOutput | ConvertFrom-Json).appId
    Write-Host "[SUCCESS] App Registration created (ID: $AppId)" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Exception while creating App Registration: $_" -ForegroundColor Red
    exit 1
}

# 4. Add API permissions (Microsoft Graph)
Write-Host "[INFO] Adding API permissions..." -ForegroundColor Yellow
try {
    # Microsoft Graph API ID
    $GraphApiId = "00000003-0000-0000-c000-000000000000"

    # Delegated permissions
    # User.Read = 88d21fd4-8e52-4522-a9d3-8732c3a2f881
    # Files.Read = 5447fe39-cb82-4c18-9a2e-6271dcc5b4a7
    # Files.Read.All = df85f4d6-205c-4ac5-a5ea-6bf408dba38d
    # offline_access = 37f7f235-527c-4136-accd-4a02d197296e

    $Permissions = @(
        "88d21fd4-8e52-4522-a9d3-8732c3a2f881",  # User.Read
        "5447fe39-cb82-4c18-9a2e-6271dcc5b4a7",  # Files.Read
        "df85f4d6-205c-4ac5-a5ea-6bf408dba38d",  # Files.Read.All
        "37f7f235-527c-4136-accd-4a02d197296e"   # offline_access
    )

    foreach ($PermId in $Permissions) {
        az ad app permission add `
            --id $AppId `
            --api $GraphApiId `
            --api-permissions "$PermId=Scope" > $null 2>&1
    }

    Write-Host "[SUCCESS] API permissions added (User.Read, Files.Read, Files.Read.All, offline_access)" -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Exception while adding API permissions (continuing): $_" -ForegroundColor Yellow
}

# 5. Create Service Principal
Write-Host "[INFO] Creating Service Principal..." -ForegroundColor Yellow
try {
    az ad sp create --id $AppId > $null
    Write-Host "[SUCCESS] Service Principal created" -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Service Principal may already exist (ignoring)" -ForegroundColor Yellow
}

# 6. Output App ID for bicep (AZD will capture this)
Write-Host "[INFO] App Registration ID: $AppId" -ForegroundColor Cyan

# 7. Update Function App environment variables (FORCE UPDATE)
Write-Host "[INFO] Updating Function App settings..." -ForegroundColor Yellow
try {
    $UpdateOutput = az functionapp config appsettings set `
        --name $AppName `
        --resource-group $ResourceGroup `
        --settings "EntraId__ClientId=$AppId" 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] Function App settings updated with new App ID: $AppId" -ForegroundColor Green
    }
    else {
        Write-Host "[ERROR] Failed to update Function App settings" -ForegroundColor Red
        Write-Host "[ERROR] Output: $UpdateOutput" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "[ERROR] Exception updating Function App settings: $_" -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] Authentication setup complete!" -ForegroundColor Green
Write-Host "[INFO] App ID: $AppId" -ForegroundColor Cyan
Write-Host "[INFO] Users can now login at: https://$FunctionAppUrl/login" -ForegroundColor Cyan
