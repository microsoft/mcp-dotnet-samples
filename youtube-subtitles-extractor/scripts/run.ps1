# Run docker container in the background
# Start-Process powershell `
#     -ArgumentList "-Command docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite" `
#     -WindowStyle Hidden

docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite

$REPOSITORY_ROOT = git rev-parse --show-toplevel

cd $REPOSITORY_ROOT/src/McpYouTubeSubtitlesExtractor.FunctionApp
func start
