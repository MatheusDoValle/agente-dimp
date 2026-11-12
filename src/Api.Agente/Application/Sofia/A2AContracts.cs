using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Agente.Application.Sofia;

public sealed record A2ARpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] A2AMessageParams? Params = null
)
{
    public const string ExpectedVersion = "2.0";
    public const string SendMethod = "message/send";
    public const string TasksGetMethod = "tasks/get";
    public const string TasksCancelMethod = "tasks/cancel";
}

public sealed record A2AMessageParams(
    [property: JsonPropertyName("message")] A2AMessage? Message = null,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("taskId")] string? TaskId = null
);

public sealed record A2AMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("parts")] IReadOnlyList<A2APart> Parts
)
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
}

public sealed record A2APart(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text")] string Text
);

public sealed record A2ARpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("result")] A2ATaskResult Result
)
{
    public static A2ARpcResponse Create(string rpcId, string taskId, string contextId, string artifactName, string artifactContent)
    {
        return new A2ARpcResponse(
            JsonRpc: "2.0",
            Id: rpcId,
            Result: new A2ATaskResult(
                Id: taskId,
                ContextId: contextId,
                Status: new A2ATaskStatus("completed"),
                Artifacts: new[]
                {
                    new A2AArtifact(
                        ArtifactId: $"art-{Guid.NewGuid():N}"[..12],
                        Name: artifactName,
                        Parts: new[]
                        {
                            new A2APart(Kind: "text", Text: artifactContent)
                        }
                    )
                }
            )
        );
    }
}

public sealed record A2ATaskResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("contextId")] string ContextId,
    [property: JsonPropertyName("status")] A2ATaskStatus Status,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<A2AArtifact> Artifacts
)
{
    [JsonPropertyName("kind")]
    public string Kind => "task";
}

public sealed record A2ATaskStatus(
    [property: JsonPropertyName("state")] string State
);

public sealed record A2AArtifact(
    [property: JsonPropertyName("artifactId")] string ArtifactId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parts")] IReadOnlyList<A2APart> Parts
);

public sealed record A2ARpcErrorResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("error")] A2ARpcError Error
)
{
    public static A2ARpcErrorResponse Create(string? rpcId, int code, string message) =>
        new("2.0", rpcId, new A2ARpcError(code, message));
}

public sealed record A2ARpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);
