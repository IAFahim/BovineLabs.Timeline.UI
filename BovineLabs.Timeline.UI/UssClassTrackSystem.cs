using System.Collections.Generic;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using UnityEngine.UIElements;

namespace BovineLabs.Timeline.UI
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(UxmlViewTrackSystem))]
    [WorldSystemFilter(
        WorldSystemFilterFlags.LocalSimulation |
        WorldSystemFilterFlags.ClientSimulation |
        WorldSystemFilterFlags.Presentation)]
    public sealed partial class UssClassTrackSystem
        : ReversibleEffectSystem<UssClassData, UssClassTrackSystem.AppliedClass, UssClassCleanup>
    {
        private readonly ClassRefCounts refCounts = new();

        protected override bool TryApply(VisualElement root, Entity entity, in UssClassData data,
            out AppliedClass inverse)
        {
            var target = data.TargetId.IsEmpty ? root : root.Q(data.TargetId.ToString());
            if (target == null)
            {
                inverse = default;
                return false;
            }

            var className = data.ClassName.ToString();
            if (string.IsNullOrEmpty(className)) // AddToClassList("") throws
            {
                inverse = default;
                return false;
            }

            // Ref-count per (element, class) so overlapping clips compose instead of corrupting each other's revert:
            // physically add the class only on the 0->1 transition, and only when it wasn't already present
            // (UXML-authored classes must survive every clip's exit).
            var preExisting = target.ClassListContains(className);
            var key = (object)(target, className);
            if (refCounts.TryAcquire(key, preExisting))
                target.AddToClassList(className);

            inverse = new AppliedClass(target, className, key);
            return true;
        }

        protected override void Revert(AppliedClass inverse)
        {
            if (inverse.Element == null)
                return;

            // Remove only on the 1->0 transition, and only if this ref-count group added the class.
            if (refCounts.Release(inverse.Key))
                inverse.Element.RemoveFromClassList(inverse.ClassName);
        }

        protected override string DescribeFailure(in UssClassData data)
        {
            if (data.ClassName.IsEmpty)
                return $"empty ClassName (TargetId '{data.TargetId.ToString()}').";

            return $"TargetId '{data.TargetId.ToString()}' not found under root.";
        }

        protected override void OnCleanup()
        {
            refCounts.Clear();
        }

        public readonly struct AppliedClass
        {
            public readonly VisualElement Element;
            public readonly string ClassName;
            public readonly object Key;

            public AppliedClass(VisualElement element, string className, object key)
            {
                Element = element;
                ClassName = className;
                Key = key;
            }
        }

        /// <summary>
        /// Pure, testable ref-count bookkeeping for overlapping USS-class effects, keyed by an opaque object
        /// pairing (element + class). The class's pre-existing presence is captured on the first acquire and
        /// reused on the last release, so it does not matter what later overlapping clips observe.
        /// Public (not internal) only so the separate Tests assembly can exercise it without InternalsVisibleTo.
        /// </summary>
        public sealed class ClassRefCounts
        {
            private readonly Dictionary<object, Entry> counts = new();

            public int Count => this.counts.Count;

            /// <summary>Acquires a hold on <paramref name="key"/>. Returns true when the caller should physically apply the class.</summary>
            /// <param name="preExisting">Whether the class was already present before any clip touched it. Honored only on the 0->1 transition.</param>
            public bool TryAcquire(object key, bool preExisting)
            {
                if (this.counts.TryGetValue(key, out var entry))
                {
                    entry.Count++;
                    this.counts[key] = entry;
                    return false; // already held — never physically re-apply
                }

                this.counts[key] = new Entry { Count = 1, PreExisting = preExisting };
                return !preExisting; // apply only if it wasn't already there
            }

            /// <summary>Releases a hold on <paramref name="key"/>. Returns true when the caller should physically remove the class.</summary>
            public bool Release(object key)
            {
                if (!this.counts.TryGetValue(key, out var entry))
                    return false;

                entry.Count--;
                if (entry.Count <= 0)
                {
                    this.counts.Remove(key);
                    return !entry.PreExisting; // remove only if this group added it
                }

                this.counts[key] = entry;
                return false;
            }

            public void Clear()
            {
                this.counts.Clear();
            }

            private struct Entry
            {
                public int Count;
                public bool PreExisting;
            }
        }
    }
}
