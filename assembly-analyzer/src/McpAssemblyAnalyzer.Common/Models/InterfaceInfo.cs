using System.Reflection;

namespace McpAssemblyAnalyzer.Common.Models;

public class InterfaceInfo
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public bool IsGeneric { get; set; }
    public string? BaseType { get; set; }
    public List<MethodInfo> Methods { get; set; } = [];
    public List<PropertyInfo> Properties { get; set; } = [];
}