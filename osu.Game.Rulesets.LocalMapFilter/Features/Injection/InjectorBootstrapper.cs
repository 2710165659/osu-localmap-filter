using System;
using osu.Framework.Threading;
using osu.Game.Rulesets.LocalMapFilter.Features.BeatmapListing;

namespace osu.Game.Rulesets.LocalMapFilter.Features.Injection;

public static class InjectorBootstrapper
{
    private static int currentSessionHash = int.MinValue;

    public static bool BeginInject(OsuGame game, Scheduler scheduler)
    {
        int sessionHash = game.GetHashCode();

        if (sessionHash == currentSessionHash)
            return true;

        currentSessionHash = sessionHash;

        scheduler.AddDelayed(() =>
        {
            try
            {
                game.Add(new LocalBeatmapListingInterceptor());
            }
            catch (Exception e)
            {
                currentSessionHash = int.MinValue;
                LocalMapFilterLogging.LogError(e, "Failed to inject beatmap listing interceptor");
            }
        }, 1);

        return true;
    }
}
