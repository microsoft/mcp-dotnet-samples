namespace McpSamples.AwesomeAzd.HybridApp.Models;

/// <summary>
/// Represents a terminal command and its working directory.
/// </summary>
public class AzdCommand
{
    /// <summary>
    /// The full command string to execute (e.g., azd init ...).
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The working directory in which the command should be executed.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
}