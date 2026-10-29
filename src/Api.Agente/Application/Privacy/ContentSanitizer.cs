using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Agente.Domain.Privacy;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.TemporaryStorage;

namespace Api.Agente.Application.Privacy;

public enum SensitiveValueKind { Cpf, Cnpj, Name, Other }
public sealed class SensitiveValue(SensitiveValueKind kind, string value)
{
    public SensitiveValueKind Kind { get; } = kind;
    public string Value { get; } = value;
    public override string ToString() => "[sensitive]";
}
public sealed class SensitiveContentException() : InvalidOperationException("Conteudo sensivel nao mascarado ou escopo de mascaramento ausente.");

public sealed class ContentSanitizer
{
    private static readonly Regex Cnpj = Pattern(@"(?<![\p{L}\p{N}])\d{2}[.\s]?\d{3}[.\s]?\d{3}[/\s]?\d{4}[-\s]?\d{2}(?![\p{L}\p{N}])");
    private static readonly Regex Cpf = Pattern(@"(?<![\p{L}\p{N}])\d{3}[.\s]?\d{3}[.\s]?\d{3}[-\s]?\d{2}(?![\p{L}\p{N}])");
    private static readonly Regex Token = Pattern(@"\[MASK_[A-Z]+_[a-f0-9]{32}\]");
    private readonly TemporaryExecutionFiles _files;
    private readonly TimeProvider _clock;

    public ContentSanitizer(TemporaryStorageOptions options, TimeProvider clock) { _files = new(options); _clock = clock; }

    public async Task<MaskedText> SanitizeAsync(Guid executionId, string text, IEnumerable<SensitiveValue>? knownValues = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(text);
        var gate = _files.Gate(executionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _files.EnsureWritable(executionId);
            var map = ReadMap(executionId, allowMissing: true);
            foreach (var known in knownValues ?? [])
            {
                if (!Enum.IsDefined(known.Kind) || string.IsNullOrWhiteSpace(known.Value)) throw new ArgumentException("Identificador conhecido invalido.");
                GetBinding(map, known.Kind, Canonical(known.Kind, known.Value));
            }

            var masked = text.Normalize(NormalizationForm.FormKC);
            foreach (var binding in map.Bindings.OrderByDescending(x => x.Canonical.Length))
                masked = BindingPattern(binding).Replace(masked, _ => binding.Token);
            masked = Cnpj.Replace(masked, match => GetBinding(map, SensitiveValueKind.Cnpj, Canonical(SensitiveValueKind.Cnpj, match.Value)).Token);
            masked = Cpf.Replace(masked, match => GetBinding(map, SensitiveValueKind.Cpf, Canonical(SensitiveValueKind.Cpf, match.Value)).Token);
            map.UpdatedAt = _clock.GetUtcNow().ToUniversalTime();
            await _files.WriteJsonAsync(_files.MapPath(executionId), map, cancellationToken);
            return new MaskedText(executionId, masked);
        }
        finally { gate.Release(); }
    }

    public void AssertSafe(Guid executionId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var gate = _files.Gate(executionId);
        gate.Wait();
        try
        {
            var map = ReadMap(executionId, allowMissing: false);
            var normalized = text.Normalize(NormalizationForm.FormKC);
            if (Cpf.IsMatch(normalized) || Cnpj.IsMatch(normalized)
                || map.Bindings.Any(binding => BindingPattern(binding).IsMatch(normalized))
                || Token.Matches(normalized).Any(match => !map.Bindings.Any(binding => binding.Token == match.Value)))
                throw new SensitiveContentException();
        }
        finally { gate.Release(); }
    }

    private MaskMap ReadMap(Guid executionId, bool allowMissing)
    {
        var path = _files.MapPath(executionId);
        _files.EnsureNoLinks(path);
        if (!File.Exists(path))
        {
            if (allowMissing) return new MaskMap { ExecutionId = executionId };
            throw new SensitiveContentException();
        }
        try
        {
            var map = JsonSerializer.Deserialize<MaskMap>(File.ReadAllText(path), TemporaryExecutionFiles.JsonOptions);
            if (map is null || map.ExecutionId != executionId || map.Version != 1
                || map.Bindings.Any(x => string.IsNullOrWhiteSpace(x.Canonical) || !Regex.IsMatch(x.Token, @"^\[MASK_[A-Z]+_[a-f0-9]{32}\]$")))
                throw new SensitiveContentException();
            return map;
        }
        catch (JsonException) { throw new SensitiveContentException(); }
    }

    private static MaskBinding GetBinding(MaskMap map, SensitiveValueKind kind, string canonical)
    {
        var found = map.Bindings.SingleOrDefault(x => x.Kind == kind && x.Canonical == canonical);
        if (found is not null) return found;
        var token = "[MASK_" + kind.ToString().ToUpperInvariant() + "_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) + "]";
        var binding = new MaskBinding { Kind = kind, Canonical = canonical, Token = token };
        map.Bindings.Add(binding);
        return binding;
    }

    private static string Canonical(SensitiveValueKind kind, string value)
    {
        var normalized = Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");
        if (kind is SensitiveValueKind.Cpf or SensitiveValueKind.Cnpj)
        {
            var compact = Regex.Replace(normalized, @"[.\s/-]", "").ToUpperInvariant();
            return compact.Length > 0 ? compact : normalized;
        }
        return kind == SensitiveValueKind.Name ? normalized.ToUpperInvariant() : normalized;
    }

    private static Regex BindingPattern(MaskBinding binding)
    {
        string expression;
        if (binding.Kind is SensitiveValueKind.Cpf or SensitiveValueKind.Cnpj)
            expression = string.Join(@"[.\s/-]*", binding.Canonical.Select(ch => Regex.Escape(ch.ToString())));
        else expression = string.Join(@"\s+", binding.Canonical.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
        return Pattern(@"(?<![\p{L}\p{N}])" + expression + @"(?![\p{L}\p{N}])",
            binding.Kind == SensitiveValueKind.Other ? RegexOptions.None : RegexOptions.IgnoreCase);
    }

    private static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private sealed class MaskMap
    {
        public int Version { get; set; } = 1;
        public Guid ExecutionId { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<MaskBinding> Bindings { get; set; } = [];
    }
    private sealed class MaskBinding
    {
        public SensitiveValueKind Kind { get; set; }
        public string Canonical { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
