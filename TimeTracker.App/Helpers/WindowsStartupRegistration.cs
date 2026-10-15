using System.IO;
// using Microsoft.Win32; // Startup registration disabled (AR-039).

namespace TimeTracker.App.Helpers;

public static class WindowsStartupRegistration
{
    public const string StartupArgument = "--startup";
    /* Startup registry access disabled (AR-039); retained only as source comments.
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TimeTracker";
    */

    public static bool IsStartupLaunch(IEnumerable<string> arguments) =>
        arguments.Contains(StartupArgument, StringComparer.OrdinalIgnoreCase);

    public static string BuildCommand(string executablePath)
    {
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"') ||
            !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Startup requires an absolute executable path.", nameof(executablePath));
        }

        var command = $"\"{executablePath}\" {StartupArgument}";
        if (command.Length > 260)
        {
            throw new ArgumentException("The executable path is too long for Windows startup registration.", nameof(executablePath));
        }
        return command;
    }

    /* Startup registry access disabled (AR-039); excluded from every build.
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var existingKey = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        // Use the apphost, not dotnet.exe when launched through dotnet run.
        var executablePath = Path.Combine(AppContext.BaseDirectory, "TimeTracker.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("TimeTracker.exe was not found. Launch a built or published copy first.", executablePath);
        }
        var command = BuildCommand(executablePath);
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }
    */
}
