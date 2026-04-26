using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Rulesets.LocalMapFilter.Features.Injection;

namespace osu.Game.Rulesets.LocalMapFilter.Features.BeatmapListing;

public partial class LocalBeatmapListingInterceptor : AbstractHandler
{
    private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly PropertyInfo? internalChildrenProperty = typeof(CompositeDrawable).GetProperty("InternalChildren", flags);
    private static readonly EventInfo? childBecameAliveEvent = typeof(CompositeDrawable).GetEvent("ChildBecameAlive", flags);
    private static readonly EventInfo? childDiedEvent = typeof(CompositeDrawable).GetEvent("ChildDied", flags);
    private static readonly MethodInfo? queueUpdateSearchMethod = typeof(BeatmapListingFilterControl).GetMethod("queueUpdateSearch", flags);
    private static readonly PropertyInfo? currentPageProperty = typeof(BeatmapListingFilterControl).GetProperty(nameof(BeatmapListingFilterControl.CurrentPage), flags);
    private static readonly FieldInfo? searchControlField = typeof(BeatmapListingFilterControl).GetField("searchControl", flags);
    private static readonly FieldInfo? explicitContentFilterField = typeof(BeatmapListingSearchControl).GetField("explicitContentFilter", flags);
    private static readonly FieldInfo? activationRequestedField = typeof(TabItem<RulesetInfo>).GetField("ActivationRequested", flags);
    private const float outer_flow_spacing = 20;
    private const float inner_flow_spacing = 5;

    [Resolved(canBeNull: true)]
    private BeatmapManager? beatmapManager { get; set; }

    private readonly Dictionary<CompositeDrawable, CompositeObserver> compositeObservers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<BeatmapListingOverlay, OverlayPatchState> patchedOverlays = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ToolbarRulesetTabButton, ToolbarPatchState> patchedToolbarButtons = new(ReferenceEqualityComparer.Instance);
    private readonly Bindable<bool> hideLocalBeatmaps = new(false);

    protected override void LoadComplete()
    {
        base.LoadComplete();
        attachSubtree(Game);
    }

