using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // User-confirmed native ordering adaptation. The original application owns
    // effect instances, transforms, activation and source follower configuration.
    public sealed partial class ProfessionNativeSceneRenderer : IProfessionAppearanceEffectRenderer
    {
        private readonly List<EffectDrawOrder> effectDrawOrders = new List<EffectDrawOrder>();

        public void BindEffectFollowers(IReadOnlyList<BaseSkeletonFollower> followers)
        {
            if (followers == null) throw new ArgumentNullException(nameof(followers));
            RestoreEffectDrawOrder(); effectDrawOrders.Clear();
            var seen = new HashSet<Renderer>();
            foreach (var follower in followers)
            {
                if (!(follower is SkeletonRendererFollower scene) || scene.SkeletonRenderer != this)
                    throw new NotSupportedException("Bind the original scene effect follower to its native skeleton first.");
                foreach (var renderer in follower.GetComponentsInChildren<Renderer>(true))
                {
                    if (!seen.Add(renderer)) throw new NotSupportedException("Bind overlapping original effect follower renderers explicitly.");
                    effectDrawOrders.Add(new EffectDrawOrder(follower, renderer));
                }
            }
        }

        private int EffectSlotDrawIndex(EffectDrawOrder item, IProfessionSkinClipSlot[] order)
        {
            if (!item.Follower || !item.Follower.FollowZSpacing || string.IsNullOrEmpty(item.Follower.SlotName)) return -1;
            var slot = State.FindSlot(item.Follower.SlotName);
            return slot == null ? -1 : Array.FindIndex(order, candidate => ReferenceEquals(candidate, slot));
        }

        private void AddEffectOrderingCuts(IProfessionSkinClipSlot[] order, SortedSet<int> cuts)
        {
            if (UsesUngroupedCloneOrder) return;
            foreach (var item in effectDrawOrders)
            {
                int draw = EffectSlotDrawIndex(item, order);
                // A back-side effect needs the anchor separated from the previous
                // drawable slots. Native extra parts use the original pool API.
                if (draw > 0 && !item.Follower.ZFront && item.Layer == Main.MeshRenderer.sortingLayerID)
                    cuts.Add(draw - 1);
            }
        }

        private void UpdateEffectDrawOrder(IProfessionSkinClipSlot[] order, MeshRenderer first)
        {
            int band = checked(2 * State.SlotCount + 3);
            foreach (var item in effectDrawOrders)
            {
                int draw = EffectSlotDrawIndex(item, order);
                if (draw < 0 || item.Layer != first.sortingLayerID) { item.Restore(); continue; }
                // Whole original sorting orders retain priority across all slots;
                // other original sorting layers and unbound effects stay intact.
                int insertion = checked(2 * draw + (item.Follower.ZFront ? 1 : -1));
                int key = checked(first.sortingOrder + (item.Order - first.sortingOrder) * band + insertion);
                if (key < short.MinValue || key > short.MaxValue)
                    throw new NotSupportedException("Original native effect ordering exceeds Unity's local sorting range.");
                item.Apply(key);
            }
        }

        private void RestoreEffectDrawOrder()
        {
            foreach (var item in effectDrawOrders) item.Restore();
        }

        private sealed class EffectDrawOrder
        {
            internal readonly BaseSkeletonFollower Follower;
            private readonly Renderer renderer;
            internal readonly int Layer, Order;
            private int? applied;
            internal EffectDrawOrder(BaseSkeletonFollower follower, Renderer renderer)
            { Follower = follower; this.renderer = renderer; Layer = renderer.sortingLayerID; Order = renderer.sortingOrder; }
            internal void Apply(int key)
            {
                if (!renderer) return;
                if (renderer.sortingLayerID != Layer)
                { Restore(); return; }
                if (renderer.sortingOrder != (applied ?? Order))
                    throw new NotSupportedException("Bind original effect sorting changes before replacing native slot order.");
                renderer.sortingOrder = key; applied = key;
            }
            internal void Restore()
            {
                if (renderer && applied.HasValue && renderer.sortingOrder == applied.Value) renderer.sortingOrder = Order;
                applied = null;
            }
        }
    }
}
