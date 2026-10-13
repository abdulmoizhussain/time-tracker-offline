using System.IO;
using TimeTracker.Core.Configuration;

namespace TimeTracker.App.Helpers;

public sealed record AppStoragePaths(string DataDirectory, string DatabasePath, string? LegacyDatabasePath)
{
    public static string DataRoot(StorageOptions options, string? executableDirectory = null, string? localAppData = null)
        => Path.GetFullPath(options.PortableMode
            ? executableDirectory ?? AppContext.BaseDirectory
            : Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTracker"));

    public static AppStoragePaths Resolve(StorageOptions options, string executableDirectory, string localAppData, string[] args)
    {
        var root = DataRoot(options, executableDirectory, localAppData);
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--db-path" or "-db-path")
                return new(root, Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[i + 1]), executableDirectory), null);
        }

        var directory = Environment.ExpandEnvironmentVariables(options.DatabaseDirectory);
        var database = Path.GetFullPath(Path.Combine(root, directory, options.DatabaseFileName));
        var legacy = !options.PortableMode && !Path.IsPathRooted(directory)
            ? Path.GetFullPath(Path.Combine(executableDirectory, directory, options.DatabaseFileName)) : null;
        if (string.Equals(database, legacy, StringComparison.OrdinalIgnoreCase)) legacy = null;
        return new(root, database, legacy);
    }
}
