using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LocalMapFilter.Configuration;

namespace osu.Game.Rulesets.LocalMapFilter.Settings;

public partial class LocalMapFilterSettingsSubsection : RulesetSettingsSubsection
{
    public LocalMapFilterSettingsSubsection(LocalMapFilterRuleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        var config = (LocalMapFilterRulesetConfigManager)Config;

        Child = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding
            {
                Horizontal = 20,
                Vertical = 10
            },
            Child = new FormCheckBox
            {
                Caption = LocalMapFilterStrings.SettingsHideLocalBeatmaps,
                HintText = LocalMapFilterStrings.SettingsHideLocalBeatmapsHint,
                Current = config.GetHideLocalBeatmapsBindable()
            }
        };
    }
}
