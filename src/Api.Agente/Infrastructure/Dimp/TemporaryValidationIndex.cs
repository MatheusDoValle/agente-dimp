using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Api.Agente.Domain.Validation;
using Microsoft.Data.Sqlite;

namespace Api.Agente.Infrastructure.Dimp;

public sealed class TemporaryValidationIndex : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly SqliteConnection _connection;
    private bool _disposed;

    private TemporaryValidationIndex(string databasePath, SqliteConnection connection)
    {
        _databasePath = databasePath;
        _connection = connection;
    }

    public static async Task<TemporaryValidationIndex> CreateAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                PRAGMA synchronous = OFF;
                PRAGMA journal_mode = MEMORY;
                PRAGMA temp_store = MEMORY;

                CREATE TABLE IF NOT EXISTS records (
                    line_number INTEGER PRIMARY KEY,
                    byte_offset INTEGER NOT NULL,
                    record_code TEXT NOT NULL,
                    fields_json TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_records_code ON records(record_code);

                CREATE TABLE IF NOT EXISTS record_counts (
                    record_code TEXT PRIMARY KEY,
                    count INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS decimal_fields (
                    line_number INTEGER NOT NULL,
                    record_code TEXT NOT NULL,
                    position INTEGER NOT NULL,
                    amount_text TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_decimal_code_pos ON decimal_fields(record_code, position);
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return new TemporaryValidationIndex(databasePath, connection);
    }

    public async Task PopulateAsync(
        IAsyncEnumerable<DimpRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        using var transaction = _connection.BeginTransaction();

        using var insertRecordCmd = _connection.CreateCommand();
        insertRecordCmd.Transaction = transaction;
        insertRecordCmd.CommandText = """
            INSERT INTO records (line_number, byte_offset, record_code, fields_json)
            VALUES (@line, @offset, @code, @fields);
            """;
        var pLine = insertRecordCmd.Parameters.Add("@line", SqliteType.Integer);
        var pOffset = insertRecordCmd.Parameters.Add("@offset", SqliteType.Integer);
        var pCode = insertRecordCmd.Parameters.Add("@code", SqliteType.Text);
        var pFields = insertRecordCmd.Parameters.Add("@fields", SqliteType.Text);

        using var upsertCountCmd = _connection.CreateCommand();
        upsertCountCmd.Transaction = transaction;
        upsertCountCmd.CommandText = """
            INSERT INTO record_counts (record_code, count) VALUES (@code, 1)
            ON CONFLICT(record_code) DO UPDATE SET count = count + 1;
            """;
        var pCountCode = upsertCountCmd.Parameters.Add("@code", SqliteType.Text);

        using var insertDecimalCmd = _connection.CreateCommand();
        insertDecimalCmd.Transaction = transaction;
        insertDecimalCmd.CommandText = """
            INSERT INTO decimal_fields (line_number, record_code, position, amount_text)
            VALUES (@line, @code, @pos, @amount);
            """;
        var pDecLine = insertDecimalCmd.Parameters.Add("@line", SqliteType.Integer);
        var pDecCode = insertDecimalCmd.Parameters.Add("@code", SqliteType.Text);
        var pDecPos = insertDecimalCmd.Parameters.Add("@pos", SqliteType.Integer);
        var pDecAmount = insertDecimalCmd.Parameters.Add("@amount", SqliteType.Text);

        var batchCount = 0;

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (record.Fields.IsEmpty)
            {
                continue;
            }

            var recordCode = record.Fields[0];

            pLine.Value = record.LineNumber;
            pOffset.Value = record.ByteOffset;
            pCode.Value = recordCode;
            pFields.Value = JsonSerializer.Serialize(record.Fields);
            await insertRecordCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            pCountCode.Value = recordCode;
            await upsertCountCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            for (var i = 1; i < record.Fields.Length; i++)
            {
                var val = record.Fields[i];
                if (!string.IsNullOrWhiteSpace(val) && IsPotentialDecimal(val))
                {
                    pDecLine.Value = record.LineNumber;
                    pDecCode.Value = recordCode;
                    pDecPos.Value = i + 1; // 1-based field position
                    pDecAmount.Value = val;
                    await insertDecimalCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            batchCount++;
            if (batchCount % 1000 == 0)
            {
                transaction.Commit();
                // Start next transaction
                transaction.Dispose();
                // Re-assign transaction
                // (In this loop, transaction committed; for simplicity we keep 1 outer transaction or re-begin)
            }
        }

        try
        {
            transaction.Commit();
        }
        catch (InvalidOperationException)
        {
            // Already committed if batchCount was exact multiple
        }
    }

    public async Task<int> GetRecordCountAsync(string recordCode, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT count FROM record_counts WHERE record_code = @code;";
        cmd.Parameters.AddWithValue("@code", recordCode);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task<int> GetTotalRecordCountAsync(CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(count), 0) FROM record_counts;";

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task<decimal> SumDecimalFieldAsync(string recordCode, int position, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT amount_text FROM decimal_fields WHERE record_code = @code AND position = @pos;";
        cmd.Parameters.AddWithValue("@code", recordCode);
        cmd.Parameters.AddWithValue("@pos", position);

        decimal sum = 0m;
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var text = reader.GetString(0);
            if (TryParseDecimal(text, out var val))
            {
                sum += val;
            }
        }

        return sum;
    }

    public async Task<IReadOnlyList<DimpRecordSummary>> GetRecordsAsync(string recordCode, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT line_number, byte_offset, fields_json FROM records WHERE record_code = @code ORDER BY line_number;";
        cmd.Parameters.AddWithValue("@code", recordCode);

        var list = new List<DimpRecordSummary>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var line = reader.GetInt64(0);
            var offset = reader.GetInt64(1);
            var json = reader.GetString(2);
            var fields = JsonSerializer.Deserialize<string[]>(json) ?? [];
            list.Add(new DimpRecordSummary(line, offset, [.. fields]));
        }

        return list;
    }

    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = text.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsPotentialDecimal(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c) && c != ',' && c != '.' && c != '-') return false;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _connection.CloseAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
            var wal = _databasePath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _databasePath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch (IOException)
        {
            // Suppress cleanup file lock errors
        }
    }
}

public sealed record DimpRecordSummary(long LineNumber, long ByteOffset, ImmutableArray<string> Fields);
