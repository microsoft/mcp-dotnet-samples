# Provision script to acquire Personal 365 refresh token before deployment
# This script compiles and runs the C# provisioning utility using dotnet

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$CsFile = Join-Path $ScriptDir "ProvisionRefreshToken.cs"
$CsprojFile = Join-Path $ScriptDir "ProvisionRefreshToken.csproj"
$PublishDir = Join-Path $ScriptDir "bin\Release\net9.0\publish"
$ExePath = Join-Path $PublishDir "ProvisionRefreshToken.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Personal 365 Refresh Token Provisioning" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check if C# file exists
if (-not (Test-Path $CsFile)) {
    Write-Host "`n✗ ProvisionRefreshToken.cs not found at $CsFile" -ForegroundColor Red
    exit 1
}

# Create minimal .csproj if not exists
if (-not (Test-Path $CsprojFile)) {
    Write-Host "`nCreating project file..." -ForegroundColor Yellow
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"@
    $csprojContent | Out-File -FilePath $CsprojFile -Encoding UTF8
}

# Compile and publish
Write-Host "`nBuilding C# utility..." -ForegroundColor Yellow
Push-Location $ScriptDir
dotnet publish -c Release -o bin/Release/net9.0/publish 2>&1
Pop-Location

if (-not (Test-Path $ExePath)) {
    Write-Host "✗ Failed to build C# code" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green

# Run the utility
Write-Host ""
& $ExePath

$exitCode = $LASTEXITCODE

# .env.local에서 토큰을 읽어서 환경 변수로 설정
if ($exitCode -eq 0 -and (Test-Path $CsprojFile)) {
    Write-Host "`n로딩중: 프로비저닝된 토큰을 환경 변수로 설정 중..."

    $envLocalPath = Join-Path $ScriptDir ".env.local"
    if (Test-Path $envLocalPath) {
        $envContent = Get-Content $envLocalPath | Where-Object { $_.StartsWith("PERSONAL_365_REFRESH_TOKEN=") }
        if ($envContent) {
            $tokenLine = $envContent -split "=" | Select-Object -Last 1
            [Environment]::SetEnvironmentVariable("PERSONAL_365_REFRESH_TOKEN", $tokenLine, "User")
            Write-Host "✓ 환경 변수 설정 완료: PERSONAL_365_REFRESH_TOKEN"
        }
    }
}

exit $exitCode
