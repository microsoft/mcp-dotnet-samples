using System.Text.Json.Serialization;

namespace McpSamples.AwesomeCopilot.HybridApp.Models;

public class CollectionItem
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public class Collection
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("items")]
    public List<CollectionItem>? Items { get; set; }

    [JsonPropertyName("display")]
    public Dictionary<string, object>? Display { get; set; }
}
