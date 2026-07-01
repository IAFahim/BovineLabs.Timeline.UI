using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vex.HudShowcase.Editor
{
    /// <summary>
    /// Captures the actual GAME VIEW frame — including screen-space overlay UI (PanelRenderer / UIDocument / the Anchor
    /// HUD) — which the camera-only <c>unity-cli screenshot</c> path does NOT show. Uses
    /// <see cref="ScreenCapture.CaptureScreenshot(string)"/>, which writes the final composited frame. Output goes to
    /// the project-relative <c>Temp/GameViewCapture.png</c> (gitignored, stable path) so tooling can read it back.
    /// Works in play mode (captures the live overlay) and in edit mode (captures the scene render). The write is async
    /// (lands a frame or two later) — poll for the file before reading.
    /// </summary>
    public static class GameViewCapture
    {
        public const string OutputPath = "Temp/GameViewCapture.png";

        [MenuItem("Showcase/Capture Game View")]
        public static void Capture()
        {
            var full = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if (File.Exists(full))
            {
                File.Delete(full); // so a poller can tell the new frame landed
            }

            ScreenCapture.CaptureScreenshot(full);
            Debug.Log($"[Capture] Game view requested → {full} (lands in ~1-2 frames)");
        }
    }
}
