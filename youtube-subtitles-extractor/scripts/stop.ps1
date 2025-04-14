# # Find PowerShell processes running the Azurite container
# $processes = Get-Process -Name powershell | Where-Object { $_.CommandLine -like "*azure-storage/azurite*" }

# # Stop each matching process
# $processes | ForEach-Object {
#     Stop-Process -Id $_.Id -Force
#     Write-Host "Stopped Azurite process with ID $($_.Id)"
# }

# # Additionally ensure the Docker container is stopped
# docker stop $(docker ps -q --filter name=azurite)
# docker rm azurite


docker stop azurite
docker rm azurite
