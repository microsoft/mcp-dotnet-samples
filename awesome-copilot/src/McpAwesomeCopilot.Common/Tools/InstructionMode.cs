using System.Text.Json.Serialization;

namespace McpAwesomeCopilot.Common.Tools;

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