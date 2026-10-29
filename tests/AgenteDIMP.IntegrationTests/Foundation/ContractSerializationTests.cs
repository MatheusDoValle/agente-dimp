using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using AgentJson = Api.Agente.Application.Contracts.ContractJson;
using RagJson = Api.RAG.Application.Contracts.ContractJson;

namespace AgenteDIMP.IntegrationTests.Foundation;

public class ContractSerializationTests
{
    [Theory]
    [InlineData("agente", "Api.Agente.Application.Contracts")]
    [InlineData("rag", "Api.RAG.Application.Contracts")]
    public void DtosMatchRequiredWirePropertiesAndRejectUnknownFields(string service, string ns)
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(FoundationSupport.RepositoryRoot(),
            "specs", "001-validar-arquivo-dimp", "contracts", service + ".openapi.json")))!;
        var schemas = source["components"]!["schemas"]!.AsObject();
        var assembly = service == "agente" ? typeof(Api.Agente.Program).Assembly : typeof(Api.RAG.Program).Assembly;
        var options = service == "agente" ? AgentJson.Options : RagJson.Options;
        foreach (var schema in schemas)
        {
            var type = assembly.GetType(ns + "." + schema.Key, throwOnError: true)!;
            var sample = Sample(schema.Value!, schemas).AsObject();
            var result = JsonSerializer.Deserialize(sample.ToJsonString(), type, options)!;
            AgentJson.Validate(result);
            var roundTrip = JsonSerializer.SerializeToNode(result, type, options)!.AsObject();
            Assert.Equal(sample.Select(x => x.Key).Order(), roundTrip.Select(x => x.Key).Order());
            sample["unrecognizedOperationalField"] = "not-allowed";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(sample.ToJsonString(), type, options));
        }
    }

    [Fact]
    public void NullableRequiredFieldCannotBeOmitted()
    {
        var json = """{"code":"NOT_FOUND","message":"Indisponivel","correlationId":"86a6dfbd-0011-4f83-b88e-8cdcfc8d53db"}""";
        Assert.Throws<JsonException>(() => AgentJson.Deserialize<Api.Agente.Application.Contracts.Problem>(json));
        var withNull = json[..^1] + ",\"executionId\":null}";
        Assert.Null(AgentJson.Deserialize<Api.Agente.Application.Contracts.Problem>(withNull).ExecutionId);
    }

    [Theory]
    [InlineData("\"inventado\"")]
    [InlineData("1")]
    public void UnknownOrNumericEnumIsRejected(string status)
    {
        var json = "{\"executionId\":\"86a6dfbd-0011-4f83-b88e-8cdcfc8d53db\",\"status\":" + status
            + ",\"statusUrl\":\"/status\",\"reportUrl\":\"/relatorio\",\"acceptedAt\":\"2026-10-29T12:00:00Z\"}";
        Assert.Throws<JsonException>(() => AgentJson.Deserialize<Api.Agente.Application.Contracts.Accepted>(json));
    }

    [Fact]
    public void CoverageRejectsNegativeCounters()
    {
        const string json = """{"catalogVersion":"test","applicable":-1,"passed":0,"failed":0,"notExecuted":0,"blockedBySourceConflict":0}""";
        Assert.Throws<ValidationException>(() => AgentJson.Deserialize<Api.Agente.Application.Contracts.Coverage>(json));
    }

    [Fact]
    public void RejectsZeroPageNumbers()
    {
        const string json = """{"id":"synthetic","sourceKind":"execucao","revisionSha256":null,"chunkId":null,"pages":[0],"locator":null,"text":"synthetic"}""";
        Assert.Throws<ValidationException>(() => AgentJson.Deserialize<Api.Agente.Application.Contracts.Evidence>(json));
    }

    [Fact]
    public void DoesNotAcceptRelativeSourceUri()
    {
        const string json = """{"documentId":"86a6dfbd-0011-4f83-b88e-8cdcfc8d53db","revisionSha256":"synthetic","layoutVersion":"10","sourceUrl":"relative/manual","pageCount":1,"retrievedAt":"2026-10-29T12:00:00Z","embeddingProfileId":"fake","extractionProfileId":"fake","chunkProfileId":"fake"}""";
        Assert.Throws<ValidationException>(() => RagJson.Deserialize<Api.RAG.Application.Contracts.Revision>(json));
    }

    private static JsonNode Sample(JsonNode schema, JsonObject schemas)
    {
        if (schema["$ref"] is { } reference) return Sample(schemas[reference.GetValue<string>().Split('/')[^1]]!, schemas);
        if (schema["anyOf"] is JsonArray alternatives)
            return alternatives.Any(x => x?["type"]?.ToString() == "null") ? null! : Sample(alternatives[0]!, schemas);
        if (schema["type"] is JsonArray types && types.Any(x => x?.ToString() == "null")) return null!;
        if (schema["const"] is { } constant) return constant.DeepClone();
        if (schema["enum"] is JsonArray values) return values[0]!.DeepClone();
        var type = schema["type"]?.ToString();
        if (type == "object")
        {
            var result = new JsonObject();
            foreach (var property in schema["properties"]!.AsObject())
                result[property.Key] = Sample(property.Value!, schemas);
            return result;
        }
        if (type == "array") return new JsonArray(Sample(schema["items"]!, schemas));
        if (type == "integer") return JsonValue.Create(Math.Max(1, schema["minimum"]?.GetValue<long>() ?? 0))!;
        if (type == "boolean") return JsonValue.Create(false)!;
        if (schema["format"]?.ToString() == "uuid") return JsonValue.Create(Guid.NewGuid().ToString())!;
        if (schema["format"]?.ToString() == "date-time") return JsonValue.Create("2026-10-29T12:00:00Z")!;
        if (schema["format"]?.ToString() == "uri") return JsonValue.Create("https://example.invalid/manual")!;
        if (schema["pattern"] is not null) return JsonValue.Create("1.25")!;
        return JsonValue.Create("synthetic")!;
    }
}
