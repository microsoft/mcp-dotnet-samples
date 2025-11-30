#!/usr/bin/env pwsh

# postprovision hook: Inject Client ID into mcp.json after deployment

Write-Host "Injecting Client ID into mcp.json..." -ForegroundColor Cyan
Write-Host "Current directory: $(Get-Location)" -ForegroundColor Gray

# 1. Get AZURE_CLIENT_ID from azd env
$clientId = azd env get-value AZURE_CLIENT_ID

if ($null -eq $clientId -or $clientId -eq "") {
    Write-Host "ERROR: AZURE_CLIENT_ID not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Client ID found: $clientId" -ForegroundColor Green

# 2. Update mcp.http.remote-func.json
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$funcJsonPath = Join-Path $projectRoot ".vscode\mcp.http.remote-func.json"
Write-Host "mcp.http.remote-func.json path: $funcJsonPath" -ForegroundColor Gray

if (Test-Path $funcJsonPath) {
    $funcContent = Get-Content $funcJsonPath -Raw
    $funcContent = $funcContent -replace '"AZURE_CLIENT_ID":\s*"[^"]*"', ('"AZURE_CLIENT_ID": "' + $clientId + '"')
    Set-Content $funcJsonPath -Value $funcContent -Encoding UTF8
    Write-Host "mcp.http.remote-func.json updated" -ForegroundColor Green
} else {
    Write-Host "ERROR: mcp.http.remote-func.json not found at $funcJsonPath" -ForegroundColor Red
}

# 3. Update mcp.http.remote-apim.json
$apimJsonPath = Join-Path $projectRoot ".vscode\mcp.http.remote-apim.json"
Write-Host "mcp.http.remote-apim.json path: $apimJsonPath" -ForegroundColor Gray

if (Test-Path $apimJsonPath) {
    $apimContent = Get-Content $apimJsonPath -Raw
    $apimContent = $apimContent -replace '"AZURE_CLIENT_ID":\s*"[^"]*"', ('"AZURE_CLIENT_ID": "' + $clientId + '"')
    Set-Content $apimJsonPath -Value $apimContent -Encoding UTF8
    Write-Host "mcp.http.remote-apim.json updated" -ForegroundColor Green
} else {
    Write-Host "ERROR: mcp.http.remote-apim.json not found at $apimJsonPath" -ForegroundColor Red
}

Write-Host "postprovision completed successfully" -ForegroundColor Green
