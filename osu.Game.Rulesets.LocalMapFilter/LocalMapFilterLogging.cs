using System;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.LocalMapFilter;

public static class LocalMapFilterLogging
{
    private const string prefix = "LocalMapFilter";

    public static void LogError(Exception exception, string message) => Logger.Error(exception, $"{prefix}: {message}");
}
