#!/usr/bin/env dotnet

#:package YamlDotNet@*
#:property PublishAot=false

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

const string FilenameKey = "filename";
const string DescriptionKey = "description";

var baseDir = Directory.GetCurrentDirectory();
var schemaPath = Path.Combine(baseDir, "frontmatter-schema.json");
var sourceBase = Path.Combine(baseDir, "src", "awesome-copilot");
var outputPath = Path.Combine(baseDir, "src", "McpSamples.AwesomeCopilot.HybridApp", "metadata.json");

// Read the frontmatter schema to discover categories and file patterns
var schema = JsonSerializer.Deserialize<FrontmatterSchema>(File.ReadAllText(schemaPath))!;
var categories = BuildCategoryConfigs(schema);

// YAML deserializer
var deserializer = new DeserializerBuilder().Build();

// JSON serializer options (2-space indentation to match original output)
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

// Process each schema-defined category
var metadata = new Dictionary<string, List<Dictionary<string, object?>>>();
foreach (var category in categories)
{
    metadata[category.Name] = ProcessCategory(category);
}

// Write output
File.WriteAllText(outputPath, JsonSerializer.Serialize(metadata, jsonOptions));

// Report results
foreach (var cat in categories)
{
    Console.WriteLine($"Extracted frontmatter from {metadata[cat.Name].Count} {cat.Name} files");
}
Console.WriteLine($"Metadata written to {outputPath}");

// Validate required fields
var hasErrors = false;
foreach (var cat in categories)
{
    hasErrors |= ValidateItems(metadata[cat.Name], cat.Name);
}

if (hasErrors)
{
    Console.Error.WriteLine("Some files are missing required fields. Please check the output above.");
    return 1;
}

Console.WriteLine("All files have required fields. Metadata extraction completed successfully.");
return 0;

// ===== Local functions =====

List<CategoryConfig> BuildCategoryConfigs(FrontmatterSchema schema)
{
    var configs = new List<CategoryConfig>();
    if (schema.Properties is null || schema.Definitions is null)
    {
        return configs;
    }

    foreach (var (categoryName, prop) in schema.Properties)
    {
        // Resolve definition name from $ref (e.g., "#/definitions/agent" → "agent")
        var refPath = prop.Items?.Ref;
        if (refPath is null)
        {
            continue;
        }

        var defName = refPath.Split('/').Last();
        if (!schema.Definitions.TryGetValue(defName, out var definition))
        {
            continue;
        }

        // Extract file extension from the name property's regex pattern
        var namePattern = definition.Properties?.GetValueOrDefault("name")?.Pattern;
        string fileExtension;
        bool isSubdirScan = false;

        if (namePattern is not null)
        {
            // Clean the regex pattern to extract the literal file suffix
            // e.g., "^[a-zA-Z0-9._-]+\\.agent\\.md$" → ".agent.md"
            // e.g., "^README\\.md$" → "README.md"
            var cleaned = namePattern.TrimStart('^').TrimEnd('$');
            cleaned = Regex.Replace(cleaned, @"\[[^\]]*\][\+\*\?]?", "");
            cleaned = cleaned.Replace("\\.", ".");

            fileExtension = cleaned;
            isSubdirScan = !cleaned.StartsWith('.');
        }
        else
        {
            // No pattern defined — use convention: .{definitionName}.md
            fileExtension = $".{defName}.md";
        }

        // Collect allowed property names from the schema definition
        var allowedKeys = definition.Properties?.Keys.ToHashSet(StringComparer.Ordinal)
                         ?? new HashSet<string>(StringComparer.Ordinal);

        configs.Add(new CategoryConfig(categoryName, categoryName, fileExtension, isSubdirScan, allowedKeys));
    }

    return configs;
}

