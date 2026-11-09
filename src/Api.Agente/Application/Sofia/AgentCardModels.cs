using System.Text.Json.Serialization;

namespace Api.Agente.Application.Sofia;

public sealed record AgentCard(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("capabilities")] AgentCapabilities Capabilities,
    [property: JsonPropertyName("defaultInputModes")] IReadOnlyList<string> DefaultInputModes,
    [property: JsonPropertyName("defaultOutputModes")] IReadOnlyList<string> DefaultOutputModes,
    [property: JsonPropertyName("skills")] IReadOnlyList<AgentSkill> Skills
);

public sealed record AgentCapabilities(
    [property: JsonPropertyName("streaming")] bool Streaming,
    [property: JsonPropertyName("pushNotifications")] bool PushNotifications
);

public sealed record AgentSkill(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags
);
