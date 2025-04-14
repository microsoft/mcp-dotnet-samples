#!/bin/bash

# # Find and kill processes running Azurite
# pids=$(ps aux | grep "azure-storage/azurite" | grep -v grep | awk '{print $2}')

# # Check if any processes were found
# if [ -n "$pids" ]; then
#     for pid in $pids; do
#         kill -9 $pid
#         echo "Stopped Azurite process with ID $pid"
#     done
# fi

# # Stop and remove the Docker container
# container_id=$(docker ps -q --filter name=azurite)
# if [ -n "$container_id" ]; then
#     docker stop $container_id
#     echo "Stopped Docker container $container_id"
# fi

# docker rm azurite 2>/dev/null || echo "No container named 'azurite' exists to remove"

docker stop azurite
docker rm azurite
