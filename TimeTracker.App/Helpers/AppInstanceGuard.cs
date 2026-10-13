using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TimeTracker.App.Helpers;

/// <summary>Serializes launch decisions and holds named presence objects until this copy exits.</summary>
public sealed class AppInstanceGuard : IDisposable
{
    private readonly Mutex _directoryPresence;
    public EventWaitHandle DirectoryActivation { get; }
    public EventWaitHandle AnyActivation { get; }

    private AppInstanceGuard(Mutex directoryPresence, EventWaitHandle directoryActivation, EventWaitHandle anyActivation)
    {
        _directoryPresence = directoryPresence;
        DirectoryActivation = directoryActivation;
        AnyActivation = anyActivation;
    }

    public static AppInstanceGuard? TryAcquire(string directory, string userId,
        Func<bool> readAllowMultipleInstances, bool activateExisting)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)).ToUpperInvariant();
        var directoryId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        var directoryEventName = $"Local\\TimeTracker.Activate.Directory.{userId}.{directoryId}";
        using var admission = new Mutex(false, $"Local\\TimeTracker.SingleInstance.Admission.{userId}");
        var acquired = false;
        Mutex? presence = null;
        EventWaitHandle? directoryEvent = null;
        EventWaitHandle? anyEvent = null;
        try
        {
            try { acquired = admission.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Another TimeTracker launch is still being initialized. Please try again.");

            // This mutex is a presence marker, not an owned lock: disposal can occur on any thread.
            presence = new Mutex(false, $"Local\\TimeTracker.SingleInstance.Directory.{userId}.{directoryId}", out var newDirectory);
            if (!newDirectory)
            {
                if (activateExisting && EventWaitHandle.TryOpenExisting(directoryEventName, out var existing))
                {
                    using (existing) existing.Set();
                }
                return null;
            }

            var allowMultiple = readAllowMultipleInstances();
            directoryEvent = new EventWaitHandle(false, EventResetMode.AutoReset, directoryEventName);
            // Every admitted copy keeps this shared event alive, including after the first copy exits.
            anyEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
                $"Local\\TimeTracker.Activate.{userId}", out var firstInstance);
            if (!firstInstance && !allowMultiple)
            {
                if (activateExisting) anyEvent.Set();
                return null;
            }

            var guard = new AppInstanceGuard(presence, directoryEvent, anyEvent);
            presence = null;
            directoryEvent = null;
            anyEvent = null;
            return guard;
        }
        finally
        {
            anyEvent?.Dispose();
            directoryEvent?.Dispose();
            presence?.Dispose();
            if (acquired) admission.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        AnyActivation.Dispose();
        DirectoryActivation.Dispose();
        _directoryPresence.Dispose();
    }
}
