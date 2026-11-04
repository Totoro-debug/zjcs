using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public abstract class SkeletonBoneOverride : MonoBehaviour
    {
        public SkeletonBoneController Controller;
        public string BoneName;
        private IProfessionLocalOverrideBone mBone;
        public abstract OverrideUpdateMode UpdateMode { get; }
        protected virtual void OnInitBone(IProfessionLocalOverrideBone bone) { }
        protected abstract void OnUpdateBone(IProfessionLocalOverrideBone bone);
        protected virtual void OnResetBone(IProfessionLocalOverrideBone bone) { }
        protected virtual void OnEnable() => RegisterOverride();
        protected virtual void OnDisable() => UnRegisterOverride();
        public void OverrideUpdate(IProfessionBoneOverrideAnimation animated)
        {
            if (mBone == null)
            {
                mBone = animated.FindOverrideBone(BoneName);
                if (mBone != null) OnInitBone(mBone);
            }
            if (mBone != null) OnUpdateBone(mBone);
        }
        private void RegisterOverride()
        {
            if (Controller != null) Controller.RegisterOverride(this);
        }
        private void UnRegisterOverride()
        {
            if (Controller == null) return;
            Controller.UnregisterOverride(this);
            if (mBone != null) OnResetBone(mBone);
        }
    }
}
