using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Agente.Application.Contracts;
using Xunit;

namespace Api.Agente.UnitTests.AI;

public class AnalyticResponseTests
{
    private const string ValidJson = """
    {
      "summary": "Analise semantica realizada com sucesso.",
      "explanations": [
        {
          "findingIds": ["11111111-1111-1111-1111-111111111111"],
          "text": "O valor informado diverge do formato esperado pelo manual.",
          "evidenceIds": ["22222222-2222-2222-2222-222222222222"]
        }
      ],
      "indications": [
        {
          "text": "Possivel inconsistencia cadastral do participante.",
          "evidenceIds": ["22222222-2222-2222-2222-222222222222"]
        }
      ],
      "limitations": [
        "Consulta limitada aos primeiros 50 chunks."
      ],
      "requiresHumanReview": true
    }
    """;

    [Fact]
    public void ValidJson_DeserializesAndValidatesSuccessfully()
    {
        var content = ContractJson.Deserialize<AnalyticContent>(ValidJson);

        Assert.Equal("Analise semantica realizada com sucesso.", content.Summary);
        Assert.Single(content.Explanations);
        Assert.Equal("11111111-1111-1111-1111-111111111111", content.Explanations[0].FindingIds[0]);
        Assert.Single(content.Indications);
        Assert.Single(content.Limitations);
        Assert.True(content.RequiresHumanReview);
    }

    [Fact]
    public void AdditionalProperties_AreDisallowed_AtRootLevel()
    {
        var jsonWithExtra = """
        {
          "summary": "Analise com campo extra.",
          "explanations": [],
          "indications": [],
          "limitations": [],
          "requiresHumanReview": false,
          "inventedMetadata": "tentativa_de_injecao"
        }
        """;

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<AnalyticContent>(jsonWithExtra));
    }

    [Fact]
    public void AdditionalProperties_AreDisallowed_InExplanationItem()
    {
        var jsonWithExtra = """
        {
          "summary": "Analise com item extra.",
          "explanations": [
            {
              "findingIds": ["11111111-1111-1111-1111-111111111111"],
              "text": "Explicacao.",
              "evidenceIds": [],
              "severityOverride": "informativo"
            }
          ],
          "indications": [],
          "limitations": [],
          "requiresHumanReview": false
        }
        """;

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<AnalyticContent>(jsonWithExtra));
    }

    [Fact]
    public void AdditionalProperties_AreDisallowed_InIndicationItem()
    {
        var jsonWithExtra = """
        {
          "summary": "Analise com indicacao extra.",
          "explanations": [],
          "indications": [
            {
              "text": "Indicio.",
              "evidenceIds": [],
              "extra": 123
            }
          ],
          "limitations": [],
          "requiresHumanReview": false
        }
        """;

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<AnalyticContent>(jsonWithExtra));
    }

    [Theory]
    [InlineData("""{"explanations":[],"indications":[],"limitations":[],"requiresHumanReview":false}""")] // missing summary
    [InlineData("""{"summary":"S","indications":[],"limitations":[],"requiresHumanReview":false}""")] // missing explanations
    [InlineData("""{"summary":"S","explanations":[],"limitations":[],"requiresHumanReview":false}""")] // missing indications
    [InlineData("""{"summary":"S","explanations":[],"indications":[],"requiresHumanReview":false}""")] // missing limitations
    [InlineData("""{"summary":"S","explanations":[],"indications":[],"limitations":[]}""")] // missing requiresHumanReview
    public void MissingRequiredFields_AtRootLevel_ThrowsException(string invalidJson)
    {
        Assert.ThrowsAny<Exception>(() => ContractJson.Deserialize<AnalyticContent>(invalidJson));
    }

    [Fact]
    public void MissingRequiredFields_InExplanations_ThrowsException()
    {
        var missingFindingIds = """
        {
          "summary": "S",
          "explanations": [
            {
              "text": "Explicacao sem findingIds.",
              "evidenceIds": []
            }
          ],
          "indications": [],
          "limitations": [],
          "requiresHumanReview": false
        }
        """;

        Assert.ThrowsAny<Exception>(() => ContractJson.Deserialize<AnalyticContent>(missingFindingIds));
    }

    [Fact]
    public void TruncatedOrMalformedJson_ThrowsJsonException()
    {
        var truncatedJson = """
        {
          "summary": "Analise interrompida repentinamente...",
          "explanations": [
        """;

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<AnalyticContent>(truncatedJson));
    }

    [Fact]
    public void ModelRefusalText_ThrowsJsonException_InsteadOfCreatingInvalidContract()
    {
        var refusalText = "I cannot fulfill this request because it violates safety guidelines.";

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<AnalyticContent>(refusalText));
    }
}
