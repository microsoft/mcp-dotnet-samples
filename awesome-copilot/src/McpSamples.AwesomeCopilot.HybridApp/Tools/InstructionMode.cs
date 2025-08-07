using System.Text.Json.Serialization;

namespace McpSamples.AwesomeCopilot.HybridApp.Tools;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstructionMode
{
    [JsonStringEnumMemberName("undefined")]
    Undefined,

    [JsonStringEnumMemberName("chatmodes")]
    ChatModes,

    [JsonStringEnumMemberName("instructions")]
    Instructions,

    [JsonStringEnumMemberName("prompts")]
    Prompts
}