    private void attachSubtree(Drawable root)
    {
        var stack = new Stack<Drawable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Drawable current = stack.Pop();
            inspectDrawable(current);

            if (current is not CompositeDrawable composite)
                continue;

            ensureCompositeObserver(composite);
            pushChildren(composite, stack);
        }
    }

    private void detachSubtree(Drawable root)
    {
        var stack = new Stack<Drawable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Drawable current = stack.Pop();

            if (current is CompositeDrawable composite)
            {
                removeCompositeObserver(composite);
                pushChildren(composite, stack);
            }

            cleanupDrawable(current);
        }
    }

    private void inspectDrawable(Drawable drawable)
    {
        if (drawable is BeatmapListingOverlay overlay)
            Schedule(() => patchOverlay(overlay));

        if (drawable is ToolbarRulesetTabButton toolbarButton)
            Schedule(() => patchToolbarButton(toolbarButton));
    }

    private void cleanupDrawable(Drawable drawable)
    {
        if (drawable is BeatmapListingOverlay overlay)
            restoreOverlayPatch(overlay);

        if (drawable is ToolbarRulesetTabButton toolbarButton)
            restoreToolbarButton(toolbarButton);
    }

    private void onChildBecameAlive(Drawable child) => attachSubtree(child);

    private void onChildDied(Drawable child) => detachSubtree(child);

    private void ensureCompositeObserver(CompositeDrawable composite)
    {
        if (compositeObservers.ContainsKey(composite))
            return;

        compositeObservers[composite] = new CompositeObserver(composite, onChildBecameAlive, onChildDied);
    }

    private void removeCompositeObserver(CompositeDrawable composite)
    {
        if (!compositeObservers.Remove(composite, out CompositeObserver? observer))
            return;

        observer.Dispose();
    }

    private void patchOverlay(BeatmapListingOverlay overlay)
    {
        try
        {
            if (patchedOverlays.ContainsKey(overlay))
                return;

            BeatmapListingFilterControl filterControl = overlay.Header.FilterControl;

            var state = new OverlayPatchState(overlay, filterControl, hideLocalBeatmaps)
            {
                OriginalSearchStarted = filterControl.SearchStarted,
                OriginalSearchFinished = filterControl.SearchFinished,
                IsAwaitingFirstVisibleResults = true
            };

            state.WrappedSearchStarted = () =>
            {
                state.IsAwaitingFirstVisibleResults = true;
                tryInsertToggle(state);
                state.OriginalSearchStarted?.Invoke();
            };

            state.WrappedSearchFinished = result => handleSearchFinished(state, result);

            filterControl.SearchStarted = state.WrappedSearchStarted;
            filterControl.SearchFinished = state.WrappedSearchFinished;

            patchedOverlays[overlay] = state;
            tryInsertToggle(state);
        }
        catch (Exception ex)
        {
            LocalMapFilterLogging.LogError(ex, "Failed to patch BeatmapListingOverlay.");
        }
    }

    private void restoreOverlayPatch(BeatmapListingOverlay overlay)
    {
        if (!patchedOverlays.Remove(overlay, out OverlayPatchState? state))
            return;

        if (state.FilterControl.SearchStarted == state.WrappedSearchStarted)
            state.FilterControl.SearchStarted = state.OriginalSearchStarted;

        if (state.FilterControl.SearchFinished == state.WrappedSearchFinished)
            state.FilterControl.SearchFinished = state.OriginalSearchFinished;

        state.Dispose();
    }

    private bool tryInsertToggle(OverlayPatchState state)
    {
        var toggleBindable = state.ToggleBindable;

        if (state.ToggleRow != null)
            return true;

        if (searchControlField?.GetValue(state.FilterControl) is not BeatmapListingSearchControl searchControl)
            return false;

        if (explicitContentFilterField?.GetValue(searchControl) is not Drawable explicitContentFilter)
            return false;

        if (explicitContentFilter.Parent?.Parent is not FillFlowContainer parentFlow)
            return false;

        var toggle = new LocalBeatmapFilterRow();

        toggle.Current.Value = toggleBindable.Value ? LocalBeatmapFilterMode.Hide : LocalBeatmapFilterMode.Show;
        toggle.Current.BindValueChanged(value =>
        {
            bool hideLocal = value.NewValue == LocalBeatmapFilterMode.Hide;

            if (toggleBindable.Value != hideLocal)
                toggleBindable.Value = hideLocal;

            Schedule(() => refreshCurrentSearch(state.FilterControl));
        });

        var wrapper = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Horizontal = 10 },
            Margin = new MarginPadding { Top = inner_flow_spacing - outer_flow_spacing },
            Child = toggle
        };

        parentFlow.Add(wrapper);
        state.ToggleRow = wrapper;
        return true;
    }

    private void patchToolbarButton(ToolbarRulesetTabButton toolbarButton)
    {
        if (patchedToolbarButtons.ContainsKey(toolbarButton))
            return;

        if (!string.Equals(toolbarButton.Value.ShortName, "localmapfilter", StringComparison.Ordinal))
            return;

        patchedToolbarButtons[toolbarButton] = new ToolbarPatchState
        {
            WasEnabled = toolbarButton.Enabled.Value,
            OriginalActivationRequested = activationRequestedField?.GetValue(toolbarButton)
        };

        toolbarButton.Enabled.Value = false;
        activationRequestedField?.SetValue(toolbarButton, null);
    }

    private void restoreToolbarButton(ToolbarRulesetTabButton toolbarButton)
    {
        if (!patchedToolbarButtons.Remove(toolbarButton, out ToolbarPatchState? state))
            return;

        toolbarButton.Enabled.Value = state.WasEnabled;
        activationRequestedField?.SetValue(toolbarButton, state.OriginalActivationRequested);
    }

    private void handleSearchFinished(OverlayPatchState state, BeatmapListingFilterControl.SearchResult result)
    {
        if (result.Type != BeatmapListingFilterControl.SearchResultType.ResultsReturned)
        {
            state.OriginalSearchFinished?.Invoke(result);
            return;
        }

        if (state.ToggleBindable.Value != true || beatmapManager == null)
        {
            state.IsAwaitingFirstVisibleResults = false;
            state.OriginalSearchFinished?.Invoke(result);
            return;
        }

        List<osu.Game.Online.API.Requests.Responses.APIBeatmapSet> filteredResults = filterOutLocalBeatmaps(result.Results);
        bool noMoreResults = getFieldValue<bool>(state.FilterControl, "noMoreResults");

        if (state.IsAwaitingFirstVisibleResults)
        {
            updateTopPreview(state.FilterControl, filteredResults.FirstOrDefault());

            if (filteredResults.Count == 0 && !noMoreResults)
            {
                state.FilterControl.FetchNextPage();
                return;
            }

            setCurrentPage(state.FilterControl, 0);
            state.IsAwaitingFirstVisibleResults = false;
            state.OriginalSearchFinished?.Invoke(BeatmapListingFilterControl.SearchResult.ResultsReturned(filteredResults));
            return;
        }

        if (filteredResults.Count == 0 && !noMoreResults)
        {
            state.FilterControl.FetchNextPage();
            return;
        }

        if (filteredResults.Count == 0 && noMoreResults)
            return;

        state.OriginalSearchFinished?.Invoke(BeatmapListingFilterControl.SearchResult.ResultsReturned(filteredResults));
    }

    private List<osu.Game.Online.API.Requests.Responses.APIBeatmapSet> filterOutLocalBeatmaps(List<osu.Game.Online.API.Requests.Responses.APIBeatmapSet> results)
    {
        var filtered = new List<osu.Game.Online.API.Requests.Responses.APIBeatmapSet>(results.Count);
        var cache = new Dictionary<int, bool>();

        foreach (var beatmapSet in results)
        {
            if (beatmapSet.OnlineID <= 0)
            {
                filtered.Add(beatmapSet);
                continue;
            }

            if (!cache.TryGetValue(beatmapSet.OnlineID, out bool isLocal))
            {
                isLocal = beatmapManager!.IsAvailableLocally(new BeatmapSetInfo { OnlineID = beatmapSet.OnlineID });
                cache[beatmapSet.OnlineID] = isLocal;
            }

            if (!isLocal)
                filtered.Add(beatmapSet);
        }

        return filtered;
    }

    private static void refreshCurrentSearch(BeatmapListingFilterControl filterControl) => queueUpdateSearchMethod?.Invoke(filterControl, new object?[] { false });

    private static void updateTopPreview(BeatmapListingFilterControl filterControl, osu.Game.Online.API.Requests.Responses.APIBeatmapSet? beatmapSet)
    {
        if (searchControlField?.GetValue(filterControl) is not BeatmapListingSearchControl searchControl)
            return;

        searchControl.BeatmapSet = beatmapSet;
    }

    private static void setCurrentPage(BeatmapListingFilterControl filterControl, int value) => currentPageProperty?.SetValue(filterControl, value);

    private static void pushChildren(CompositeDrawable composite, Stack<Drawable> stack)
    {
        if (internalChildrenProperty?.GetValue(composite) is not IEnumerable children)
            return;

        foreach (object? child in children)
        {
            if (child is Drawable drawable)
                stack.Push(drawable);
        }
    }

    private static IEnumerable<T> findDescendants<T>(Drawable root)
        where T : Drawable
    {
        var stack = new Stack<Drawable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Drawable current = stack.Pop();

            if (current is T typed)
                yield return typed;

            if (current is CompositeDrawable composite)
                pushChildren(composite, stack);
        }
    }

    private static T? getFieldValue<T>(object owner, string fieldName)
    {
        for (Type? current = owner.GetType(); current != null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(fieldName, flags);

            if (field == null)
                continue;

            return (T?)field.GetValue(owner);
        }

        return default;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        foreach (CompositeObserver observer in compositeObservers.Values)
            observer.Dispose();

        compositeObservers.Clear();

        foreach (BeatmapListingOverlay overlay in patchedOverlays.Keys.ToList())
            restoreOverlayPatch(overlay);

        foreach (ToolbarRulesetTabButton toolbarButton in patchedToolbarButtons.Keys.ToList())
            restoreToolbarButton(toolbarButton);
    }

    private sealed class OverlayPatchState : IDisposable
    {
        public readonly BeatmapListingOverlay Overlay;
        public readonly BeatmapListingFilterControl FilterControl;
        public Bindable<bool> ToggleBindable;

        public Action? OriginalSearchStarted;
        public Action? WrappedSearchStarted;
        public Action<BeatmapListingFilterControl.SearchResult>? OriginalSearchFinished;
        public Action<BeatmapListingFilterControl.SearchResult>? WrappedSearchFinished;
        public bool IsAwaitingFirstVisibleResults;
        public Drawable? ToggleRow;
        public bool IsDisposed;

        public OverlayPatchState(BeatmapListingOverlay overlay, BeatmapListingFilterControl filterControl, Bindable<bool> toggleBindable)
        {
            Overlay = overlay;
            FilterControl = filterControl;
            ToggleBindable = toggleBindable;
        }

        public void Dispose()
        {
            IsDisposed = true;
            ToggleRow?.Expire();
        }
    }

    private sealed class ToolbarPatchState
    {
        public bool WasEnabled;
        public object? OriginalActivationRequested;
    }

    private sealed class CompositeObserver : IDisposable
    {
        private readonly CompositeDrawable composite;
        private readonly Delegate? childBecameAliveHandler;
        private readonly Delegate? childDiedHandler;

        public CompositeObserver(CompositeDrawable composite, Action<Drawable> childBecameAlive, Action<Drawable> childDied)
        {
            this.composite = composite;
            childBecameAliveHandler = createHandler(childBecameAliveEvent, childBecameAlive);
            childDiedHandler = createHandler(childDiedEvent, childDied);

            addHandler(childBecameAliveEvent, childBecameAliveHandler);
            addHandler(childDiedEvent, childDiedHandler);
        }

        public void Dispose()
        {
            removeHandler(childBecameAliveEvent, childBecameAliveHandler);
            removeHandler(childDiedEvent, childDiedHandler);
        }

        private Delegate? createHandler(EventInfo? eventInfo, Action<Drawable> callback)
        {
            if (eventInfo?.EventHandlerType == null)
                return null;

            return Delegate.CreateDelegate(eventInfo.EventHandlerType, callback.Target, callback.Method, false);
        }

        private void addHandler(EventInfo? eventInfo, Delegate? handler)
        {
            if (eventInfo?.GetAddMethod(true) == null || handler == null)
                return;

            eventInfo.GetAddMethod(true)!.Invoke(composite, new object?[] { handler });
        }

        private void removeHandler(EventInfo? eventInfo, Delegate? handler)
        {
            if (eventInfo?.GetRemoveMethod(true) == null || handler == null)
                return;

            eventInfo.GetRemoveMethod(true)!.Invoke(composite, new object?[] { handler });
        }
    }
}
