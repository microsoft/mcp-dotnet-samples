# Provision script to acquire Personal 365 refresh token before deployment
# This script uses Device Flow to get a refresh token for consumers (personal) tenant

param(
    [string]$EnvFile = ".env.local"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Personal 365 Refresh Token Provisioning" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Client ID from app registration (multitenant)
$ClientId = "44609b96-b8ed-48cd-ae81-75abbd52ffd1"
$TenantId = "consumers"
$Scope = "https://graph.microsoft.com/.default offline_access"

Write-Host "`nStep 1: Starting Device Flow authentication..." -ForegroundColor Yellow
Write-Host "This will open a browser for you to authenticate with your Personal Microsoft 365 account."

# Device flow request
$deviceFlowUrl = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/devicecode"
$deviceFlowBody = @{
    client_id = $ClientId
    scope = $Scope
}

try {
    $deviceFlowResponse = Invoke-RestMethod -Method Post -Uri $deviceFlowUrl -Body $deviceFlowBody -ErrorAction Stop
    $deviceCode = $deviceFlowResponse.device_code
    $userCode = $deviceFlowResponse.user_code
    $verificationUrl = $deviceFlowResponse.verification_uri
    $expiresIn = $deviceFlowResponse.expires_in
    $interval = $deviceFlowResponse.interval

    Write-Host "`n✓ Device flow initiated successfully" -ForegroundColor Green
    Write-Host "`nPlease complete the authentication:" -ForegroundColor Cyan
    Write-Host "1. Open this URL in your browser: $verificationUrl" -ForegroundColor White
    Write-Host "2. Enter this code: $userCode" -ForegroundColor White -BackgroundColor Black
    Write-Host "`nWaiting for authentication..." -ForegroundColor Yellow

    # Poll for token
    $tokenUrl = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
    $elapsedTime = 0
    $maxWaitTime = $expiresIn

    while ($elapsedTime -lt $maxWaitTime) {
        Start-Sleep -Seconds $interval
        $elapsedTime += $interval

        $tokenBody = @{
            client_id = $ClientId
            device_code = $deviceCode
            grant_type = "urn:ietf:params:oauth:grant-type:device_code"
        }

        try {
            $tokenResponse = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $tokenBody -ErrorAction Stop

            $accessToken = $tokenResponse.access_token
            $refreshToken = $tokenResponse.refresh_token
            $expiresIn = $tokenResponse.expires_in

            Write-Host "`n✓ Authentication successful!" -ForegroundColor Green
            Write-Host "✓ Access token acquired" -ForegroundColor Green
            Write-Host "✓ Refresh token acquired" -ForegroundColor Green

            # Save to .env file
            Write-Host "`nStep 2: Saving refresh token to $EnvFile..." -ForegroundColor Yellow

            # Create or update .env file
            if (Test-Path $EnvFile) {
                # Remove old PERSONAL_365_REFRESH_TOKEN if it exists
                $content = Get-Content $EnvFile | Where-Object { !$_.StartsWith("PERSONAL_365_REFRESH_TOKEN=") }
                $content | Set-Content $EnvFile
            }

            # Append refresh token
            Add-Content -Path $EnvFile -Value "PERSONAL_365_REFRESH_TOKEN=$refreshToken"

            Write-Host "✓ Refresh token saved to $EnvFile" -ForegroundColor Green
            Write-Host "`n========================================" -ForegroundColor Cyan
            Write-Host "✓ Provisioning completed successfully!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Cyan

            exit 0
        }
        catch {
            $errorResponse = $_.Exception.Response.Content.ReadAsStringAsync().Result
            $errorData = $errorResponse | ConvertFrom-Json -ErrorAction SilentlyContinue

            if ($errorData.error -eq "authorization_pending") {
                # Still waiting, this is expected
                continue
            }
            elseif ($errorData.error -eq "expired_token") {
                Write-Host "`n✗ Device code expired. Please try again." -ForegroundColor Red
                exit 1
            }
            else {
                Write-Host "`n✗ Error: $($errorData.error_description)" -ForegroundColor Red
                exit 1
            }
        }
    }

    Write-Host "`n✗ Authentication timeout. Please try again." -ForegroundColor Red
    exit 1
}
catch {
    Write-Host "`n✗ Failed to initiate device flow: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
