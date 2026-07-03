using System;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.UI.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("Link")]
        public EntityLinkSchema link;

        public UISourceMode Mode;

        [Range(0, UISource.NoPlayer - 1)] public int Player;

        public Target Route;

        public readonly UISource ToComponent(IBaker baker)
        {
            var player = (byte)math.clamp(Player, 0, UISource.NoPlayer - 1);

            return new UISource
            {
                Player = Mode == UISourceMode.Player ? player : UISource.NoPlayer,
                Link = EntityLinkAuthoringUtility.BakeRef(baker, link, Route),
            };
        }
    }
}