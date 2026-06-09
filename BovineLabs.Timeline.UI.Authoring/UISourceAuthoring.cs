using System;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.UI.Data;
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

        [Min(0)] public int Player;

        public Target Route;

        public EntityLinkSchema Link;

        public readonly UISource ToComponent()
        {
            return new UISource
            {
                Player = Mode == UISourceMode.Player ? (byte)Player : UISource.NoPlayer,
                Route = Route,
                LinkKey = Link
            };
        }
    }
}