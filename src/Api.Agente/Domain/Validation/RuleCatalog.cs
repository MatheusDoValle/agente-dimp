using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Api.Agente.Domain.Validation;

// This registry describes source rules; it is not a fiscal validator or a parser.
public sealed class RuleCatalog
{
    public const string SupportedVersion = "0.1.0";
    public const string SourceSha256 = "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A";
    public static ImmutableArray<string> RecordCodes { get; } =
        ["0000","0001","0002","0005","0006","0100","0105","0109","0200","0201","0209","0300","0600","0700","0990","1001","1100","1110","1115","1120","1200","1220","1500","1600","1990","9001","9900","9990","9999"];
    private readonly FrozenDictionary<string, RuleDefinition> _rules;
    private RuleCatalog(CatalogDocument document, string catalogSha256)
    {
        Version = document.CatalogVersion; RevisionSha256 = document.RevisionSha256;
        CatalogSha256 = catalogSha256;
        _rules = document.Rules.ToFrozenDictionary(r => r.Id, StringComparer.Ordinal);
        Rules = document.Rules;
    }
    public string Version { get; }
    public string RevisionSha256 { get; }
    public string CatalogSha256 { get; }
    public ImmutableArray<RuleDefinition> Rules { get; }
    public CatalogImplementationCoverage ImplementationCoverage => new(Rules.Length,
        Rules.Count(r => r.Implementation == "implemented"),
        Rules.Count(r => r.Implementation == "planned"), Rules.Count(r => r.Implementation == "blocked"));

