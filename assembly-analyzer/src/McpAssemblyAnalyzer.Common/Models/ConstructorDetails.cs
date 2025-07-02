namespace McpAssemblyAnalyzer.Common.Models;

public class ConstructorDetails
{
    public string? Name { get; set; }
    public List<ParameterDetails> Parameters { get; set; } = [];
}
