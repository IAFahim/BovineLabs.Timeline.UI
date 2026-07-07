using BovineLabs.Anchor;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.UI.Debug.Views
{
    /// <summary>
    /// Trivial marker view-model for the self-polling Timeline.UI debug toolbar tabs. These tabs read ECS directly on a
    /// scheduler (FPS-tab style) rather than through the system-fed <c>UIHelper</c> pipeline, so they need no bindable
    /// state — <see cref="View{T}"/> just requires a T.
    /// </summary>
    [IsService]
    public sealed class ToolbarSummaryViewModel
    {
    }
}

#endif