List<Dictionary<string, object?>> ProcessCategory(CategoryConfig config)
{
    var dirPath = Path.Combine(sourceBase, config.DirName);
    var results = new List<Dictionary<string, object?>>();

    if (!Directory.Exists(dirPath))
    {
        return results;
    }

    IEnumerable<string> files;
    if (config.IsSubdirScan)
    {
        // Scan subdirectories for a fixed filename (e.g., hooks/*/README.md)
        files = Directory.GetDirectories(dirPath)
                         .Select(d => Path.Combine(d, config.FileExtension))
                         .Where(File.Exists)
                         .OrderBy(f => f, StringComparer.Ordinal);
    }
    else
    {
        files = Directory.GetFiles(dirPath, $"*{config.FileExtension}")
                         .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);
    }

    foreach (var filePath in files)
    {
        var filename = config.IsSubdirScan
            ? $"{Path.GetFileName(Path.GetDirectoryName(filePath)!)}/{config.FileExtension}"
            : Path.GetFileName(filePath);

        var frontmatter = ExtractFrontmatter(filePath);
        if (frontmatter is not null)
        {
            // Build result with filename first, then only schema-defined fields
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [FilenameKey] = filename,
            };

            foreach (var (key, value) in frontmatter)
            {
                if (config.AllowedKeys.Contains(key))
                {
                    result[key] = value;
                }
            }

            // Ensure description is present
            if (!result.ContainsKey(DescriptionKey) ||
                string.IsNullOrEmpty(result[DescriptionKey]?.ToString()))
            {
                Console.Error.WriteLine($"Warning: No description found in {filename}, adding placeholder");
                result[DescriptionKey] = "No description provided";
            }

            results.Add(result);
        }
        else
        {
            Console.Error.WriteLine($"Warning: No frontmatter found in {filename}, skipping");
        }
    }

    return results;
}

Dictionary<string, object?>? ExtractFrontmatter(string filePath)
{
    var content = File.ReadAllText(filePath);

    // Remove BOM if present
    if (content.Length > 0 && content[0] == '\uFEFF')
    {
        content = content[1..];
    }

    if (!content.StartsWith("---"))
    {
        return null;
    }

    var lines = content.Split('\n');
    var frontmatterEnd = -1;

    for (var i = 1; i < lines.Length; i++)
    {
        if (lines[i].Trim() == "---")
        {
            frontmatterEnd = i;
            break;
        }
    }

    if (frontmatterEnd == -1)
    {
        return null;
    }

    var frontmatterContent = string.Join('\n', lines.Skip(1).Take(frontmatterEnd - 1));

    try
    {
        var yaml = deserializer.Deserialize<object>(frontmatterContent);
        if (yaml is Dictionary<object, object> dict)
        {
            return ConvertYamlDict(dict);
        }

        return null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error parsing frontmatter in {filePath}: {ex.Message}");
        return null;
    }
}

Dictionary<string, object?> ConvertYamlDict(Dictionary<object, object> yamlDict)
{
    var result = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var (key, value) in yamlDict)
    {
        var keyStr = key.ToString()!;
        result[keyStr] = ConvertYamlValue(keyStr, value);
    }

    return result;
}

object? ConvertYamlValue(string key, object? value)
{
    return value switch
    {
        null => null,
        Dictionary<object, object> dict => ConvertYamlDict(dict),
        List<object> list => list.Select(item => ConvertYamlValue("", item)).ToList(),
        _ => value,
    };
}

bool ValidateItems(List<Dictionary<string, object?>> items, string typeName)
{
    var hasErrors = false;
    foreach (var item in items)
    {
        var filename = item.GetValueOrDefault(FilenameKey)?.ToString();
        var description = item.GetValueOrDefault(DescriptionKey)?.ToString();

        if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(description))
        {
            Console.Error.WriteLine(
                $"Error: {typeName} missing required fields: {filename ?? "unknown"}");
            hasErrors = true;
        }
    }

    return hasErrors;
}

// ===== Schema model records =====

record FrontmatterSchema(
    [property: JsonPropertyName("properties")] Dictionary<string, SchemaCategory>? Properties,
    [property: JsonPropertyName("definitions")] Dictionary<string, TypeDefinition>? Definitions
);

record SchemaCategory(
    [property: JsonPropertyName("items")] SchemaRef? Items
);

record SchemaRef(
    [property: JsonPropertyName("$ref")] string? Ref
);

record TypeDefinition(
    [property: JsonPropertyName("properties")] Dictionary<string, PropertyDefinition>? Properties
);

record PropertyDefinition(
    [property: JsonPropertyName("pattern")] string? Pattern
);

record CategoryConfig(string Name, string DirName, string FileExtension, bool IsSubdirScan, HashSet<string> AllowedKeys);