    public static RuleCatalog Load(ReadOnlySpan<byte> catalogUtf8, string expectedCatalogSha256,
        string expectedVersion = SupportedVersion, string expectedRevisionSha256 = SourceSha256)
    {
        if (expectedVersion != SupportedVersion || expectedRevisionSha256 != SourceSha256
            || !HashPattern().IsMatch(expectedCatalogSha256)) throw Invalid("CATALOG_PIN_INVALID");
        var hash = Convert.ToHexString(SHA256.HashData(catalogUtf8));
        if (hash != expectedCatalogSha256) throw Invalid("CATALOG_HASH_MISMATCH");
        CatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(catalogUtf8, Options)
                ?? throw Invalid("CATALOG_EMPTY");
        }
        catch (JsonException) { throw Invalid("CATALOG_JSON_INVALID"); }
        if (document.CatalogVersion != expectedVersion || document.RevisionSha256 != expectedRevisionSha256
            || document.LayoutVersion != "10" || document.SourceDocumentId != "S01"
            || document.Rules.IsDefaultOrEmpty) throw Invalid("CATALOG_IDENTITY_INVALID");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in document.Rules)
        {
            if (rule is null || !Valid(rule, document) || !ids.Add(rule.Id)) throw Invalid("CATALOG_RULE_INVALID");
        }
        if (document.Rules.Any(r => r.Dependencies.Any(id => !ids.Contains(id)))
            || RecordCodes.Any(code => !document.Rules.Any(r => r.Record == code)))
            throw Invalid("CATALOG_LINK_INVALID");
        // A directed dependency cycle is an invalid catalog, regardless of ordering.
        var byId = document.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visited.Contains(id)) return true;
            if (!visiting.Add(id)) return false;
            if (byId[id].Dependencies.Any(dep => !Visit(dep))) return false;
            visiting.Remove(id); visited.Add(id); return true;
        }
        if (ids.Any(id => !Visit(id))) throw Invalid("CATALOG_DEPENDENCY_CYCLE");
        return new RuleCatalog(document, hash);
    }

    public RuleDefinition Get(string ruleId) => _rules.TryGetValue(ruleId, out var rule)
        ? rule : throw new KeyNotFoundException("CATALOG_RULE_NOT_FOUND");

    public void BindToExecution(Execution execution, Guid configurationSnapshotId)
    {
        ArgumentNullException.ThrowIfNull(execution);
        execution.SetSource("10", RevisionSha256, Version, configurationSnapshotId);
    }

    // Does not accept an applicability override: unavailable software cannot turn
    // into not_applicable to improve execution coverage. Actual validators follow.
    public RuleEvaluation CreateUnavailableEvaluation(Guid executionId, string ruleId, string occurrenceKey)
    {
        var rule = Get(ruleId);
        if (rule.Implementation == "implemented") throw Invalid("CATALOG_VALIDATOR_REQUIRED");
        return new RuleEvaluation(executionId, rule.Id, occurrenceKey, RuleApplicability.Undetermined,
            rule.Implementation == "blocked" ? RuleResult.BlockedBySourceConflict : RuleResult.NotExecuted);
    }

    private static bool Valid(RuleDefinition r, CatalogDocument d)
    {
        if (string.IsNullOrWhiteSpace(r.Id) || !IdPattern().IsMatch(r.Id)
            || r.CatalogVersion != d.CatalogVersion || r.RevisionSha256 != d.RevisionSha256
            || (r.Record is not null && !RecordCodes.Contains(r.Record)) || r.Field is < 1
            || (r.Field is not null && r.Record is null)
            || r.Source is null || r.Source.DocumentId != "S01" || r.Source.Page is < 1 or > 58
            || string.IsNullOrWhiteSpace(r.Source.Locator) || string.IsNullOrWhiteSpace(r.Source.Section)
            || string.IsNullOrWhiteSpace(r.Statement) || string.IsNullOrWhiteSpace(r.Excerpt)
            || string.IsNullOrWhiteSpace(r.Applicability) || string.IsNullOrWhiteSpace(r.ValidatorId)
            || string.IsNullOrWhiteSpace(r.SeverityReason)
            || !OneOf(r.Category,"physical","structure","field","conditional","relationship","aggregation")
            || !OneOf(r.Severity,"bloqueante","erro","aviso","informativo")
            || !OneOf(r.Interpretation,"resolved","ambiguous","conflicting")
            || !OneOf(r.Implementation,"planned","implemented","blocked")
            || !TextArray(r.Preconditions, true) || !TextArray(r.DataRequirements, true)
            || !TextArray(r.Dependencies, false) || !TextArray(r.ConflictIds, false)
            || r.ConflictIds.Any(id => !ConflictPattern().IsMatch(id)) || r.Tests.IsDefaultOrEmpty) return false;
        if ((r.Interpretation != "resolved") != (r.Implementation == "blocked")
            || (r.Interpretation == "conflicting" && r.ConflictIds.IsEmpty)
            || (r.Interpretation == "resolved" && !r.ConflictIds.IsEmpty && r.Resolution is null)) return false;
        if (r.Resolution is not null && (!Uri.TryCreate(r.Resolution.Url,UriKind.Absolute,out var url)
            || url.Scheme != "https" || !HashPattern().IsMatch(r.Resolution.RevisionSha256)
            || string.IsNullOrWhiteSpace(r.Resolution.Locator) || string.IsNullOrWhiteSpace(r.Resolution.ConsultedAt))) return false;
        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in r.Tests)
        {
            if (t is null || string.IsNullOrWhiteSpace(t.CaseId) || !caseIds.Add(t.CaseId)
                || !OneOf(t.Kind,"positive","negative","conditional","boundary")
                || !OneOf(t.Status,"planned","implemented","awaiting_official_resolution")
                || (t.ExpectedResult is not null && !OneOf(t.ExpectedResult,"passed","failed","not_applicable","not_executed","blocked_by_source_conflict"))
                || (t.Status == "implemented" && (string.IsNullOrWhiteSpace(t.Fixture) || t.ExpectedResult is null))) return false;
        }
        return r.Implementation != "implemented" || r.Tests.Any(t => t.Status == "implemented" && t.Kind == "positive")
            && r.Tests.Any(t => t.Status == "implemented" && t.Kind == "negative");
    }
    private static bool TextArray(ImmutableArray<string> values, bool required) => !values.IsDefault
        && (!required || !values.IsEmpty) && values.All(v => !string.IsNullOrWhiteSpace(v));
    private static bool OneOf(string value, params string[] allowed) => allowed.Contains(value,StringComparer.Ordinal);
    private static InvalidDataException Invalid(string code) => new(code);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, MaxDepth = 32
    };
    private static Regex IdPattern() => new("^DIMP10\\.[A-Za-z0-9._:-]+$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static Regex HashPattern() => new("^[A-F0-9]{64}$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static Regex ConflictPattern() => new("^SF-[0-9]{3}$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
}

public sealed record CatalogDocument(string CatalogVersion, string LayoutVersion, string RevisionSha256,
    string SourceDocumentId, ImmutableArray<RuleDefinition> Rules);
public sealed record CatalogSource(string DocumentId, int Page, string Section, string Locator);
public sealed record CatalogTestCase(string CaseId, string Kind, string Status, string? Fixture, string? ExpectedResult);
public sealed record OfficialResolution(string Url, string RevisionSha256, string Locator, string ConsultedAt);
public sealed record RuleDefinition(string Id, string CatalogVersion, string RevisionSha256, string? Record,
    int? Field, CatalogSource Source, string Statement, string Excerpt, string Category, string Applicability,
    ImmutableArray<string> Preconditions, ImmutableArray<string> DataRequirements, ImmutableArray<string> Dependencies,
    string Interpretation, ImmutableArray<string> ConflictIds, string Implementation, string ValidatorId,
    string Severity, string SeverityReason, ImmutableArray<CatalogTestCase> Tests, OfficialResolution? Resolution,
    JsonElement? Details);
public sealed record CatalogImplementationCoverage(int Total, int Implemented, int Planned, int Blocked);
