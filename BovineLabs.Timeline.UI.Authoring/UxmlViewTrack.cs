namespace BovineLabs.Timeline.UI.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(UxmlViewClip))]
    [TrackColor(0.2f, 0.9f, 0.5f)]
    [DisplayName("DOTS/UI/UXML View Track")]
    public class UxmlViewTrack : DOTSTrack
    {
    }
}
