namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(UITextRevealClip))]
    [TrackColor(0.9f, 0.1f, 0.5f)]
    [DisplayName("DOTS/UI/Text Reveal Track")]
    public class UITextRevealTrack : DOTSTrack
    {
    }
}
