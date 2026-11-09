using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Jobs;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Agente.Application;

public sealed record SubmitAnalysisResult
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public Accepted? Accepted { get; init; }
    public Problem? Problem { get; init; }

    public static SubmitAnalysisResult Success(Accepted accepted) =>
        new() { IsSuccess = true, StatusCode = 202, Accepted = accepted };

    public static SubmitAnalysisResult Failure(int statusCode, string code, string message, Guid correlationId) =>
        new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Problem = new Problem
            {
                Code = code,
                Message = message,
                CorrelationId = correlationId,
                ExecutionId = null
            }
        };
}

public sealed class SubmitAnalysisService(
    ExecutionContext executionContext,
    AgentDbContext dbContext,
    ITemporaryArtifactStore artifactStore,
    TimeProvider clock)
{
    public Execution CreateExecution() => executionContext.CreateExecution();

    public async Task<TemporaryArtifact> SaveOriginalAsync(Guid executionId, Stream stream, CancellationToken cancellationToken) =>
        await artifactStore.CreateAsync(executionId, TemporaryArtifactKind.Original, stream, cancellationToken);

    public async Task CleanupAsync(Guid executionId)
    {
        try
        {
            await artifactStore.DeleteExecutionAsync(executionId, CancellationToken.None);
        }
        catch
        {
            // best effort
        }
    }

    public async Task<SubmitAnalysisResult> CompleteSubmissionAsync(
        Execution execution,
        TemporaryArtifact artifact,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (artifact.ByteSize == 0)
        {
            await CleanupAsync(execution.Id);
            return SubmitAnalysisResult.Failure(422, "EMPTY_FILE",
                "Arquivo vazio ou impossivel de receber.", correlationId);
        }

        try
        {
            var now = clock.GetUtcNow();
            execution.SetReceivedFile(artifact.ByteSize, artifact.Sha256);
            var job = new ProcessingJob(execution.Id, now);

            dbContext.Executions.Add(execution);
            dbContext.ProcessingJobs.Add(job);
            await dbContext.SaveChangesAsync(cancellationToken);

            var accepted = new Accepted
            {
                ExecutionId = execution.Id,
                Status = AcceptedStatusValue.Recebida,
                StatusUrl = $"/api/v1/analises/{execution.Id}",
                ReportUrl = $"/api/v1/analises/{execution.Id}/relatorio",
                AcceptedAt = execution.CreatedAt
            };

            return SubmitAnalysisResult.Success(accepted);
        }
        catch (Exception)
        {
            await CleanupAsync(execution.Id);
            return SubmitAnalysisResult.Failure(503, "PERSISTENCE_UNAVAILABLE",
                "Persistencia/fila indisponivel; nao aceitar trabalho que seria perdido.", correlationId);
        }
    }
}
