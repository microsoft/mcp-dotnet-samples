#!/usr/bin/env pwsh

# postprovision hook: Inject Client ID into mcp.json after deployment

Write-Host "Injecting Client ID into mcp.json..." -ForegroundColor Cyan

# 1. Get AZURE_CLIENT_ID from azd env
$clientId = azd env get-value AZURE_CLIENT_ID

if ($null -eq $clientId -or $clientId -eq "") {
    Write-Host "ERROR: AZURE_CLIENT_ID not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Client ID found: $clientId" -ForegroundColor Green

# 2. Update mcp.http.remote-func.json
$funcJsonPath = ".\.vscode\mcp.http.remote-func.json"
if (Test-Path $funcJsonPath) {
    $funcJson = Get-Content $funcJsonPath -Raw | ConvertFrom-Json
    $funcJson.servers."onedrive-download".env.AZURE_CLIENT_ID = $clientId
    $funcJson | ConvertTo-Json -Depth 10 | Set-Content $funcJsonPath
    Write-Host "mcp.http.remote-func.json updated" -ForegroundColor Green
} else {
    Write-Host "WARNING: mcp.http.remote-func.json not found" -ForegroundColor Yellow
}

# 3. Update mcp.http.remote-apim.json
$apimJsonPath = ".\.vscode\mcp.http.remote-apim.json"
if (Test-Path $apimJsonPath) {
    $apimJson = Get-Content $apimJsonPath -Raw | ConvertFrom-Json
    $apimJson.servers."onedrive-download".env.AZURE_CLIENT_ID = $clientId
    $apimJson | ConvertTo-Json -Depth 10 | Set-Content $apimJsonPath
    Write-Host "mcp.http.remote-apim.json updated" -ForegroundColor Green
} else {
    Write-Host "WARNING: mcp.http.remote-apim.json not found" -ForegroundColor Yellow
}

Write-Host "postprovision completed successfully" -ForegroundColor Green
