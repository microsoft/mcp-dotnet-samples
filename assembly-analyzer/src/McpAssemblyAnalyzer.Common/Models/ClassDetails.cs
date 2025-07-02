namespace McpAssemblyAnalyzer.Common.Models;

public class ClassDetails
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool IsGeneric { get; set; }
    public string? BaseType { get; set; }
    public List<InterfaceDetails> Interfaces { get; set; } = [];
    public List<ConstructorDetails> Constructors { get; set; } = [];
    public List<MethodDetails> Methods { get; set; } = [];
    public List<PropertyDetails> Properties { get; set; } = [];
    public List<FieldDetails> Fields { get; set; } = [];
}
