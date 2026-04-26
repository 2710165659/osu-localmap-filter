using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.LocalMapFilter.Configuration;
using osu.Game.Rulesets.LocalMapFilter.Features.Injection;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.StateChanges;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osuTK;

namespace osu.Game.Rulesets.LocalMapFilter;

public partial class LocalMapFilterRuleset : Ruleset
{
    public override string Description => "Local Map Filter";

    public override string ShortName => "localmapfilter";

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods)
        => new DrawableLocalMapFilterRuleset(this, beatmap, mods);

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new LocalMapFilterBeatmapConverter(beatmap, this);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new LocalMapFilterDifficultyCalculator(RulesetInfo, beatmap);

    public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new LocalMapFilterRulesetConfigManager(settings, RulesetInfo);

    public override RulesetSettingsSubsection? CreateSettings() => null;

    public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

    public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => Array.Empty<KeyBinding>();

    public override Drawable CreateIcon() => new RulesetIcon();

    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    private partial class RulesetIcon : CompositeDrawable
    {
        private const int max_injection_attempts = 50;

        public RulesetIcon()
        {
            AutoSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beginInjectWhenAttached();
        }

        private void beginInjectWhenAttached(int attempt = 0)
        {
            OsuGame? game = findParentGame();

            if (game != null)
            {
                InjectorBootstrapper.BeginInject(game, Scheduler);
                return;
            }

            if (attempt >= max_injection_attempts)
                return;

            Scheduler.AddDelayed(() => beginInjectWhenAttached(attempt + 1), 100);
        }

        private OsuGame? findParentGame()
        {
            for (Drawable? current = this; current != null; current = current.Parent)
            {
                if (current is OsuGame game)
                    return game;
            }

            return null;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new OsuSpriteText
            {
                Text = "LMF",
                Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
            };
        }
    }

    private class LocalMapFilterBeatmapConverter : BeatmapConverter<LocalMapFilterHitObject>
    {
        public LocalMapFilterBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => true;

        protected override IEnumerable<LocalMapFilterHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            yield return new LocalMapFilterHitObject
            {
                StartTime = original.StartTime,
                Samples = original.Samples,
            };
        }
    }

    private class LocalMapFilterDifficultyCalculator : DifficultyCalculator
    {
        public LocalMapFilterDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
            => new DifficultyAttributes(mods, 0);

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
            => Array.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
            => Array.Empty<Skill>();
    }

    private class LocalMapFilterHitObject : HitObject
    {
    }

    private partial class DrawableLocalMapFilterRuleset : DrawableRuleset<LocalMapFilterHitObject>
    {
        public DrawableLocalMapFilterRuleset(LocalMapFilterRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new LocalMapFilterPlayfield();

        public override DrawableHitObject<LocalMapFilterHitObject> CreateDrawableRepresentation(LocalMapFilterHitObject h) => new DrawableLocalMapFilterHitObject(h);

        protected override PassThroughInputManager CreateInputManager() => new LocalMapFilterInputManager(Ruleset!.RulesetInfo);

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new LocalMapFilterReplayInputHandler(replay);
    }

    private partial class LocalMapFilterPlayfield : Playfield
    {
    }

    private partial class DrawableLocalMapFilterHitObject : DrawableHitObject<LocalMapFilterHitObject>
    {
        public DrawableLocalMapFilterHitObject(LocalMapFilterHitObject hitObject)
            : base(hitObject)
        {
            Alpha = 0;
            Size = Vector2.Zero;
            AlwaysPresent = false;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset >= 0)
                ApplyResult(HitResult.Perfect);
        }
    }

    private class LocalMapFilterReplayInputHandler : FramedReplayInputHandler<LocalMapFilterReplayFrame>
    {
        public LocalMapFilterReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(LocalMapFilterReplayFrame frame) => false;

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
        }
    }

    private class LocalMapFilterReplayFrame : ReplayFrame
    {
        public override bool IsEquivalentTo(ReplayFrame other) => other.Time == Time;
    }

    private partial class LocalMapFilterInputManager : RulesetInputManager<LocalMapFilterAction>
    {
        public LocalMapFilterInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.None)
        {
        }
    }

    private enum LocalMapFilterAction
    {
    }
}
