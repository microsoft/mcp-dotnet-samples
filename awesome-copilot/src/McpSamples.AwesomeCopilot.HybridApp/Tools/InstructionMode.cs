using System.Text.Json.Serialization;

namespace McpSamples.AwesomeCopilot.HybridApp.Tools;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstructionMode
{
    [JsonStringEnumMemberName("undefined")]
    Undefined,

    [JsonStringEnumMemberName("agents")]
    Agents,

    [JsonStringEnumMemberName("hooks")]
    Hooks,

    [JsonStringEnumMemberName("instructions")]
    Instructions,

    [JsonStringEnumMemberName("prompts")]
    Prompts,

    [JsonStringEnumMemberName("skills")]
    Skills,

    [JsonStringEnumMemberName("workflows")]
    Workflows
}