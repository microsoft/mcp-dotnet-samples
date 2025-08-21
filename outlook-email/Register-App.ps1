<#!
.SYNOPSIS
Registers an Entra ID (Azure AD) application for the Outlook Email MCP sample.

.DESCRIPTION
Creates a single-tenant SPA application with redirect URI http://localhost, requests
Microsoft Graph Mail.Send application permission, creates a service principal, grants
admin consent, and generates a client secret named 'default'. Outputs a JSON payload
containing identifiers and the secret value (only shown once). Optionally writes JSON
to a file via -OutFile.

.PARAMETER OutFile
Path to write JSON output instead of stdout.

.PARAMETER Help
Show this help.

.EXAMPLE
./Register-App.ps1

.EXAMPLE
./Register-App.ps1 -OutFile app-reg.json
#>
[CmdletBinding()] param(
    [Parameter(Position=0)]
    [string] $OutFile,
    [switch] $Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info { param([string]$Message) Write-Host "[register-app] $Message" }
function Write-Err  { param([string]$Message) Write-Host "[register-app][error] $Message" -ForegroundColor Red }

function Show-Usage {
@'
Usage: ./Register-App.ps1 [-OutFile <file>] [-Help]

Options:
    -OutFile <file>  Write JSON output to the specified file instead of stdout.
                     (The file will be overwritten if it exists.)
    -Help            Show this help.
'@ | Write-Host
}

if ($Help) { Show-Usage; return }

# Preconditions
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Err 'Azure CLI (az) is required.'
    exit 1
}

# Ensure login
try {
    az account show | Out-Null
} catch {
    Write-Info 'Logging into Azure...'
    az login | Out-Null
}

$tenantId = az account show --query tenantId -o tsv
Write-Info "Using tenant: $tenantId"

$graphAppId = '00000003-0000-0000-c000-000000000000'
$redirectUri = 'http://localhost'
$secretName  = 'default'
$maxAttempts = 5

# Resolve Mail.Send role id
$mailSendRoleId = az ad sp show --id $graphAppId --query "appRoles[?value=='Mail.Send' && contains(allowedMemberTypes, 'Application')].id" -o tsv
if (-not $mailSendRoleId) {
    Write-Err 'Could not resolve Mail.Send application permission ID from Microsoft Graph SP.'
    exit 1
}
Write-Info "Resolved Mail.Send app role ID: $mailSendRoleId"

# Generate unique app name
$attempt = 1
$appName = $null
while ($true) {
    $suffix = (Get-Random -Minimum 0 -Maximum 10000).ToString('0000')
    $candidate = "mcp-outlookemail-$suffix"
    $exists = az ad app list --display-name $candidate --query "[0].appId" -o tsv
    if (-not $exists) { $appName = $candidate; break }
    if ($attempt -ge $maxAttempts) { Write-Err "Failed to find unique app name after $maxAttempts attempts."; exit 1 }
    $attempt++
    Start-Sleep -Seconds 1
}
Write-Info "App display name will be: $appName"

# Create app registration
Write-Info 'Creating app registration...'
$appCreateJson = az ad app create --display-name $appName --sign-in-audience AzureADMyOrg -o json
$appCreate = $appCreateJson | ConvertFrom-Json
$appId = $appCreate.appId
$appObjectId = $appCreate.id
if (-not $appId -or -not $appObjectId) {
    Write-Err "Failed to parse app creation output. Raw: $appCreateJson"
    exit 1
}
Write-Info "Created app. Application (client) ID: $appId"
Write-Info "App object ID: $appObjectId"

# Apply SPA redirect URI
Write-Info 'Applying SPA redirect URI via update...'
az ad app update --id "$appId" --set "spa={'redirectUris': ['$redirectUri']}" | Out-Null

# Prepare requiredResourceAccess
Write-Info 'Updating app with required resource access (Mail.Send)...'
az ad app update --id $appId --required-resource-accesses "[{'resourceAppId':'$graphAppId','resourceAccess':[{'id':'$mailSendRoleId','type':'Role'}]}]"

# Create service principal (may already exist)
Write-Info 'Ensuring service principal exists...'
az ad sp create --id $appId | Out-Null
$spObjectId = az ad sp show --id $appId --query id -o tsv
Write-Info "Service principal object ID: $spObjectId"

# Grant admin consent
Write-Info "Granting admin consent..."
Start-Sleep -Seconds 30
az ad app permission admin-consent --id "$appId"

# Create client secret
Write-Info "Creating client secret named '$secretName'..."
$secretJson = az ad app credential reset --id $appId --append --display-name $secretName --years 1 -o json
$secretObj = $secretJson | ConvertFrom-Json
$clientSecretValue = $secretObj.password
if (-not $clientSecretValue) {
    Write-Err "Failed to obtain client secret value. Raw: $secretJson"
    exit 1
}

# Build output
$result = [ordered]@{
    displayName         = $appName
    tenantId            = $tenantId
    clientId            = $appId
    clientSecret        = $clientSecretValue
}
$json = $result | ConvertTo-Json -Depth 3

if ($OutFile) {
    Set-Content -Path $OutFile -Value $json -Encoding UTF8
    Write-Info "JSON output written to $OutFile (contains clientSecret). Secure this file (consider: chmod 600 $OutFile)."
} else {
    $json
    Write-Info 'JSON output emitted above. Store clientSecret securely; it cannot be retrieved later.'
}
