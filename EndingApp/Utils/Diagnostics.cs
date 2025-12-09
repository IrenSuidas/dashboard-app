using System.Diagnostics;

namespace EndingApp.Utils;

internal static class Diagnostics
{
    /// <summary>
    /// Gets the current process private memory size in MB.
    /// </summary>
    public static long GetPrivateMemoryMB()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            return proc.PrivateMemorySize64 / 1024 / 1024;
        }
        catch (Exception ex)
        {
            Logger.Warn("Diagnostics:GetPrivateMemoryMB failed: {0}", ex.Message);
            return -1;
        }
    }

    /// <summary>
    /// Logs a memory value with the given label.
    /// </summary>
    public static void LogMemory(string label, long memoryMB)
    {
        if (memoryMB < 0)
        {
            Logger.Warn("{0}: memory not available", label);
            return;
        }

        Logger.Info("{0}: {1} MB", label, memoryMB);
    }

    /// <summary>
    /// Logs the memory delta between two values in MB.
    /// </summary>
    public static void LogMemoryDelta(string label, long beforeMB, long afterMB)
    {
        if (beforeMB < 0 || afterMB < 0)
        {
            Logger.Warn("{0}: memory not available", label);
            return;
        }

        long delta = afterMB - beforeMB;
        Logger.Info("{0}: {1} MB (delta {2} MB)", label, afterMB, delta);
    }
}
