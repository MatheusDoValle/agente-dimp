using System.Text;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using ReportContract = Api.Agente.Application.Contracts.Report;
using DomainReport = Api.Agente.Domain.Reports.Report;
using DomainFinding = Api.Agente.Domain.Validation.Finding;
using ContractFinding = Api.Agente.Application.Contracts.Finding;

namespace Api.Agente.Infrastructure.Reports;

public sealed class ReportStoreException : Exception
{
    public ReportStoreException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class ExecutionNotFoundException(Guid id) : Exception($"Execucao {id} nao encontrada.");
public sealed class AnalysisInProgressException(Guid id) : Exception($"Execucao {id} ainda em andamento.");
public sealed class ReportExpiredException(Guid id) : Exception($"Relatorio da execucao {id} expirado.");
public sealed class ReportUnavailableException(Guid id) : Exception($"Relatorio indisponivel para a execucao {id}.");
public sealed class InvalidCursorException(string message) : Exception(message);

public interface IReportStore
{
    Task SaveReportAsync(
        DomainReport report,
        IEnumerable<Domain.Reports.EvidenceSnapshot> evidence,
        IEnumerable<DomainFinding> findings,
        IEnumerable<RuleEvaluation> evaluations,
        CancellationToken cancellationToken = default);

    Task<ReportContract> GetReportAsync(
        Guid executionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<FindingsPage> GetFindingsPageAsync(
        Guid executionId,
        string? cursor,
        int pageSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class ReportStore(AgentDbContext dbContext) : IReportStore
{
    public async Task SaveReportAsync(
        DomainReport report,
        IEnumerable<Domain.Reports.EvidenceSnapshot> evidence,
        IEnumerable<DomainFinding> findings,
        IEnumerable<RuleEvaluation> evaluations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var existingReport = await dbContext.Reports
            .SingleOrDefaultAsync(r => r.ExecutionId == report.ExecutionId, cancellationToken);

        if (existingReport is null)
        {
            dbContext.Reports.Add(report);
        }

        foreach (var ev in evidence)
        {
            var exists = await dbContext.Evidence
                .AnyAsync(e => e.Id == ev.Id, cancellationToken);
            if (!exists)
            {
                dbContext.Evidence.Add(ev);
            }
        }

        foreach (var f in findings)
        {
            var exists = await dbContext.Findings
                .AnyAsync(x => x.Id == f.Id, cancellationToken);
            if (!exists)
            {
                dbContext.Findings.Add(f);
            }
        }

        foreach (var rev in evaluations)
        {
            var exists = await dbContext.RuleEvaluations
                .AnyAsync(r => r.ExecutionId == rev.ExecutionId && r.RuleId == rev.RuleId && r.OccurrenceKey == rev.OccurrenceKey, cancellationToken);
            if (!exists)
            {
                dbContext.RuleEvaluations.Add(rev);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReportContract> GetReportAsync(
        Guid executionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var execution = await dbContext.Executions.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution is null)
        {
            var tombstone = await dbContext.ExecutionTombstones.AsNoTracking()
                .SingleOrDefaultAsync(t => t.ExecutionId == executionId, cancellationToken);

            if (tombstone != null && now < tombstone.PurgeAt)
                throw new ReportExpiredException(executionId);

            throw new ExecutionNotFoundException(executionId);
        }

        if (execution.ReportExpiresAt.HasValue && now >= execution.ReportExpiresAt.Value)
            throw new ReportExpiredException(executionId);

        if (!execution.EndedAt.HasValue)
            throw new AnalysisInProgressException(executionId);

        var reportEntity = await dbContext.Reports.AsNoTracking()
            .SingleOrDefaultAsync(r => r.ExecutionId == executionId, cancellationToken);

        if (!execution.HasReport && reportEntity is null)
            throw new ReportUnavailableException(executionId);

        var evaluations = await dbContext.RuleEvaluations.AsNoTracking()
            .Where(r => r.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        var findings = await dbContext.Findings.AsNoTracking()
            .Where(f => f.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        var evidence = await dbContext.Evidence.AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        AnalyticContent? analyticContent = null;
        if (!string.IsNullOrWhiteSpace(reportEntity?.AnalyticContent))
        {
            try
            {
                analyticContent = ContractJson.Deserialize<AnalyticContent>(reportEntity.AnalyticContent);
            }
            catch
            {
                // Ignore parse errors on raw analytical content to preserve deterministic facts
            }
        }

        var priceBook = await dbContext.PriceBooks.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var costEstimator = new PriceBookCostEstimator(priceBook);
        var aggregator = new Application.Usage.ExecutionUsageAggregator(dbContext, costEstimator);
        var usageSummary = await aggregator.AggregateExecutionUsageAsync(executionId, null, cancellationToken);

        return ReportComposer.Compose(
            execution,
            evaluations,
            findings,
            evidence,
            analyticContent,
            usageSummary);
    }

    public async Task<FindingsPage> GetFindingsPageAsync(
        Guid executionId,
        string? cursor,
        int pageSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (pageSize < 1 || pageSize > 500)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize deve estar entre 1 e 500.");

        var execution = await dbContext.Executions.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution is null)
        {
            var tombstone = await dbContext.ExecutionTombstones.AsNoTracking()
                .SingleOrDefaultAsync(t => t.ExecutionId == executionId, cancellationToken);

            if (tombstone != null && now < tombstone.PurgeAt)
                throw new ReportExpiredException(executionId);

            throw new ExecutionNotFoundException(executionId);
        }

        if (execution.ReportExpiresAt.HasValue && now >= execution.ReportExpiresAt.Value)
            throw new ReportExpiredException(executionId);

        if (!execution.EndedAt.HasValue)
            throw new AnalysisInProgressException(executionId);

        var reportEntity = await dbContext.Reports.AsNoTracking()
            .SingleOrDefaultAsync(r => r.ExecutionId == executionId, cancellationToken);

        if (!execution.HasReport && reportEntity is null)
            throw new ReportUnavailableException(executionId);

        int offset = 0;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            offset = DecodeCursor(cursor, executionId);
        }

        var baseQuery = dbContext.Findings.AsNoTracking()
            .Where(f => f.ExecutionId == executionId);

        var total = await baseQuery.LongCountAsync(cancellationToken);

        var findings = await baseQuery
            .OrderBy(f => f.Location.Line ?? long.MaxValue)
            .ThenBy(f => f.Location.ByteOffset ?? long.MaxValue)
            .ThenBy(f => f.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (offset + findings.Count < total)
        {
            nextCursor = EncodeCursor(executionId, offset + findings.Count);
        }

        var items = findings.Select(f => new ContractFinding
        {
            Id = f.Id.ToString(),
            RuleId = f.RuleId,
            Category = f.Category switch
            {
                FindingCategory.StructuralError => FindingCategoryValue.ErroEstrutural,
                FindingCategory.BusinessInconsistency => FindingCategoryValue.InconsistenciaNegocio,
                FindingCategory.AiIndication => FindingCategoryValue.IndicioIa,
                FindingCategory.MissingEvidence => FindingCategoryValue.AusenciaEvidencias,
                _ => FindingCategoryValue.FalhaTecnica
            },
            Severity = f.Severity switch
            {
                FindingSeverity.Blocking => FindingSeverityValue.Bloqueante,
                FindingSeverity.Error => FindingSeverityValue.Erro,
                FindingSeverity.Warning => FindingSeverityValue.Aviso,
                _ => FindingSeverityValue.Informativo
            },
            Origin = f.Origin switch
            {
                FindingOrigin.Deterministic => FindingOriginValue.Deterministico,
                FindingOrigin.Ai => FindingOriginValue.Ia,
                _ => FindingOriginValue.Sistema
            },
            Description = f.Description,
            Location = new Location
            {
                Line = f.Location.Line,
                ByteOffset = f.Location.ByteOffset,
                RecordCode = f.Location.RecordCode,
                FieldPosition = f.Location.FieldPosition,
                UnavailableReason = f.Location.UnavailableReason
            },
            EvidenceIds = f.EvidenceIds.Select(e => e.ToString()).ToArray()
        }).ToArray();

        return new FindingsPage
        {
            Items = items,
            NextCursor = nextCursor,
            Total = total
        };
    }

    private static string EncodeCursor(Guid executionId, int offset)
    {
        var raw = $"{executionId:D}:{offset}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private static int DecodeCursor(string cursor, Guid expectedExecutionId)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var raw = Encoding.UTF8.GetString(bytes);
            var parts = raw.Split(':');
            if (parts.Length != 2)
                throw new InvalidCursorException("Formato de cursor invalido.");

            if (!Guid.TryParse(parts[0], out var execId) || execId != expectedExecutionId)
                throw new InvalidCursorException("Cursor pertence a outra execucao.");

            if (!int.TryParse(parts[1], out var offset) || offset < 0)
                throw new InvalidCursorException("Offset invalido no cursor.");

            return offset;
        }
        catch (FormatException)
        {
            throw new InvalidCursorException("Cursor nao e base64 valido.");
        }
    }
}
