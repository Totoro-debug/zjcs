using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        private static void CheckPlayerEffectOrdering(ProfessionNativeBattlePlayerActor actor, Transform effect,
            Transform[] sourceNodes, Transform[] nodes, string label)
        {
            var native = actor.Renderer;
            int layer = native.GetComponent<MeshRenderer>().sortingLayerID;
            var groups = native.transform.Cast<Transform>()
                .Where(t => t.GetComponent<ProfessionNativeSceneMeshPart>())
                .Select(t => t.GetComponent<SortingGroup>()).Where(g => g && g.enabled && g.gameObject.activeInHierarchy).ToArray();
            Require(groups.Length > 0, "actual native body fragments for effect ordering");
            int mapped = 0, preserved = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                var renderer = nodes[i].GetComponent<Renderer>();
                if (!renderer) continue;
                var source = sourceNodes[i].GetComponent<Renderer>();
                Require(source && renderer.sortingLayerID == source.sortingLayerID, "effect source sorting layer remains intact");
                var follower = renderer.GetComponentInParent<SkeletonRendererFollower>(true);
                bool bound = follower && follower.SkeletonRenderer == native && follower.FollowZSpacing
                    && !string.IsNullOrEmpty(follower.SlotName) && source.sortingLayerID == layer;
                var slot = bound ? native.State.FindSlot(follower.SlotName) : null;
                if (slot == null)
                {
                    Require(renderer.sortingOrder == source.sortingOrder, "unbound/foreign-layer effect preserves its original sorting order");
                    preserved++; continue;
                }
                int draw = Array.FindIndex(native.State.DrawOrder, candidate => ReferenceEquals(candidate, slot));
                if (source.sortingOrder == 0)
                {
                    // Compare against independently located body fragments, not
                    // the adapter's computed key or its internal registration.
                    var cuts = native.SeparatorSlots.Select(s => Array.FindIndex(native.State.DrawOrder,
                        candidate => ReferenceEquals(candidate, s))).OrderBy(index => index).ToArray();
                    if (follower.ZFront)
                    {
                        int previous = cuts.Where(index => index < draw).DefaultIfEmpty(-1).Max();
                        int anchorOrder = groups.Single(g => g.sortingOrder == 2 * (previous + 1)).sortingOrder;
                        int followingOrder = groups.Single(g => g.sortingOrder == 2 * (draw + 1)).sortingOrder;
                        Require(renderer.sortingOrder > anchorOrder && renderer.sortingOrder < followingOrder,
                            "source front effect lies between its anchor and the next actual body fragment");
                    }
                    else Require(renderer.sortingOrder < groups.Single(g => g.sortingOrder == 2 * draw).sortingOrder,
                        "source back effect precedes its actual anchor body fragment");
                }
                else if (source.sortingOrder > 0)
                    Require(renderer.sortingOrder > groups.Max(g => g.sortingOrder), "positive original order retains priority over all body fragments");
                mapped++;
            }

            var slotRoot = native.transform.Find("PartSlot");
            var followers = slotRoot.Cast<Transform>()
                .Where(root => root.GetComponent<IAppearanceSkeletonPart>() == null)
                .SelectMany(root => root.Cast<Transform>())
                .Select(child => child.GetComponent<BaseSkeletonFollower>()).Where(f => f).ToArray();
            var bounds = native.GetComponent<MeshRenderer>().bounds;
            foreach (var renderer in native.GetComponentsInChildren<MeshRenderer>())
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
            float physicalTime = native.PlayerPhysicsTime;
            byte[] before = null;
            try
            {
                native.BindEffectFollowers(Array.Empty<BaseSkeletonFollower>());
                native.LateUpdateMesh();
                for (int i = 0; i < nodes.Length; i++)
                {
                    var renderer = nodes[i].GetComponent<Renderer>();
                    if (renderer) Require(renderer.sortingOrder == sourceNodes[i].GetComponent<Renderer>().sortingOrder,
                        "releasing native ordering restores every original renderer order");
                }
                before = Capture(bounds, label + "-source-effect-orders");
            }
            finally
            {
                native.BindEffectFollowers(followers);
                native.LateUpdateMesh();
            }
            var after = Capture(bounds, label + "-slot-effect-orders");
            int changed = ChangedEffectPixels(before, after);
            if (label.EndsWith("_008", StringComparison.Ordinal) || label.EndsWith("_029", StringComparison.Ordinal))
                Require(changed > 0, "actual clothing light/cape pixels respond to native slot ordering");
            Require(native.PlayerPhysicsTime == physicalTime, "source/slot GPU comparison advances neither animation nor physical time");
            if (label.EndsWith("_008", StringComparison.Ordinal))
            {
                var follower = followers.First(f => f.FollowZSpacing && f.ZFront && !string.IsNullOrEmpty(f.SlotName));
                var sourceSeparators = native.SeparatorSlots.ToArray();
                try
                {
                    follower.ZFront = false; native.LateUpdateMesh();
                    int draw = Array.FindIndex(native.State.DrawOrder, candidate => ReferenceEquals(candidate, native.State.FindSlot(follower.SlotName)));
                    var anchor = native.transform.Cast<Transform>().Where(t => t.GetComponent<ProfessionNativeSceneMeshPart>())
                        .Select(t => t.GetComponent<SortingGroup>()).Single(g => g && g.enabled
                            && g.gameObject.activeInHierarchy && g.sortingOrder == 2 * draw);
                    Require(follower.GetComponentsInChildren<Renderer>(true)
                        .Where(r => r.sortingLayerID == layer).All(r => r.sortingOrder < anchor.sortingOrder),
                        "back-side effect precedes the separately drawn actual anchor");
                    var back = Capture(bounds, label + "-back-effect-order");
                    Require(ChangedEffectPixels(after, back) > 0, "source clothing light has different actual GPU pixels in front of and behind its anchor");
                    Require(native.SeparatorSlots.SequenceEqual(sourceSeparators), "native back-side split preserves the original separator identities");
                }
                finally { follower.ZFront = true; native.LateUpdateMesh(); }
                Require(native.PlayerPhysicsTime == physicalTime, "front/back ordering never advances physical time");
            }
            Debug.Log($"Original player effect ordering passed: {label}; mapped={mapped}; original={preserved}; same-frame GPU changed={changed}; source priorities, slot placement and original-order restoration.");
        }

        private static int ChangedEffectPixels(byte[] before, byte[] after)
        {
            var a = new Texture2D(2, 2); var b = new Texture2D(2, 2);
            try
            {
                Require(a.LoadImage(before) && b.LoadImage(after) && a.width == b.width && a.height == b.height,
                    "same-frame GPU images decode at the same resolution");
                return a.GetPixels32().Zip(b.GetPixels32(), (x, y) => x.Equals(y) ? 0 : 1).Sum();
            }
            finally { UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        }
    }
}
