using System;

namespace McpSamples.PptTranslator.HybridApp.Models
{
    /// <summary>
    /// Represents the execution mode of the MCP server.
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>
        /// Running locally with STDIO communication.
        /// </summary>
        StdioLocal,

        /// <summary>
        /// Running locally with HTTP communication.
        /// </summary>
        HttpLocal,

        /// <summary>
        /// Running in a container with STDIO communication and volume mounts.
        /// </summary>
        StdioContainer,

        /// <summary>
        /// Running in a container with HTTP communication and volume mounts.
        /// </summary>
        HttpContainer,

        /// <summary>
        /// Running in Azure Container Apps with HTTP communication.
        /// </summary>
        HttpRemote
    }

    /// <summary>
    /// Provides utilities for detecting and working with execution modes.
    /// </summary>
    public static class ExecutionModeDetector
    {
        /// <summary>
        /// Detects the current execution mode based on environment variables.
        /// </summary>
        /// <returns>The detected execution mode.</returns>
        public static ExecutionMode DetectExecutionMode()
        {
            bool inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            bool isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_APP_NAME"));
            bool isHttp = Environment.GetEnvironmentVariable("MCP_HTTP_MODE") == "true";

            if (isAzure)
                return ExecutionMode.HttpRemote;

            if (inContainer && isHttp)
                return ExecutionMode.HttpContainer;

            if (inContainer)
                return ExecutionMode.StdioContainer;

            if (isHttp)
                return ExecutionMode.HttpLocal;

            return ExecutionMode.StdioLocal;
        }

        /// <summary>
        /// Gets the host mount path from environment variable (for container modes).
        /// This is the single folder on the host that is mounted to /files in the container.
        /// </summary>
        public static string? GetHostMountPath()
        {
            return Environment.GetEnvironmentVariable("HOST_MOUNT_PATH");
        }

        /// <summary>
        /// Checks if the current mode is a container mode.
        /// </summary>
        public static bool IsContainerMode(this ExecutionMode mode)
        {
            return mode == ExecutionMode.StdioContainer 
                || mode == ExecutionMode.HttpContainer 
                || mode == ExecutionMode.HttpRemote;
        }

        /// <summary>
        /// Checks if the current mode is a local mode.
        /// </summary>
        public static bool IsLocalMode(this ExecutionMode mode)
        {
            return mode == ExecutionMode.StdioLocal 
                || mode == ExecutionMode.HttpLocal;
        }
    }
}
