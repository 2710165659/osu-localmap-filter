using System;
using osu.Framework.Localisation;

namespace osu.Game.Rulesets.LocalMapFilter;

public static class LocalMapFilterStrings
{
    public static LocalisableString HideExistingBeatmapsRow => text("已有谱面", "Owned beatmaps");

    public static LocalisableString Hide => text("隐藏", "Hide");

    public static LocalisableString Show => text("显示", "Show");

    public static LocalisableString SettingsHeader => text("本地谱面过滤", "Local Map Filter");

    public static LocalisableString SettingsHideLocalBeatmaps => text("在谱面下载列表隐藏已有谱面", "Hide local beatmaps in beatmap listing");

    public static LocalisableString SettingsHideLocalBeatmapsHint => text("浏览在线谱面列表时，过滤掉本地已经存在的谱面集。", "Filters out beatmap sets that already exist locally while browsing the online beatmap listing.");

    public static LocalisableString RowHint => text("自动过滤本地已有谱面。若整页结果都被过滤，会自动继续抓取下一页。", "Skip beatmap sets that already exist locally. If an entire page is filtered out, the next page will be fetched automatically.");

    private static LocalisableString text(string zhCn, string en) => new LocalisableString(new BilingualString(zhCn, en));

    private sealed class BilingualString : ILocalisableStringData
    {
        private readonly string zhCn;
        private readonly string en;

        public BilingualString(string zhCn, string en)
        {
            this.zhCn = zhCn;
            this.en = en;
        }

        public string GetLocalised(LocalisationParameters parameters)
        {
            string? language = parameters.Store?.EffectiveCulture.TwoLetterISOLanguageName;
            return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ? zhCn : en;
        }

        public bool Equals(ILocalisableStringData? other)
            => other is BilingualString bilingual && Equals(bilingual);

        private bool Equals(BilingualString? other)
            => other != null && zhCn == other.zhCn && en == other.en;

        public override bool Equals(object? obj)
            => obj is BilingualString bilingual && Equals(bilingual);

        public override int GetHashCode()
            => HashCode.Combine(zhCn, en);
    }
}
