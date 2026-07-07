namespace BovineLabs.Timeline.UI.Editor
{
    using System.Collections.Generic;
    using System.Text;
    using BovineLabs.Core.Editor.Settings;
    using BovineLabs.Timeline.UI.Authoring;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// The ONE editor-time rule set for a <see cref="DataUISettings"/> asset. Shared by the "Vex/UI/Validate HUD Setup"
    /// menu (and, later, a build preprocessor). Mirrors the loud bake-time errors so a designer can catch broken HUD
    /// config before entering play mode. (The baker re-checks the structural rules directly because the Authoring
    /// assembly cannot reference this Editor assembly.)
    /// </summary>
    public static class DataUIValidation
    {
        /// <summary>Runs every rule against a settings asset and returns a human-readable list of problems (empty = OK).</summary>
        public static IReadOnlyList<string> Validate(DataUISettings s)
        {
            var errs = new List<string>();
            if (s == null)
            {
                errs.Add("No DataUISettings asset.");
                return errs;
            }

            if (s.Rows.Count > byte.MaxValue)
            {
                errs.Add($"{s.Rows.Count} rows > 255 — the slot index is a byte and would wrap.");
            }

            var seen = new HashSet<string>();
            for (var i = 0; i < s.Rows.Count; i++)
            {
                var r = s.Rows[i];
                if (r == null)
                {
                    errs.Add($"Row {i}: null entry.");
                    continue;
                }

                var slot = ResolveSlotName(r, i);
                if (!seen.Add(slot))
                {
                    errs.Add($"Row {i} ('{r.Label}'): duplicate slot '{slot}' — two rows bind to card-{slot}.");
                }

                if (r.Source.Mode == UISourceMode.Binding)
                {
                    errs.Add($"Row {i} ('{r.Label}'): Source Mode is Binding — a HUD row has no bound self, so it never resolves. Set Mode = Player.");
                }

                if (r.Bar != null && !r.Bar.HasMax)
                {
                    errs.Add($"Row {i} ('{r.Label}'): Bar source '{r.Bar.name}' has no Max stat — the bar renders empty.");
                }

                if (!string.IsNullOrEmpty(r.Format))
                {
                    try
                    {
                        _ = string.Format(r.Format, 0, 0);
                    }
                    catch (System.FormatException)
                    {
                        errs.Add($"Row {i} ('{r.Label}'): Format '{r.Format}' is invalid — use '{{0}}' (current) / '{{1}}' (max).");
                    }
                }
            }

            return errs;
        }

        /// <summary>The slot name the driver resolves for a row: explicit SlotName wins, else the list index.</summary>
        public static string ResolveSlotName(DataUISettings.Entry entry, int index) =>
            string.IsNullOrEmpty(entry.SlotName) ? index.ToString() : entry.SlotName;

        [MenuItem("Vex/UI/Validate HUD Setup")]
        private static void ValidateMenu()
        {
            var settings = EditorSettingsUtility.GetSettings<DataUISettings>();
            var errs = Validate(settings);

            if (errs.Count == 0)
            {
                Debug.Log($"[DataUI] HUD setup OK — {settings.Rows.Count} row(s), {settings.Panels.Count} panel(s), no problems found.", settings);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[DataUI] HUD setup has {errs.Count} problem(s):");
            foreach (var e in errs)
            {
                sb.Append(" • ").AppendLine(e);
            }

            Debug.LogError(sb.ToString(), settings);
        }
    }
}
