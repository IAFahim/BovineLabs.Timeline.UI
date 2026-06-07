namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(UssClassClip))]
    [TrackColor(0.8f, 0.4f, 0.1f)]
    [DisplayName("DOTS/UI/USS Class Track")]
    public class UssClassTrack : DOTSTrack
    {
    }
}
