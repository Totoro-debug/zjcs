using System;
using UnityEngine;

namespace Zjcs.Official.PartnersUI
{
    public interface IPartnerLazyUiGraphic
    {
        string StartingAnimation { get; }
        bool StartingLoop { get; }
        bool IsValid { get; }
        bool TryGetAnimation(string name);
        bool ContainsSplitAnimation(string name);
        void PlayAnimation(string name, bool loop);
    }
    // The original application UI branch. Split bytes are packaged locally;
    // channels are still created on demand by the native animation producer.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-300)]
    public sealed class SpineLazyAnimLoad : MonoBehaviour
    {
        public enum ELazyLoadSpineType { SkeletonGraphics = 1, SkeletonAnimation, SkeletonMecanim, None }
        [SerializeField] private ELazyLoadSpineType mLazyLoadSpineType = ELazyLoadSpineType.None;
        [SerializeField] private ScriptableObject mSplitBinarySo;
        private IPartnerLazyUiGraphic mSkeletonGraphic;
        public ELazyLoadSpineType LazyLoadSpineType => mLazyLoadSpineType;
        public ScriptableObject SplitBinarySo => mSplitBinarySo;
        private void Awake()
        {
            switch (mLazyLoadSpineType)
            {
                case ELazyLoadSpineType.SkeletonGraphics: OnAwakeGraphics(); break;
                case ELazyLoadSpineType.None: break;
                default: throw new NotSupportedException("Bind the original non-UI lazy animation branch before using this prefab.");
            }
        }
        private void OnAwakeGraphics()
        {
            mSkeletonGraphic = GetComponent<IPartnerLazyUiGraphic>();
            if (mSkeletonGraphic == null) return;
            string name = mSkeletonGraphic.StartingAnimation;
            if (string.IsNullOrEmpty(name)) return;
            if (!mSkeletonGraphic.TryGetAnimation(name))
            {
                string message = " SpineLazyAnimLoad找不到SkeletonGraphic的起始动画 " + name;
                if (SplitBinarySo && mSkeletonGraphic.ContainsSplitAnimation(name))
                    Debug.LogError(message + " SplitBinarySo包含了该动画名，但是加载失败", gameObject);
                else Debug.LogWarning(message + " （预制体上序列化值有问题）", gameObject);
            }
            else if (mSkeletonGraphic.StartingLoop && mSkeletonGraphic.IsValid)
                mSkeletonGraphic.PlayAnimation(name, true);
        }
        private void OnDestroy()
        {
            switch (mLazyLoadSpineType)
            {
                case ELazyLoadSpineType.SkeletonGraphics: mSkeletonGraphic = null; break;
                case ELazyLoadSpineType.None: break;
                default: throw new ArgumentOutOfRangeException($"unexpected lazy load spine type {mLazyLoadSpineType}");
            }
        }
    }
}
