using System.Reflection;

namespace McpAssemblyAnalyzer.Common.Models;

public class ClassInfo
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool IsGeneric { get; set; }
    public string? BaseType { get; set; }
    public List<InterfaceInfo> Interfaces { get; set; } = [];
    public List<ConstructorInfo> Constructors { get; set; } = [];
    public List<MethodInfo> Methods { get; set; } = [];
    public List<PropertyInfo> Properties { get; set; } = [];
    public List<FieldInfo> Fields { get; set; } = [];
}
