using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // AppearanceSystem.SkeletonBoneController: original registration and update
    // dispatch used by weapon prefabs; SDK types cross an application interface.
    [ExecuteAlways]
    public class SkeletonBoneController : MonoBehaviour
    {
        private readonly Dictionary<OverrideUpdateMode, List<SkeletonBoneOverride>> mOverrideDic
            = new Dictionary<OverrideUpdateMode, List<SkeletonBoneOverride>>();
        private IProfessionBoneOverrideAnimation mSkeletonAnimation;

        private void Awake() => mSkeletonAnimation = GetComponent<IProfessionBoneOverrideAnimation>();
        private void OnEnable()
        {
            if (mSkeletonAnimation == null) return;
            mSkeletonAnimation.UpdateLocal += OnUpdateLocal;
            mSkeletonAnimation.UpdateComplete += OnUpdateComplete;
        }
        private void OnDisable()
        {
            if (mSkeletonAnimation == null) return;
            mSkeletonAnimation.UpdateLocal -= OnUpdateLocal;
            mSkeletonAnimation.UpdateComplete -= OnUpdateComplete;
        }
        private void OnUpdateLocal(IProfessionBoneOverrideAnimation animated) => OnUpdate(OverrideUpdateMode.Local, animated);
        private void OnUpdateComplete(IProfessionBoneOverrideAnimation animated) => OnUpdate(OverrideUpdateMode.Complete, animated);
        private void OnUpdate(OverrideUpdateMode updateMode, IProfessionBoneOverrideAnimation animated)
        {
            if (!mOverrideDic.TryGetValue(updateMode, out var value)) return;
            foreach (SkeletonBoneOverride item in value) item.OverrideUpdate(animated);
        }
        public void RegisterOverride(SkeletonBoneOverride boneOverride)
        {
            if (!mOverrideDic.TryGetValue(boneOverride.UpdateMode, out var value))
            {
                value = new List<SkeletonBoneOverride>();
                mOverrideDic.Add(boneOverride.UpdateMode, value);
            }
            value.Add(boneOverride);
        }
        public void UnregisterOverride(SkeletonBoneOverride boneOverride) => mOverrideDic[boneOverride.UpdateMode].Remove(boneOverride);
    }
}
