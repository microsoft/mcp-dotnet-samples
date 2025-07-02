namespace McpAssemblyAnalyzer.Common.Models;

public class ParameterDetails
{
    public string? Name { get; set; }
    public string? ParameterType { get; set; }
    public bool IsOptional { get; set; }
    public object? DefaultValue { get; set; }
}