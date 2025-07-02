using System.ComponentModel;
using System.Reflection;

using McpAssemblyAnalyzer.Common.Helpers;
using McpAssemblyAnalyzer.Common.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

namespace McpAssemblyAnalyzer.Common.Tools;

public interface IAssemblyDetailsTool
{
    Task<AssemblyDetails> GetAssemblyDetailsAsync(string assemblyPath);
}

[McpServerToolType]
public class AssemblyDetailsTool(ILogger<AssemblyDetailsTool> logger) : IAssemblyDetailsTool
{
    [McpServerTool(Name = "get_assembly_info", Title = "Get Assembly Information")]
    [Description("Gets information about the specified assembly.")]
    public async Task<AssemblyDetails> GetAssemblyDetailsAsync(
        [Description("The path to the assembly file")] string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            logger.LogError("Assembly file not found: {AssemblyPath}", assemblyPath);

            return new AssemblyDetails { Error = $"Assembly file not found: {assemblyPath}" };
        }

        try
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);

            var assemblyInfo = new AssemblyDetails
            {
                AssemblyName = assembly.GetName().Name,
                Version = assembly.GetName().Version?.ToString(),
                Location = assembly.Location,
                FullName = assembly.FullName,
                FileCreatedDate = File.GetCreationTime(assemblyPath),
                FileModifiedDate = File.GetLastWriteTime(assemblyPath),
                Classes = ClassHelper.GetClassDetails(assembly),
                Enums = EnumHelper.GetEnumDetails(assembly)
            };

            return await Task.FromResult(assemblyInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing assembly: {AssemblyPath}", assemblyPath);

            return new AssemblyDetails { Error = ex.Message };
        }
    }
}
