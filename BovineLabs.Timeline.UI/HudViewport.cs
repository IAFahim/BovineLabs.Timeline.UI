using UnityEngine;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The SINGLE seam that answers "which screen region belongs to player <c>n</c>", as a normalized
    /// (0..1) rect. Today, with one shared camera, every player resolves to the full screen — but callers
    /// (e.g. the damage vignette) never assume that: they ask here. A future split-screen layout installs a
    /// per-player resolver via <see cref="SetResolver"/> (or the resolved camera's own <see cref="Camera.rect"/>
    /// is used) and per-player overlays position themselves without touching a single call site.
    /// </summary>
    public static class HudViewport
    {
        private static readonly Rect FullScreen = new(0f, 0f, 1f, 1f);

        /// <summary>Resolves the normalized (0..1) viewport rect for a player index.</summary>
        public delegate Rect Resolver(int player);

        private static Resolver custom;

        /// <summary>Installs a per-player viewport resolver (e.g. split-screen quadrants). Pass null to clear.</summary>
        public static void SetResolver(Resolver resolver)
        {
            custom = resolver;
        }

        /// <summary>Clears any installed resolver, reverting to the resolved camera rect / full screen.</summary>
        public static void ResetResolver()
        {
            custom = null;
        }

        /// <summary>
        /// Resolves the normalized viewport rect for a player. Uses an installed resolver first, then the resolved
        /// camera's <see cref="Camera.rect"/>, and finally the full screen — so a shared-camera setup is full-screen
        /// today while a multi-viewport setup Just Works later.
        /// </summary>
        public static Rect Resolve(int player)
        {
            if (custom != null)
            {
                return custom(player);
            }

            var cam = HudCameraProvider.Resolve(player);
            return cam != null ? cam.rect : FullScreen;
        }
    }
}
