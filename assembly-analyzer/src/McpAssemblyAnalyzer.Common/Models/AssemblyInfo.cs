namespace McpAssemblyAnalyzer.Common.Models;

public class AssemblyInfo
{
    public string? AssemblyName { get; set; }
    public string? Version { get; set; }
    public string? Location { get; set; }
    public string? FullName { get; set; }
    public DateTime? CompiledDate { get; set; }
    public DateTime? FileCreatedDate { get; set; }
    public DateTime? FileModifiedDate { get; set; }
    public List<ClassInfo> Classes { get; set; } = [];
    public List<EnumInfo> Enums { get; set; } = [];

    public string? Error { get; set; }
}
