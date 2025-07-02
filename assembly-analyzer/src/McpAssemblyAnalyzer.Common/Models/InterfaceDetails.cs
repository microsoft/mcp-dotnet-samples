namespace McpAssemblyAnalyzer.Common.Models;

public class InterfaceDetails
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public bool IsGeneric { get; set; }
    public string? BaseType { get; set; }
    public List<MethodDetails> Methods { get; set; } = [];
    public List<PropertyDetails> Properties { get; set; } = [];
}