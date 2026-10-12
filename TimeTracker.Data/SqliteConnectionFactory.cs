using Microsoft.Data.Sqlite;

namespace TimeTracker.Data;

public sealed class SqliteConnectionFactory
{
    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(ConnectionString);
}
