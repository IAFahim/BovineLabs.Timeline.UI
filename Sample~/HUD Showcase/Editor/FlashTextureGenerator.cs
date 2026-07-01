using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vex.HudShowcase.Editor
{
    /// <summary>
    /// Generates the damage-flash silhouette: a WHITE-rgb copy of the blade fill that keeps its alpha, so the flash
    /// paints only the blade shape (not a rectangle). Needed because UI Toolkit's -unity-background-image-tint-color is
    /// a MULTIPLY — white-tinting the dark-red blade texture only darkens it; a dedicated white texture is required.
    /// Run once via the menu (the builder also calls EnsureExists).
    /// </summary>
    public static class FlashTextureGenerator
    {
        private const string Src = "Assets/HudShowcase/Textures/Arvex_HP_fill_cropped.png";
        public const string Dst = "Assets/HudShowcase/Textures/Arvex_HP_flash.png";

        [MenuItem("Showcase/Generate Flash Silhouette Texture")]
        public static void Generate()
        {
            if (!File.Exists(Src))
            {
                Debug.LogError($"[Flash] source missing: {Src}");
                return;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(Src))) // LoadImage → always readable
            {
                Debug.LogError($"[Flash] could not decode {Src}");
                Object.DestroyImmediate(tex);
                return;
            }

            var px = tex.GetPixels32();
            for (var i = 0; i < px.Length; i++)
            {
                px[i].r = 255;
                px[i].g = 255;
                px[i].b = 255; // white rgb, keep the blade's alpha
            }

            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Dst, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(Dst, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(Dst) is TextureImporter imp)
            {
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }

            Debug.Log($"[Flash] wrote {Dst}");
        }

        /// <summary>Generate the flash texture if it is missing (called by the showcase builder).</summary>
        public static void EnsureExists()
        {
            if (!File.Exists(Dst))
            {
                Generate();
            }
        }
    }
}
