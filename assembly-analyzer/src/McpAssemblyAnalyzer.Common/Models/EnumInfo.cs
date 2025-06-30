namespace McpAssemblyAnalyzer.Common.Models;

public class EnumInfo
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public List<KeyValuePair<int, string>> Values { get; set; } = [];
}