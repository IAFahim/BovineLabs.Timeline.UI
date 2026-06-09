// just for testing. Hate thisssss Only timeline

using BovineLabs.Anchor;
using BovineLabs.Anchor.Elements;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI.Tests
{
    [ExecuteAlways]
    public class AnimationListHarness : MonoBehaviour
    {
        private NativeArray<RowsViewModel.Data.Row> rows;

        private void OnEnable()
        {
            rows = new NativeArray<RowsViewModel.Data.Row>(4, Allocator.Persistent)
            {
                [0] = new RowsViewModel.Data.Row { RawLabel = "Idle", RawValue = 0 },
                [1] = new RowsViewModel.Data.Row { RawLabel = "Run", RawValue = 1 },
                [2] = new RowsViewModel.Data.Row { RawLabel = "Attack", RawValue = 2 },
                [3] = new RowsViewModel.Data.Row { RawLabel = "You", RawValue = 2 }
            };

#if UNITY_6000_5_OR_NEWER
            var renderer = GetComponent<PanelRenderer>();
            if (renderer != null) renderer.RegisterUIReloadCallback(OnUIReload);
#else
            var doc = this.GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                var list = doc.rootVisualElement.Q<AnchorGridView>("list");
                if (list != null)
                {
                    list.itemsSource = new UIArray<RowsViewModel.Data.Row>(this.rows);
                    list.Refresh();
                }
            }
#endif
        }

        private void OnDisable()
        {
            if (rows.IsCreated) rows.Dispose();

#if UNITY_6000_5_OR_NEWER
            var renderer = GetComponent<PanelRenderer>();
            if (renderer != null) renderer.UnregisterUIReloadCallback(OnUIReload);
#endif
        }

#if UNITY_6000_5_OR_NEWER
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            var list = root.Q<AnchorGridView>("list");
            if (list == null) return;

            list.itemsSource = new UIArray<RowsViewModel.Data.Row>(rows);
            list.Refresh();
        }
#endif
    }
}