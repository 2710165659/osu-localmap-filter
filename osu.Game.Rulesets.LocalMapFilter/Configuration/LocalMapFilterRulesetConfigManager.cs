using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.LocalMapFilter.Configuration;

public class LocalMapFilterRulesetConfigManager : RulesetConfigManager<LocalMapFilterSetting>
{
    public static LocalMapFilterRulesetConfigManager? Instance { get; private set; }

    public LocalMapFilterRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset)
        : base(settings, ruleset)
    {
        Instance = this;
    }

    protected override void InitialiseDefaults()
    {
        base.InitialiseDefaults();
        SetDefault(LocalMapFilterSetting.HideLocalBeatmapsInListing, false);
    }

    public Bindable<bool> GetHideLocalBeatmapsBindable() => GetBindable<bool>(LocalMapFilterSetting.HideLocalBeatmapsInListing);
}

public enum LocalMapFilterSetting
{
    HideLocalBeatmapsInListing,
}
