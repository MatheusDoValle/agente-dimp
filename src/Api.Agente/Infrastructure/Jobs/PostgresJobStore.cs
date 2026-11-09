using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace Api.Agente.Infrastructure.Jobs;

public interface IJobStore
{
    Task EnqueueAsync(Guid executionId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<JobLease?> TryLeaseNextAsync(string workerId, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> HeartbeatAsync(Guid executionId, string workerId, long fencingToken, TimeSpan extension, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> UpdateCheckpointAsync(Guid executionId, string workerId, long fencingToken, string stage, string? checkpointData, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> BeginCompletingAsync(Guid executionId, string workerId, long fencingToken, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> CompleteJobAsync(Guid executionId, string workerId, long fencingToken, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> ReleaseJobAsync(Guid executionId, string workerId, long fencingToken, TimeSpan delay, DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class PostgresJobStore(IConfiguration configuration) : IJobStore
{
    private string GetConnectionString()
    {
        var connectionString = configuration.GetConnectionString("AgentDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'AgentDb' nao configurada.");
        return connectionString;
    }

    public async Task EnqueueAsync(Guid executionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Identidade de execucao invalida.", nameof(executionId));

        var utcNow = now.ToUniversalTime();
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO agente.processing_jobs
                (""ExecutionId"", ""Status"", ""AvailableAt"", ""FencingToken"", ""AttemptNumber"", ""CheckpointVersion"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@executionId, 'pending', @availableAt, 0, 0, 0, @now, @now)
            ON CONFLICT (""ExecutionId"") DO NOTHING;
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("availableAt", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<JobLease?> TryLeaseNextAsync(string workerId, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var utcNow = now.ToUniversalTime();
        var leaseExpiresAt = utcNow.Add(leaseDuration);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        const string selectSql = @"
            SELECT ""ExecutionId""
            FROM agente.processing_jobs
            WHERE (""Status"" = 'pending' AND ""AvailableAt"" <= @now)
               OR (""Status"" = 'leased' AND ""LeaseExpiresAt"" <= @now)
            ORDER BY ""AvailableAt"" ASC
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
        ";

        Guid? candidateId = null;
        await using (var selectCmd = new NpgsqlCommand(selectSql, connection, tx))
        {
            selectCmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
            var scalar = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (scalar is Guid g) candidateId = g;
        }

        if (candidateId is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return null;
        }

        const string updateSql = @"
            UPDATE agente.processing_jobs
            SET ""Status"" = 'leased',
                ""LeaseOwner"" = @workerId,
                ""LeaseExpiresAt"" = @leaseExpiresAt,
                ""HeartbeatAt"" = @now,
                ""FencingToken"" = ""FencingToken"" + 1,
                ""AttemptNumber"" = ""AttemptNumber"" + 1,
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
            RETURNING ""FencingToken"", ""AttemptNumber"", ""LeaseExpiresAt"";
        ";

        long fencingToken;
        int attemptNumber;
        DateTimeOffset expiresAt;

        await using (var updateCmd = new NpgsqlCommand(updateSql, connection, tx))
        {
            updateCmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
            updateCmd.Parameters.AddWithValue("leaseExpiresAt", NpgsqlDbType.TimestampTz, leaseExpiresAt);
            updateCmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
            updateCmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, candidateId.Value);

            await using var reader = await updateCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }

            fencingToken = reader.GetInt64(0);
            attemptNumber = reader.GetInt32(1);
            expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
        }

        await tx.CommitAsync(cancellationToken);
        return new JobLease(candidateId.Value, workerId, fencingToken, attemptNumber, expiresAt);
    }

    public async Task<bool> HeartbeatAsync(Guid executionId, string workerId, long fencingToken, TimeSpan extension, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utcNow = now.ToUniversalTime();
        var leaseExpiresAt = utcNow.Add(extension);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE agente.processing_jobs
            SET ""HeartbeatAt"" = @now,
                ""LeaseExpiresAt"" = @leaseExpiresAt,
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
              AND ""LeaseOwner"" = @workerId
              AND ""FencingToken"" = @fencingToken
              AND ""Status"" = 'leased';
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("leaseExpiresAt", NpgsqlDbType.TimestampTz, leaseExpiresAt);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
        cmd.Parameters.AddWithValue("fencingToken", NpgsqlDbType.Bigint, fencingToken);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> UpdateCheckpointAsync(Guid executionId, string workerId, long fencingToken, string stage, string? checkpointData, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utcNow = now.ToUniversalTime();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE agente.processing_jobs
            SET ""Stage"" = @stage,
                ""CheckpointData"" = @checkpointData,
                ""CheckpointVersion"" = ""CheckpointVersion"" + 1,
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
              AND ""LeaseOwner"" = @workerId
              AND ""FencingToken"" = @fencingToken
              AND ""Status"" = 'leased';
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("stage", NpgsqlDbType.Text, stage);
        cmd.Parameters.AddWithValue("checkpointData", NpgsqlDbType.Text, (object?)checkpointData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
        cmd.Parameters.AddWithValue("fencingToken", NpgsqlDbType.Bigint, fencingToken);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> BeginCompletingAsync(Guid executionId, string workerId, long fencingToken, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utcNow = now.ToUniversalTime();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE agente.processing_jobs
            SET ""Status"" = 'completing',
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
              AND ""LeaseOwner"" = @workerId
              AND ""FencingToken"" = @fencingToken
              AND ""Status"" = 'leased';
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
        cmd.Parameters.AddWithValue("fencingToken", NpgsqlDbType.Bigint, fencingToken);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> CompleteJobAsync(Guid executionId, string workerId, long fencingToken, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utcNow = now.ToUniversalTime();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE agente.processing_jobs
            SET ""Status"" = 'completed',
                ""LeaseOwner"" = NULL,
                ""LeaseExpiresAt"" = NULL,
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
              AND ""LeaseOwner"" = @workerId
              AND ""FencingToken"" = @fencingToken;
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
        cmd.Parameters.AddWithValue("fencingToken", NpgsqlDbType.Bigint, fencingToken);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> ReleaseJobAsync(Guid executionId, string workerId, long fencingToken, TimeSpan delay, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utcNow = now.ToUniversalTime();
        var availableAt = utcNow.Add(delay);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE agente.processing_jobs
            SET ""Status"" = 'pending',
                ""AvailableAt"" = @availableAt,
                ""LeaseOwner"" = NULL,
                ""LeaseExpiresAt"" = NULL,
                ""UpdatedAt"" = @now
            WHERE ""ExecutionId"" = @executionId
              AND ""LeaseOwner"" = @workerId
              AND ""FencingToken"" = @fencingToken;
        ";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("availableAt", NpgsqlDbType.TimestampTz, availableAt);
        cmd.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, utcNow);
        cmd.Parameters.AddWithValue("executionId", NpgsqlDbType.Uuid, executionId);
        cmd.Parameters.AddWithValue("workerId", NpgsqlDbType.Text, workerId);
        cmd.Parameters.AddWithValue("fencingToken", NpgsqlDbType.Bigint, fencingToken);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }
}
