using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Api.Agente.Domain.Validation.Records;
using Api.Agente.Infrastructure.Dimp;

namespace Api.Agente.Domain.Validation;

public sealed class DimpValidationEngine
{
    private static readonly FrozenDictionary<string, IRecordValidator> Validators = new Dictionary<string, IRecordValidator>(StringComparer.Ordinal)
    {
        ["0000"] = new Record0000Validator(),
        ["0001"] = new Record0001Validator(),
        ["0002"] = new Record0002Validator(),
        ["0005"] = new Record0005Validator(),
        ["0006"] = new Record0006Validator(),
        ["0100"] = new Record0100Validator(),
        ["0105"] = new Record0105Validator(),
        ["0109"] = new Record0109Validator(),
        ["0200"] = new Record0200Validator(),
        ["0201"] = new Record0201Validator(),
        ["0209"] = new Record0209Validator(),
        ["0300"] = new Record0300Validator(),
        ["0600"] = new Record0600Validator(),
        ["0700"] = new Record0700Validator(),
        ["0990"] = new Record0990Validator(),
        ["1001"] = new Record1001Validator(),
        ["1100"] = new Record1100Validator(),
        ["1110"] = new Record1110Validator(),
        ["1115"] = new Record1115Validator(),
        ["1120"] = new Record1120Validator(),
        ["1200"] = new Record1200Validator(),
        ["1220"] = new Record1220Validator(),
        ["1500"] = new Record1500Validator(),
        ["1600"] = new Record1600Validator(),
        ["1990"] = new Record1990Validator(),
        ["9001"] = new Record9001Validator(),
        ["9900"] = new Record9900Validator(),
        ["9990"] = new Record9990Validator(),
        ["9999"] = new Record9999Validator(),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public async IAsyncEnumerable<RuleEvaluation> ValidateAsync(
        IAsyncEnumerable<DimpRecord> records,
        TemporaryValidationIndex? index,
        Guid executionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var recordCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalLines = 0;
        var bloco0Lines = 0;
        var bloco1Lines = 0;
        var bloco9Lines = 0;

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            totalLines++;

            if (record.DiagnosticCodes.Contains("ORPHAN_RECORD"))
            {
                yield return new RuleEvaluation(
                    executionId,
                    "DIMP10.FILE.FIELD_ORDER",
                    $"line:{record.LineNumber}",
                    RuleApplicability.Applicable,
                    RuleResult.Failed);
            }

            if (record.Fields.IsEmpty)
            {
                continue;
            }

            var code = record.Fields[0];
            recordCounts[code] = recordCounts.GetValueOrDefault(code) + 1;

            if (code.StartsWith('0')) bloco0Lines++;
            else if (code.StartsWith('1')) bloco1Lines++;
            else if (code.StartsWith('9')) bloco9Lines++;

            if (Validators.TryGetValue(code, out var validator))
            {
                var evals = validator.Validate(record, executionId);
                foreach (var ev in evals)
                {
                    yield return ev;
                }
            }
            else
            {
                // Unknown record
                yield return new RuleEvaluation(
                    executionId,
                    "DIMP10.FILE.FIELD_NOTATION",
                    $"line:{record.LineNumber}",
                    RuleApplicability.Applicable,
                    RuleResult.Failed);
            }
        }
    }
}
