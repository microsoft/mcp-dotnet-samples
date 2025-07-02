namespace McpAssemblyAnalyzer.Common.Models;

public class AssemblyDetails
{
    public string? AssemblyName { get; set; }
    public string? Version { get; set; }
    public string? Location { get; set; }
    public string? FullName { get; set; }
    public DateTime? FileCreatedDate { get; set; }
    public DateTime? FileModifiedDate { get; set; }
    public List<ClassDetails> Classes { get; set; } = [];
    public List<EnumDetails> Enums { get; set; } = [];

    public string? Error { get; set; }
}
