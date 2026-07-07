using System;
using System.Collections;
using System.ComponentModel;
using BovineLabs.Anchor;
using BovineLabs.Timeline.UI.Data.ViewModel;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    [Transient]
    public class EssenceUIView : View<EssenceUIViewModel>, IDisposable
    {
        private static readonly Color TrackColor = new(0f, 0f, 0f, 0.35f);
        private static readonly Color FillColor = new(0.3f, 0.8f, 0.45f, 1f);
        private readonly GridView events;

        private readonly GridView intrinsics;
        private readonly GridView stats;

        public EssenceUIView()
            : base(new EssenceUIViewModel())
        {
            intrinsics = Build(MakeIntrinsic, BindIntrinsic, ViewModel.Intrinsics);
            stats = Build(MakeStat, BindStat, ViewModel.Stats);
            events = Build(MakeEvent, BindEvent, ViewModel.Events);

            Add(Section("Intrinsics", intrinsics));
            Add(Section("Stats", stats));
            Add(Section("Events", events));

            // Subscribe on attach, unsubscribe on detach (registered pair — no constructor subscription).
            // Without the attach counterpart a single detach/re-attach (tab switch, panel rebuild, a
            // UxmlViewTrack remount of a parent) left the view unsubscribed forever, silently showing
            // stale data. On attach we also resync itemsSource + Refresh so a re-attached view is current.
            RegisterCallback<AttachToPanelEvent>(_ => OnAttach());
            RegisterCallback<DetachFromPanelEvent>(_ => ViewModel.PropertyChanged -= OnPropertyChanged);
        }

        public void Dispose()
        {
            // Final safety unsubscribe for callers that dispose without a detach event firing.
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnAttach()
        {
            // Guard against double-subscribe if attach fires without an intervening detach.
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.PropertyChanged += OnPropertyChanged;

            intrinsics.itemsSource = ViewModel.Intrinsics;
            intrinsics.Refresh();
            stats.itemsSource = ViewModel.Stats;
            stats.Refresh();
            events.itemsSource = ViewModel.Events;
            events.Refresh();
        }

        private GridView Build(Func<VisualElement> make, Action<VisualElement, int> bind, IList source)
        {
            return new GridView
            {
                dataSource = ViewModel,
                selectionType = SelectionType.None,
                makeItem = make,
                bindItem = bind,
                itemsSource = source
            };
        }

        private static VisualElement Section(string title, VisualElement body)
        {
            var section = new VisualElement { style = { marginBottom = 8 } };
            section.Add(new Text(title) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            section.Add(body);
            return section;
        }

        private VisualElement MakeIntrinsic()
        {
            var row = new VisualElement { style = { marginBottom = 4 } };

            var header = new VisualElement
                { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            header.Add(new Text { name = "label" });
            header.Add(new Text { name = "value" });

            var track = new VisualElement { name = "track", style = { height = 8, backgroundColor = TrackColor } };
            var fill = new VisualElement
            {
                name = "fill",
                style = { height = Length.Percent(100), width = Length.Percent(0), backgroundColor = FillColor }
            };
            track.Add(fill);

            row.Add(header);
            row.Add(track);
            return row;
        }

        private void BindIntrinsic(VisualElement element, int index)
        {
            var row = ViewModel.Value.Intrinsics[index];
            element.Q<Text>("label").text = row.Label;
            element.Q<Text>("value").text = row.Display;
            element.Q("fill").style.width = Length.Percent(row.Fraction * 100f);
        }

        private VisualElement MakeStat()
        {
            var row = new VisualElement
                { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            row.Add(new Text { name = "label" });
            row.Add(new Text { name = "breakdown", style = { opacity = 0.6f } });
            row.Add(new Text { name = "value" });
            return row;
        }

        private void BindStat(VisualElement element, int index)
        {
            var row = ViewModel.Value.Stats[index];
            element.Q<Text>("label").text = row.Label;
            element.Q<Text>("breakdown").text = row.Breakdown;
            element.Q<Text>("value").text = row.Value;
        }

        private VisualElement MakeEvent()
        {
            var row = new VisualElement
                { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            row.Add(new Text { name = "label" });
            row.Add(new Text { name = "value" });
            return row;
        }

        private void BindEvent(VisualElement element, int index)
        {
            var row = ViewModel.Value.Events[index];
            element.style.opacity = row.Fade;
            element.Q<Text>("label").text = row.Label;
            element.Q<Text>("value").text = row.Display;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EssenceUIViewModel.Intrinsics):
                    intrinsics.itemsSource = ViewModel.Intrinsics;
                    intrinsics.Refresh();
                    break;
                case nameof(EssenceUIViewModel.Stats):
                    stats.itemsSource = ViewModel.Stats;
                    stats.Refresh();
                    break;
                case nameof(EssenceUIViewModel.Events):
                    events.itemsSource = ViewModel.Events;
                    events.Refresh();
                    break;
            }
        }
    }
}