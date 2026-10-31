using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
    [Serializable]
    public class AppearanceMecanimPartHandler : AppearanceRendererPartHandler
    {
        private IProfessionAppearancePartMecanim mSkeletonMecanim;
        private object mLastClip;
        private List<IAppearanceSkeletonPart> mAnimList;
        private ProfessionPartClipApplied mOnClipApplied;
        private bool mAnimActive;
        private bool mHasAnim;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (mHasAnim) ActiveAnim();
        }

        protected override void OnDisable()
        {
            DeActiveAnim();
            base.OnDisable();
        }

        private void OnClipApplied(object clip, string name, int layerindex, float weight, float time, float lasttime, bool playsbackward)
        {
            if (ReferenceEquals(mLastClip, clip)) return;
            mLastClip = clip;
            foreach (var part in mAnimList) part.TryPlayAnim(name);
        }

        private void ActiveAnim()
        {
            if (mAnimActive) return;
            if (mSkeletonMecanim == null) mSkeletonMecanim = (IProfessionAppearancePartMecanim)mSkeletonRenderer;
            if (mOnClipApplied == null) mOnClipApplied = OnClipApplied;
            mSkeletonMecanim.OnClipApplied += mOnClipApplied;
            mAnimActive = true;
        }

        private void DeActiveAnim()
        {
            if (!mAnimActive) return;
            mAnimActive = false;
            mSkeletonMecanim.OnClipApplied -= mOnClipApplied;
        }

        protected override void OnSkinUpdate()
        {
            base.OnSkinUpdate();
            mHasAnim = false;
            if (mAnimList == null) mAnimList = new List<IAppearanceSkeletonPart>(LoadedSkeletonDict.Count);
            mAnimList.Clear();
            foreach (var item in LoadedSkeletonDict)
            {
                if (!item.Value.Part.HasAnim) continue;
                mAnimList.Add(item.Value.Part);
                mHasAnim = true;
            }
            if (mHasAnim) ActiveAnim(); else DeActiveAnim();
        }
    }
}
