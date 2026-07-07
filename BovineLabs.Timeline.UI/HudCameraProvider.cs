using UnityEngine;

namespace BovineLabs.Timeline.UI
{
    /// <summary>
    /// The SINGLE seam for "which camera renders player <c>n</c>". Today every player shares one camera
    /// (<see cref="Camera.main"/>), but nothing in the UI hardcodes that: minimap projection and any viewport-aware
    /// feature resolve their camera through here. A future split-screen / multi-viewport setup installs a per-player
    /// resolver via <see cref="SetResolver"/> and every call site follows without change.
    /// </summary>
    public static class HudCameraProvider
    {
        /// <summary>Resolves the camera that renders a given player index; return null to fall back to the shared camera.</summary>
        public delegate Camera Resolver(int player);

        private static Resolver custom;

        /// <summary>Installs a per-player camera resolver (e.g. split-screen). Pass null to clear.</summary>
        public static void SetResolver(Resolver resolver)
        {
            custom = resolver;
        }

        /// <summary>Clears any installed resolver, reverting to the shared <see cref="Camera.main"/>.</summary>
        public static void ResetResolver()
        {
            custom = null;
        }

        /// <summary>Resolves the camera for a player. Falls back to the shared <see cref="Camera.main"/> when no resolver is set.</summary>
        public static Camera Resolve(int player)
        {
            var cam = custom?.Invoke(player);
            return cam != null ? cam : Camera.main;
        }
    }
}
