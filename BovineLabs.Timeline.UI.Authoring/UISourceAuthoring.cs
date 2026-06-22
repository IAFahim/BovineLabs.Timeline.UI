using System;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.UI.Authoring
{
    public enum UISourceMode : byte
    {
        Binding,
        Player
    }

    [Serializable]
    public struct UISourceAuthoring
    {
        public UISourceMode Mode;

        [Range(0, UISource.NoPlayer - 1)] public int Player;

        public Target Route;

        public EntityLinkSchema Link;

        public readonly UISource ToComponent()
        {
            var player = (byte)math.clamp(this.Player, 0, UISource.NoPlayer - 1);

            return new UISource
            {
                Player = Mode == UISourceMode.Player ? player : UISource.NoPlayer,
                Route = Route,
                LinkKey = Link
            };
        }
    }
}