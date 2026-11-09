using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneRenderer : IProfessionAfterimageTarget
    {
        public Vector3 GetCapturePosition(string boneName, Vector3 worldOffset)
            => ProfessionNativeAfterimageTargets.Position(this, this, boneName, worldOffset);
        public void CollectRenderers(List<MeshRenderer> renderers, Comparer<MeshRenderer> comparer)
            => ProfessionNativeAfterimageTargets.Collect(this, renderers, comparer);
    }

    public sealed partial class BattleNativeMonsterRenderer : IProfessionAfterimageTarget
    {
        public Vector3 GetCapturePosition(string boneName, Vector3 worldOffset)
            => ProfessionNativeAfterimageTargets.Position(this, this, boneName, worldOffset);
        public void CollectRenderers(List<MeshRenderer> renderers, Comparer<MeshRenderer> comparer)
            => ProfessionNativeAfterimageTargets.Collect(this, renderers, comparer);
    }

    internal static class ProfessionNativeAfterimageTargets
    {
        internal static Vector3 Position(Component root, IProfessionFollowerRenderer renderer,
            string boneName, Vector3 worldOffset)
            => root.transform.TransformPoint(new Vector3(0, renderer.Skeleton.FindBone(boneName).WorldY)) + worldOffset;

        // Same selection and order as AppearancePortraitUtils: separated body
        // meshes (or the root), then the loaded appearance parts, sorted by Z.
        internal static void Collect(Component root, List<MeshRenderer> renderers, Comparer<MeshRenderer> comparer)
        {
            renderers.Clear();
            var separator = root.GetComponent<DynamicSkeletonRenderSeparator>();
            if (separator)
            {
                foreach (var part in separator.PartsRenderers)
                    if (part.MeshRenderer) renderers.Add(part.MeshRenderer);
            }
            else if (root.TryGetComponent(out MeshRenderer main)) renderers.Add(main);
            var appearance = root.GetComponent<AppearanceRendererPartHandler>();
            if (appearance)
                foreach (var part in appearance.LoadedSkeletonDict.Values)
                {
                    var renderer = part.Part.SkeletonTarget.GetComponent<MeshRenderer>();
                    if (renderer) renderers.Add(renderer);
                }
            renderers.Sort(comparer);
        }
    }
}
