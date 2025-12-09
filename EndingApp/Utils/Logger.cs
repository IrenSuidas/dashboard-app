using System.Diagnostics;

namespace EndingApp.Utils;

internal static class Logger
{
    [Conditional("DEBUG")]
    public static void Debug(string message, params object[] args)
    {
        Console.WriteLine(GetMessage("DEBUG", message, args));
    }

    public static void Info(string message, params object[] args)
    {
        Console.WriteLine(GetMessage("INFO", message, args));
    }

    public static void Warn(string message, params object[] args)
    {
        Console.WriteLine(GetMessage("WARN", message, args));
    }

    public static void Error(string message, params object[] args)
    {
        Console.WriteLine(GetMessage("ERROR", message, args));
    }

    private static string GetMessage(string level, string message, object[] args)
    {
        string formatted = args?.Length > 0 ? string.Format(message, args) : message;
        return $"{DateTime.UtcNow:O} {level}: {formatted}";
    }
}
