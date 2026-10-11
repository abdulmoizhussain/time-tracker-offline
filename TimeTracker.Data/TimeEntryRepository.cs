using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.Data;

public sealed class TimeEntryRepository : ITimeEntryRepository, IScreenSessionEventRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public TimeEntryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_connectionFactory.DatabasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync("PRAGMA synchronous=NORMAL;", cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync("PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);

        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;").ConfigureAwait(false);

        if (version < 1)
        {
            await ApplyVersion1Async(connection, cancellationToken).ConfigureAwait(false);
            version = 1;
        }

        if (version < 2)
        {
            await ApplyVersion2Async(connection, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyVersion1Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string createTimeEntriesSql = """
            CREATE TABLE IF NOT EXISTS TimeEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Description TEXT NOT NULL,
                StartUtc TEXT NULL,
                EndUtc TEXT NULL,
                DurationSeconds INTEGER NOT NULL,
                IsRunning INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            """;

        const string createScreenSessionEventsSql = """
            CREATE TABLE IF NOT EXISTS ScreenSessionEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventType TEXT NOT NULL,
                OccurredUtc TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );
            """;

        const string createIndexesSql = """
            CREATE INDEX IF NOT EXISTS IX_TimeEntries_CreatedUtc ON TimeEntries(CreatedUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_TimeEntries_IsRunning ON TimeEntries(IsRunning);
            CREATE INDEX IF NOT EXISTS IX_TimeEntries_StartUtc_EndUtc ON TimeEntries(StartUtc, EndUtc);
            CREATE INDEX IF NOT EXISTS IX_ScreenSessionEvents_OccurredUtc ON ScreenSessionEvents(OccurredUtc DESC);
            """;

        await connection.ExecuteAsync(createTimeEntriesSql, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(createScreenSessionEventsSql, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(createIndexesSql, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync("PRAGMA user_version = 1;", cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyVersion2Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string createImportExportHistorySql = """
            CREATE TABLE IF NOT EXISTS ImportExportHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ActionType TEXT NOT NULL,
                ItemCount INTEGER NOT NULL,
                FirstEntryDescription TEXT NULL,
                FirstEntryStartUtc TEXT NULL,
                LastEntryDescription TEXT NULL,
                LastEntryStartUtc TEXT NULL,
                FilePath TEXT NULL,
                Notes TEXT NULL,
                OccurredUtc TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );
            """;

        const string createIndexSql = """
            CREATE INDEX IF NOT EXISTS IX_ImportExportHistory_OccurredUtc ON ImportExportHistory(OccurredUtc DESC);
            """;

        await connection.ExecuteAsync(createImportExportHistorySql, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(createIndexSql, cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync("PRAGMA user_version = 2;", cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> StartTimerAsync(string description, DateTime startUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO TimeEntries (Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc)
            VALUES (@Description, @StartUtc, @EndUtc, @DurationSeconds, @IsRunning, @CreatedUtc, @UpdatedUtc);
            SELECT last_insert_rowid();
            """;

        var now = DateTime.UtcNow;
        var parameters = new
        {
            Description = description,
            StartUtc = ToUtcString(startUtc),
            EndUtc = (string?)null,
            DurationSeconds = 0,
            IsRunning = 1,
            CreatedUtc = ToUtcString(now),
            UpdatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return id;
    }

    public async Task StopTimerAsync(int id, DateTime endUtc, CancellationToken cancellationToken = default)
    {
        const string selectSql = """
            SELECT Id, Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc
            FROM TimeEntries
            WHERE Id = @Id;
            """;

        const string updateSql = """
            UPDATE TimeEntries
            SET EndUtc = @EndUtc,
                DurationSeconds = @DurationSeconds,
                IsRunning = 0,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var entry = await connection.QueryFirstOrDefaultAsync<TimeEntryDto>(
            new CommandDefinition(selectSql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (entry is null)
        {
            return;
        }

        var startUtc = ParseUtc(entry.StartUtc);
        var duration = (int)(endUtc - startUtc).TotalSeconds;
        if (duration < 0)
        {
            duration = 0;
        }

        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(
            new CommandDefinition(updateSql, new
            {
                Id = id,
                EndUtc = ToUtcString(endUtc),
                DurationSeconds = duration,
                UpdatedUtc = ToUtcString(now)
            }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<int> AddDurationEntryAsync(string description, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO TimeEntries (Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc)
            VALUES (@Description, @StartUtc, @EndUtc, @DurationSeconds, @IsRunning, @CreatedUtc, @UpdatedUtc);
            SELECT last_insert_rowid();
            """;

        var now = DateTime.UtcNow;
        var parameters = new
        {
            Description = description,
            StartUtc = (string?)null,
            EndUtc = (string?)null,
            DurationSeconds = (int)duration.TotalSeconds,
            IsRunning = 0,
            CreatedUtc = ToUtcString(now),
            UpdatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return id;
    }

    public async Task<int> AddManualRangeEntryAsync(string description, DateTime startLocal, DateTime endLocal, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO TimeEntries (Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc)
            VALUES (@Description, @StartUtc, @EndUtc, @DurationSeconds, @IsRunning, @CreatedUtc, @UpdatedUtc);
            SELECT last_insert_rowid();
            """;

        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();
        var duration = (int)(endUtc - startUtc).TotalSeconds;
        var now = DateTime.UtcNow;

        var parameters = new
        {
            Description = description,
            StartUtc = ToUtcString(startUtc),
            EndUtc = ToUtcString(endUtc),
            DurationSeconds = duration,
            IsRunning = 0,
            CreatedUtc = ToUtcString(now),
            UpdatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return id;
    }

    public async Task UpdateEntryAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE TimeEntries
            SET Description = @Description,
                StartUtc = @StartUtc,
                EndUtc = @EndUtc,
                DurationSeconds = @DurationSeconds,
                IsRunning = @IsRunning,
                UpdatedUtc = @UpdatedUtc
            WHERE Id = @Id;
            """;

        var now = DateTime.UtcNow;
        var parameters = new
        {
            entry.Id,
            entry.Description,
            StartUtc = entry.StartUtc.HasValue ? ToUtcString(entry.StartUtc.Value) : (string?)null,
            EndUtc = entry.EndUtc.HasValue ? ToUtcString(entry.EndUtc.Value) : (string?)null,
            entry.DurationSeconds,
            IsRunning = entry.IsRunning ? 1 : 0,
            UpdatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task DeleteEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM TimeEntries WHERE Id = @Id;";
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetRecentEntriesAsync(int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc
            FROM TimeEntries
            ORDER BY CreatedUtc DESC
            LIMIT @Take;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var entries = await connection.QueryAsync<TimeEntryDto>(
            new CommandDefinition(sql, new { Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return entries.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<string>> SearchDescriptionsAsync(string search, int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT Description
            FROM TimeEntries
            WHERE Description LIKE @Pattern
            ORDER BY Description
            LIMIT @Take;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var descriptions = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { Pattern = $"%{search}%", Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return descriptions.ToList();
    }
    public async Task<int> GetDatabaseVersionAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var version = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("PRAGMA user_version;", cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return version;
    }

    public async Task ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM TimeEntries;
            DELETE FROM ScreenSessionEvents;
            DELETE FROM ImportExportHistory;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task RecordImportExportAsync(ImportExportHistory record, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ImportExportHistory (
                ActionType, ItemCount, FirstEntryDescription, FirstEntryStartUtc,
                LastEntryDescription, LastEntryStartUtc, FilePath, Notes, OccurredUtc, CreatedUtc)
            VALUES (
                @ActionType, @ItemCount, @FirstEntryDescription, @FirstEntryStartUtc,
                @LastEntryDescription, @LastEntryStartUtc, @FilePath, @Notes, @OccurredUtc, @CreatedUtc);
            """;

        var now = DateTime.UtcNow;
        var parameters = new
        {
            record.ActionType,
            record.ItemCount,
            record.FirstEntryDescription,
            FirstEntryStartUtc = record.FirstEntryStartUtc.HasValue ? ToUtcString(record.FirstEntryStartUtc.Value) : (string?)null,
            record.LastEntryDescription,
            LastEntryStartUtc = record.LastEntryStartUtc.HasValue ? ToUtcString(record.LastEntryStartUtc.Value) : (string?)null,
            record.FilePath,
            record.Notes,
            OccurredUtc = ToUtcString(record.OccurredUtc),
            CreatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportExportHistory>> GetRecentImportExportHistoryAsync(int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ActionType, ItemCount, FirstEntryDescription, FirstEntryStartUtc,
                   LastEntryDescription, LastEntryStartUtc, FilePath, Notes, OccurredUtc, CreatedUtc
            FROM ImportExportHistory
            ORDER BY OccurredUtc DESC
            LIMIT @Take;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var records = await connection.QueryAsync<ImportExportHistoryDto>(
            new CommandDefinition(sql, new { Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return records.Select(ToModel).ToList();
    }

    public async Task RecordSessionEventAsync(string eventType, DateTime occurredUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ScreenSessionEvents (EventType, OccurredUtc, CreatedUtc)
            VALUES (@EventType, @OccurredUtc, @CreatedUtc);
            """;

        var now = DateTime.UtcNow;
        var parameters = new
        {
            EventType = eventType,
            OccurredUtc = ToUtcString(occurredUtc),
            CreatedUtc = ToUtcString(now)
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScreenSessionEvent>> GetRecentSessionEventsAsync(int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, EventType, OccurredUtc, CreatedUtc
            FROM ScreenSessionEvents
            ORDER BY OccurredUtc DESC
            LIMIT @Take;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var events = await connection.QueryAsync<ScreenSessionEventDto>(
            new CommandDefinition(sql, new { Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return events.Select(ToModel).ToList();
    }

    public async Task ClearAllSessionEventsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM ScreenSessionEvents;";
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }


    private static string ToUtcString(DateTime value)
    {
        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTime ParseUtc(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return DateTime.MinValue;
        }

        var parsed = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
    }

    private static TimeEntry ToModel(TimeEntryDto dto)
    {
        return new TimeEntry
        {
            Id = dto.Id,
            Description = dto.Description,
            StartUtc = string.IsNullOrEmpty(dto.StartUtc) ? null : ParseUtc(dto.StartUtc),
            EndUtc = string.IsNullOrEmpty(dto.EndUtc) ? null : ParseUtc(dto.EndUtc),
            DurationSeconds = dto.DurationSeconds,
            IsRunning = dto.IsRunning != 0,
            CreatedUtc = ParseUtc(dto.CreatedUtc),
            UpdatedUtc = ParseUtc(dto.UpdatedUtc)
        };
    }

    private static ScreenSessionEvent ToModel(ScreenSessionEventDto dto)
    {
        return new ScreenSessionEvent
        {
            Id = dto.Id,
            EventType = dto.EventType,
            OccurredUtc = ParseUtc(dto.OccurredUtc),
            CreatedUtc = ParseUtc(dto.CreatedUtc)
        };
    }

    private static ImportExportHistory ToModel(ImportExportHistoryDto dto)
    {
        return new ImportExportHistory
        {
            Id = dto.Id,
            ActionType = dto.ActionType,
            ItemCount = dto.ItemCount,
            FirstEntryDescription = dto.FirstEntryDescription,
            FirstEntryStartUtc = string.IsNullOrEmpty(dto.FirstEntryStartUtc) ? null : ParseUtc(dto.FirstEntryStartUtc),
            LastEntryDescription = dto.LastEntryDescription,
            LastEntryStartUtc = string.IsNullOrEmpty(dto.LastEntryStartUtc) ? null : ParseUtc(dto.LastEntryStartUtc),
            FilePath = dto.FilePath,
            Notes = dto.Notes,
            OccurredUtc = ParseUtc(dto.OccurredUtc),
            CreatedUtc = ParseUtc(dto.CreatedUtc)
        };
    }

    private sealed class TimeEntryDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? StartUtc { get; set; }
        public string? EndUtc { get; set; }
        public int DurationSeconds { get; set; }
        public int IsRunning { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string UpdatedUtc { get; set; } = string.Empty;
    }

    private sealed class ScreenSessionEventDto
    {
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string OccurredUtc { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
    }

    private sealed class ImportExportHistoryDto
    {
        public int Id { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public string? FirstEntryDescription { get; set; }
        public string? FirstEntryStartUtc { get; set; }
        public string? LastEntryDescription { get; set; }
        public string? LastEntryStartUtc { get; set; }
        public string? FilePath { get; set; }
        public string? Notes { get; set; }
        public string OccurredUtc { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
    }
}

