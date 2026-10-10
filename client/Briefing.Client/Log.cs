using BepInEx.Logging;

namespace Briefing;

internal static class Log
{
    internal static ManualLogSource Source;

    internal static void Info(string message) => Source?.LogInfo(message);
    internal static void Warn(string message) => Source?.LogWarning(message);
    internal static void Error(string message) => Source?.LogError(message);
}
