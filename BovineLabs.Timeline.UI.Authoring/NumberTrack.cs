using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.UI.Authoring
{
    [Serializable]
    [TrackClipType(typeof(NumberClip))]
    [TrackColor(1f, 1f, 1f)]
    [DisplayName("DOTS/Number Track")]
    public class NumberTrack : DOTSTrack
    {
    }
}