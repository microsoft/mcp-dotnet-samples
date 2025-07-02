namespace McpAssemblyAnalyzer.Common.Models;

public class MethodDetails
{
    public string? Name { get; set; }
    public string? ReturnType { get; set; }
    public List<ParameterDetails> Parameters { get; set; } = [];
}
