using Api.Agente.Application.Privacy;
using Api.Agente.Infrastructure.Configuration;
using Xunit;

namespace Api.Agente.UnitTests.Privacy;

public sealed class ContentSanitizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AgenteDIMP.Privacy-" + Guid.NewGuid().ToString("N"));
    private ContentSanitizer Create() => new(new TemporaryStorageOptions { RootPath = _root }, TimeProvider.System);

    [Fact]
    public async Task MasksCpfCnpjAndKnownNamesInDescriptionsExcerptsAndFailures()
    {
        var execution = Guid.NewGuid();
        var text = "Descricao: Pessoa Sintetica Alfa, CPF 000.000.000-00; CNPJ 00.000.000/0000-00; falha para PESSOA SINTETICA ALFA.";
        var result = await Create().SanitizeAsync(execution, text, [new SensitiveValue(SensitiveValueKind.Name, "Pessoa Sintetica Alfa")]);
        Assert.DoesNotContain("000.000.000-00", result.Value);
        Assert.DoesNotContain("00.000.000/0000-00", result.Value);
        Assert.DoesNotContain("Pessoa Sintetica Alfa", result.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[MASK_", result.Value);
    }

    [Fact]
    public async Task TokensAreConsistentAcrossFormattingRetriesAndRecoveredInstances()
    {
        var execution = Guid.NewGuid();
        var first = await Create().SanitizeAsync(execution, "000.000.000-00");
        var recovered = await Create().SanitizeAsync(execution, "00000000000");
        Assert.Equal(first.Value, recovered.Value);
    }

    [Fact]
    public async Task SeparateExecutionsNeverShareMaskTokens()
    {
        var sanitizer = Create();
        var first = await sanitizer.SanitizeAsync(Guid.NewGuid(), "00000000000");
        var second = await sanitizer.SanitizeAsync(Guid.NewGuid(), "00000000000");
        Assert.NotEqual(first.Value, second.Value);
    }

    [Fact]
    public async Task GuardRejectsKnownNameAndIdentifierBeforePersistence()
    {
        var execution = Guid.NewGuid();
        var sanitizer = Create();
        var safe = await sanitizer.SanitizeAsync(execution, "Pessoa Sintetica Alfa", [new SensitiveValue(SensitiveValueKind.Name, "Pessoa Sintetica Alfa")]);
        sanitizer.AssertSafe(execution, safe.Value);
        Assert.Throws<SensitiveContentException>(() => sanitizer.AssertSafe(execution, "CPF 00000000000"));
        Assert.Throws<SensitiveContentException>(() => sanitizer.AssertSafe(execution, "Pessoa Sintetica Alfa"));
        Assert.Throws<SensitiveContentException>(() => sanitizer.AssertSafe(Guid.NewGuid(), safe.Value));
    }

    [Fact]
    public async Task CancellationDoesNotCreateMaskingState()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().SanitizeAsync(Guid.NewGuid(), "00000000000", cancellationToken: source.Token));
        Assert.False(Directory.Exists(_root));
    }

    public void Dispose()
    {
        var absolute = Path.GetFullPath(_root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(absolute).StartsWith("AgenteDIMP.Privacy-", StringComparison.Ordinal))
            throw new InvalidOperationException("Destino de limpeza invalido.");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
    }
}
