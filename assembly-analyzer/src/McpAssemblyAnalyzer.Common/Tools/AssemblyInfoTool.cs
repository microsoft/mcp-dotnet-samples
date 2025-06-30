using System.ComponentModel;
using System.Reflection;

using McpAssemblyAnalyzer.Common.Helpers;
using McpAssemblyAnalyzer.Common.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

namespace McpAssemblyAnalyzer.Common.Tools;

public interface IAssemblyInfoTool
{
    Task<AssemblyInfo> GetAssemblyInfoAsync(string assemblyPath);
}

[McpServerToolType]
public class AssemblyInfoTool(ILogger<AssemblyInfoTool> logger) : IAssemblyInfoTool
{
    [McpServerTool(Name = "get_assembly_info", Title = "Get Assembly Information")]
    [Description("Gets information about the specified assembly.")]
    public async Task<AssemblyInfo> GetAssemblyInfoAsync(
        [Description("The path to the assembly file")] string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            logger.LogError("Assembly file not found: {AssemblyPath}", assemblyPath);

            return new AssemblyInfo { Error = $"Assembly file not found: {assemblyPath}" };
        }

        try
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);

            var assemblyInfo = new AssemblyInfo
            {
                AssemblyName = assembly.GetName().Name,
                Version = assembly.GetName().Version?.ToString(),
                Location = assembly.Location,
                FullName = assembly.FullName,
                CompiledDate = GetCompiledDate(assemblyPath) ?? File.GetLastWriteTime(assemblyPath),
                FileCreatedDate = File.GetCreationTime(assemblyPath),
                FileModifiedDate = File.GetLastWriteTime(assemblyPath),
                Classes = ClassHelper.GetClassInformation(assembly),
                Enums = EnumHelper.GetEnumInformation(assembly)
            };

            return await Task.FromResult(assemblyInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing assembly: {AssemblyPath}", assemblyPath);

            return new AssemblyInfo { Error = ex.Message };
        }
    }

    private static DateTime? GetCompiledDate(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Read DOS header
            stream.Seek(0x3C, SeekOrigin.Begin);
            int peHeaderOffset = reader.ReadInt32();

            // Read PE header
            stream.Seek(peHeaderOffset, SeekOrigin.Begin);
            uint peSignature = reader.ReadUInt32();

            if (peSignature != 0x00004550) // "PE\0\0"
                return null;

            // Skip machine type (2 bytes)
            stream.Seek(2, SeekOrigin.Current);

            // Skip number of sections (2 bytes)
            stream.Seek(2, SeekOrigin.Current);

            // Read timestamp (4 bytes)
            uint timestamp = reader.ReadUInt32();

            // Convert from Unix timestamp to DateTime
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(timestamp);
        }
        catch
        {
            // If we can't read the PE header, fall back to file modification time
            return File.GetLastWriteTime(filePath);
        }
    }
}
