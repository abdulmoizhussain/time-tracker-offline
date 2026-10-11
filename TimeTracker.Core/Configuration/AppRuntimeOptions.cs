namespace TimeTracker.Core.Configuration;

public sealed class AppRuntimeOptions
{
    public StorageOptions Storage { get; set; } = new();
    public HotkeyOptions Hotkeys { get; set; } = new();
    public SessionEventTrackingOptions SessionEvents { get; set; } = new();
    public UiOptions Ui { get; set; } = new();
    public TimerOptions Timer { get; set; } = new();
}

public sealed class StorageOptions
{
    public string DatabaseDirectory { get; set; } = "Data";
    public string DatabaseFileName { get; set; } = "timetracker.db";
}

public sealed class HotkeyOptions
{
    public bool Enabled { get; set; }
    public string StartStop { get; set; } = "Ctrl+Shift+T";
}

public sealed class SessionEventTrackingOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class UiOptions
{
    public double ScaleMultiplier { get; set; } = 1.0;
}

public sealed class TimerOptions
{
    public string DefaultDescription { get; set; } = "Untitled task";
}
