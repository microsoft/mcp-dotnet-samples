$body = @{
    context = @{ invocationId = 'vscode-call-1' }
    tool_invocations = @(
        @{
            tool_id = 'onedrive-download-tool'
            name = 'download_file_from_onedrive_url'
            arguments = @{ sharingUrl = 'https://1drv.ms/t/c/bd98f86d1ff003f7/ES0WYROmRO5LrYBpkKCQIQwBzhwWHc8q6UW70GoUWMIZIg?e=k9l6bX' }
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $uri = 'https://func-onedrive-download-34ugypgdcsh76.azurewebsites.net/mcp'
    Write-Host "Posting to $uri"
    $resp = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType 'application/json' -Verbose -ErrorAction Stop
    $json = $resp | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath '.\temp_mcp_response.json' -Encoding utf8
    Write-Host 'MCP_CALL_SUCCESS'
    Write-Host $json
} catch {
    Write-Host 'MCP_CALL_FAILED'
    Write-Host $_.Exception.Message
    if ($_.InvocationInfo) { Write-Host ($_.InvocationInfo.Line) }
    $_ | Format-List * -Force
}
