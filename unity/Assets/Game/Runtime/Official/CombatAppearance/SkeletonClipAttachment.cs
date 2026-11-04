using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
    // Original AppearanceSystem.SkeletonClipAttachment application logic.
    [ExecuteAlways]
    public sealed class SkeletonClipAttachment : MonoBehaviour
    {
        public GameObject SkeletonGo;
        public string SlotName;
        public string AttachmentName;
        private IProfessionFollowerAnimation mSkeletonComponent;
        private IProfessionFollowerSlot mSlot;
        private IProfessionClippingAttachment mClipAttachment;
        private ISkeletonClipSet mSkeletonClipSet;
        private bool mInit;
        private bool mEnableFollow;

        private void Awake()
        {
            if (SkeletonGo)
            {
                mSkeletonComponent = SkeletonGo.GetComponent<IProfessionFollowerAnimation>();
                if (SkeletonGo.TryGetComponent<AppearancePartHandler>(out var handler))
                    handler.OnSkinChanged.AddListener(OnSkinChanged);
            }
        }
        private void OnEnable() { if (!mInit) Initialize(); }
        private void OnSkinChanged() { Initialize(); }
        private void Initialize()
        {
            mSlot = null;
            mClipAttachment = null;
            mInit = false;
            var skeleton = mSkeletonComponent?.Skeleton;
            if (skeleton == null) return;
            mSlot = skeleton.FindSlot(SlotName);
            if (mSlot == null) { Debug.LogError($"不存在指定的槽位：{SlotName}"); return; }
            object attachment = skeleton.GetAttachment(mSlot.Index, AttachmentName);
            if (attachment == null) Debug.LogError($"不存在指定的Attachment:{AttachmentName}");
            else if (!(attachment is IProfessionClippingAttachment clip))
                Debug.LogError($"{AttachmentName} 不是剪切附件");
            else
            {
                mClipAttachment = clip;
                mInit = true;
                DisableFollow();
            }
        }
        private void Update()
        {
            if (mSlot == null || mClipAttachment == null || !mSlot.Bone.Active) return;
            if (ReferenceEquals(mClipAttachment, mSlot.Attachment))
            {
                if (!mEnableFollow) EnableFollow();
            }
            else if (mEnableFollow) DisableFollow();
        }
        private void EnableFollow()
        {
            mEnableFollow = true;
            if (mSkeletonClipSet == null) mSkeletonClipSet = GetComponent<ISkeletonClipSet>();
            mSkeletonClipSet?.SetClipHeight(0f);
        }
        private void DisableFollow()
        {
            mEnableFollow = false;
            mSkeletonClipSet?.SetClipHeight(float.MinValue);
        }
    }
}
