using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.Data;

public sealed class JiraUploadRepository(SqliteConnectionFactory factory) : IJiraUploadRepository
{
    internal static async Task ApplyMigrationAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS JiraSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Domain TEXT NOT NULL,
                Email TEXT NOT NULL,
                ProtectedToken TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS JiraUploadHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Domain TEXT NOT NULL,
                Email TEXT NOT NULL,
                FromUtc TEXT NOT NULL,
                ToUtcExclusive TEXT NOT NULL,
                StartedUtc TEXT NOT NULL,
                CompletedUtc TEXT NULL,
                TotalCount INTEGER NOT NULL,
                SuccessCount INTEGER NOT NULL DEFAULT 0,
                FailedCount INTEGER NOT NULL DEFAULT 0,
                FirstUploadedStartUtc TEXT NULL,
                LastUploadedStartUtc TEXT NULL,
                Status TEXT NOT NULL
            );
            PRAGMA user_version = 4;
            """;
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<JiraConnectionSettings?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Domain, Email, ProtectedToken FROM JiraSettings WHERE Id = 1;";
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<JiraConnectionSettings>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task SaveSettingsAsync(JiraConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO JiraSettings (Id, Domain, Email, ProtectedToken) VALUES (1, @Domain, @Email, @ProtectedToken)
            ON CONFLICT(Id) DO UPDATE SET Domain = excluded.Domain, Email = excluded.Email, ProtectedToken = excluded.ProtectedToken;
            """;
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, settings, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> CreateHistoryAsync(JiraUploadHistory history, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO JiraUploadHistory (Domain, Email, FromUtc, ToUtcExclusive, StartedUtc, TotalCount, Status)
            VALUES (@Domain, @Email, @FromUtc, @ToUtcExclusive, @StartedUtc, @TotalCount, @Status);
            SELECT last_insert_rowid();
            """;
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            history.Domain, history.Email, FromUtc = Utc(history.FromUtc), ToUtcExclusive = Utc(history.ToUtcExclusive),
            StartedUtc = Utc(history.StartedUtc), history.TotalCount, history.Status
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateHistoryAsync(JiraUploadHistory history, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE JiraUploadHistory SET CompletedUtc = @CompletedUtc, SuccessCount = @SuccessCount,
                FailedCount = @FailedCount, FirstUploadedStartUtc = @FirstUploadedStartUtc,
                LastUploadedStartUtc = @LastUploadedStartUtc, Status = @Status WHERE Id = @Id;
            """;
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            history.Id, CompletedUtc = Utc(history.CompletedUtc), history.SuccessCount, history.FailedCount,
            FirstUploadedStartUtc = Utc(history.FirstUploadedStartUtc), LastUploadedStartUtc = Utc(history.LastUploadedStartUtc), history.Status
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JiraUploadHistory>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM JiraUploadHistory ORDER BY Id DESC;";
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        // SQLite stores UTC as round-trip text; explicitly restore DateTimeKind before UI conversion.
        var rows = await connection.QueryAsync<HistoryRow>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(row => new JiraUploadHistory
        {
            Id = row.Id, Domain = row.Domain, Email = row.Email,
            FromUtc = Parse(row.FromUtc)!.Value, ToUtcExclusive = Parse(row.ToUtcExclusive)!.Value,
            StartedUtc = Parse(row.StartedUtc)!.Value, CompletedUtc = Parse(row.CompletedUtc),
            TotalCount = row.TotalCount, SuccessCount = row.SuccessCount, FailedCount = row.FailedCount,
            FirstUploadedStartUtc = Parse(row.FirstUploadedStartUtc), LastUploadedStartUtc = Parse(row.LastUploadedStartUtc), Status = row.Status
        }).ToList();
    }

    private static string? Utc(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTime? Parse(string? value) => value is null ? null : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private sealed class HistoryRow
    {
        public int Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FromUtc { get; set; } = string.Empty;
        public string ToUtcExclusive { get; set; } = string.Empty;
        public string StartedUtc { get; set; } = string.Empty;
        public string? CompletedUtc { get; set; }
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public string? FirstUploadedStartUtc { get; set; }
        public string? LastUploadedStartUtc { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
