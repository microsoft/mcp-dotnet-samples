using System.ComponentModel;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ModelContextProtocol.Server;

namespace McpSamples.OnedriveDownload.HybridApp.Tools;

/// <summary>
/// Represents the result of a OneDrive file download operation.
/// </summary>
public class OneDriveDownloadResult
{
    /// <summary>
    /// Gets or sets the base64 encoded content of the downloaded file.
    /// </summary>
    public string? FileContentBase64 { get; set; }

    /// <summary>
    /// Gets or sets the name of the downloaded file.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the error message if the download failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// This provides interfaces for the OneDrive tool.
/// </summary>
public interface IOneDriveTool
{
    /// <summary>
    /// Downloads a file from OneDrive given its full path.
    /// </summary>
    /// <param name="filePath">The full path to the file in OneDrive (e.g., '/Documents/MyFile.docx').</param>
    /// <returns>Returns <see cref="OneDriveDownloadResult"/> instance.</returns>
    Task<OneDriveDownloadResult> DownloadFileAsync(string filePath);
}

/// <summary>
/// This represents the tool entity for OneDrive file operations.
/// </summary>
[McpServerToolType]
public class OneDriveTool(GraphServiceClient graphServiceClient, ILogger<OneDriveTool> logger) : IOneDriveTool
{
    /// <inheritdoc />
    [McpServerTool(Name = "download_onedrive_file", Title = "Download OneDrive File")]
    [Description("Downloads a file from OneDrive given its full path.")]
    public async Task<OneDriveDownloadResult> DownloadFileAsync(
        [Description("The full path to the file in OneDrive (e.g., '/Documents/MyFile.docx')")] string filePath)
    {
        var result = new OneDriveDownloadResult();

        // Get the user's Drive ID
        var myDrive = await graphServiceClient.Me.Drive.GetAsync();
        if (myDrive == null || string.IsNullOrEmpty(myDrive.Id))
        {
            result.ErrorMessage = "Could not retrieve user's OneDrive information.";
            return result;
        }
        string driveId = myDrive.Id;

        // Find the file by path
        var driveItem = await graphServiceClient.Drives[driveId].Root.ItemWithPath(filePath).GetAsync();

        if (driveItem == null || driveItem.File == null)
        {
            result.ErrorMessage = $"File not found at path: {filePath}";
            return result;
        }

        // Download the file content
        using var contentStream = await graphServiceClient.Drives[driveId].Items[driveItem.Id].Content.GetAsync();
        if (contentStream == null)
        {
            result.ErrorMessage = $"Could not retrieve content for file: {filePath}";
            return result;
        }

        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream);
        byte[] fileBytes = memoryStream.ToArray();

        result.FileContentBase64 = Convert.ToBase64String(fileBytes);
        result.FileName = driveItem.Name;

        logger.LogInformation("File '{FileName}' downloaded successfully from OneDrive.", driveItem.Name);

        return result;
    }
}
