using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Rulesets.LocalMapFilter.Features.BeatmapListing;

public partial class LocalBeatmapFilterRow : BeatmapSearchFilterRow<LocalBeatmapFilterMode>
{
    public LocalBeatmapFilterRow()
        : base(LocalMapFilterStrings.HideExistingBeatmapsRow)
    {
    }

    protected override Drawable CreateFilter() => new LocalBeatmapFilter();

    private partial class LocalBeatmapFilter : BeatmapSearchFilter
    {
        protected override TabItem<LocalBeatmapFilterMode> CreateTabItem(LocalBeatmapFilterMode value) => new LocalBeatmapFilterTabItem(value);
    }

    private partial class LocalBeatmapFilterTabItem : FilterTabItem<LocalBeatmapFilterMode>
    {
        public LocalBeatmapFilterTabItem(LocalBeatmapFilterMode value)
            : base(value)
        {
        }

        protected override LocalisableString LabelFor(LocalBeatmapFilterMode value)
            => value == LocalBeatmapFilterMode.Hide ? LocalMapFilterStrings.Hide : LocalMapFilterStrings.Show;
    }
}

public enum LocalBeatmapFilterMode
{
    Hide,
    Show,
}
