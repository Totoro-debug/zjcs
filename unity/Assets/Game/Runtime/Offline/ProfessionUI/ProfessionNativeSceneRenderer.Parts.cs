using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneRenderer
    {
        private readonly List<ScenePartPlacement> sceneParts = new List<ScenePartPlacement>();

        // The original part handler owns separator slots and the original
        // follower owns transforms/visibility. This lease only binds native
        // renderer ordering and hides old parts while its skeleton is released.
        public IDisposable BindScenePart(IAppearanceSkeletonPart part)
        {
            Initialize(false, false);
            if (part == null || !part.GameObject || !part.SkeletonTarget) throw new ArgumentNullException(nameof(part));
            if (!part.SkeletonTarget.GetComponent<ProfessionNativeSceneWeaponRenderer>())
                throw new NotSupportedException("Bind the actual native independent scene part renderer first.");
            if (string.IsNullOrEmpty(part.SkeletonFollower.SlotName) || part.SkeletonFollower.ZFront)
                throw new NotSupportedException("Bind the original non-weapon or front-slot placement before drawing this part.");
            if (sceneParts.Any(p => p.Part.GameObject && p.Part.SkeletonFollower.SlotName == part.SkeletonFollower.SlotName))
                throw new NotSupportedException("Bind ordering between multiple original parts at the same slot first.");
            var placement = new ScenePartPlacement(this, part);
            sceneParts.Add(placement);
            return placement;
        }

        private void UpdateScenePartOrder(IProfessionSkinClipSlot[] order, int[] cuts, MeshRenderer first)
        {
            foreach (var item in sceneParts.ToArray())
            {
                if (!item.Part.GameObject) { item.Dispose(); continue; }
                var slot = State.FindSlot(item.Part.SkeletonFollower.SlotName);
                int draw = Array.FindIndex(order, candidate => ReferenceEquals(candidate, slot));
                if (draw < 0 || !cuts.Contains(draw))
                    throw new InvalidOperationException("The original part handler must include the independent weapon's separator slot.");
                // Current integrated weapons use a source point marker. An
                // after-marker key is also before its next drawable slot. Do
                // not infer placement for another visible anchor or ZFront.
                if (slot.Attachment != null && !(slot.Attachment is ProfessionNativePointAttachment))
                    throw new NotSupportedException("Bind original independent weapon placement at a drawable anchor.");
                var renderer = item.Part.SkeletonTarget.GetComponent<MeshRenderer>();
                if (renderer.sortingLayerID != first.sortingLayerID || renderer.sortingOrder != first.sortingOrder)
                    throw new NotSupportedException("Bind original independent weapon and body sorting differences first.");
                int key = checked(first.sortingOrder + 2 * draw + 1);
                if (key < short.MinValue || key > short.MaxValue) throw new NotSupportedException("Native independent part ordering exceeds Unity's local range.");
                item.Group.sortingLayerID = first.sortingLayerID; item.Group.sortingOrder = key;
            }
        }

        private void SuspendSceneParts() { foreach (var item in sceneParts) item.Suspend(); }
        private void ResumeSceneParts() { foreach (var item in sceneParts) item.Resume(); }
        private void DisposeSceneParts() { foreach (var item in sceneParts.ToArray()) item.Dispose(); }

        private sealed class ScenePartPlacement : IDisposable
        {
            private ProfessionNativeSceneRenderer owner;
            private bool suspended, resumeActive;
            internal IAppearanceSkeletonPart Part { get; }
            internal SortingGroup Group { get; }
            private readonly ProfessionNativeScenePartOrder order;
            internal ScenePartPlacement(ProfessionNativeSceneRenderer owner, IAppearanceSkeletonPart part)
            {
                var body = owner.GetComponentInParent<SortingGroup>(true);
                var parent = part.GameObject.transform.parent;
                if (!body || !parent || parent.GetComponentInParent<SortingGroup>(true) != body)
                    throw new InvalidOperationException("Native body and independent weapon must share the original character's sorting group.");
                order = part.GameObject.GetComponent<ProfessionNativeScenePartOrder>();
                if (!order)
                {
                    if (part.GameObject.GetComponent<SortingGroup>())
                        throw new NotSupportedException("Bind an existing original independent part sorting group first.");
                    order = part.GameObject.AddComponent<ProfessionNativeScenePartOrder>();
                }
                if (order.Owner) throw new InvalidOperationException("The independent native part is already attached to another skeleton.");
                this.owner = owner; Part = part;
                Group = order.Group; order.Owner = owner; Group.enabled = true;
            }
            internal void Suspend()
            {
                if (suspended || !Part.GameObject) return;
                suspended = true; resumeActive = Part.GameObject.activeSelf;
                Part.GameObject.SetActive(false);
            }
            internal void Resume()
            {
                if (!suspended) return;
                suspended = false;
                if (Part.GameObject && resumeActive) Part.GameObject.SetActive(true);
            }
            public void Dispose()
            {
                if (ReferenceEquals(owner, null)) return;
                owner.sceneParts.Remove(this); owner = null;
                if (order) order.Owner = null;
                // Keep the native ordering object with its pooled part. A
                // same-frame return/rent must not add a second SortingGroup
                // while Unity still defers destruction of the first one.
                if (Group) Group.enabled = false;
            }
        }
    }
}
