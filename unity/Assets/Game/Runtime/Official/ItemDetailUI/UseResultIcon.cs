using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public interface IUseResultIconBindings
    {
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        bool IsTreasure(int itemId);
        bool ShowGained(int fromId);
        bool IsGotItem(int itemId);
        void ApplyTreasureOwnership(ItemIcon icon, int itemId, bool showGained);
        object ProbabilityPercent(double probability);
    }

    // Original use-result cell. The ordinary 41021 path clears the loop without
    // instantiating this prefab; its original referenced hierarchy is retained.
    public sealed class UseResultIcon : UIBase
    {
        private ItemIcon mItemIcon;
        [SerializeField] private RectTransform mIconSlot;
        [SerializeField] private WorldMultiLanguageText mProbabilityText;
        [SerializeField] private float mProbabilityReservedHeight = 22;
        private double? probability;
        private IUseResultIconBindings bindings;
        public int ClassId { get; private set; }
        public long Count { get; private set; }
        public int FromId { get; private set; }
        public bool ShowCount { get; private set; }
        public void Bind(IUseResultIconBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));
        protected override void Start() { base.Start(); InitWrapper(null); }
        protected override void OnEnableAndInited() { base.OnEnableAndInited(); Refresh(); }
        protected override void OnDestroy() { Clear(); base.OnDestroy(); }
        public void SetItem(int classId, long count, int fromId, bool showCount)
        {
            ClassId = classId; Count = count; FromId = fromId; ShowCount = showCount;
            Clear(); Refresh();
        }
        public void SetProbability(double? value) { probability = value; RefreshProbability(); }
        private void Refresh()
        {
            if (bindings == null) throw new InvalidOperationException("Original use-result icon bindings are absent.");
            RefreshProbability();
            if (!gameObject.activeInHierarchy || mItemIcon) return;
            mItemIcon = bindings.GetIcon(ClassId, mIconSlot ? mIconSlot : transform);
            mItemIcon.Init(ClassId).SetClickPopupInfoDialog();
            if (!ShowCount || Count <= 0) mItemIcon.HideText(); else mItemIcon.SetCount(Count);
            bool show = bindings.ShowGained(FromId);
            if (bindings.IsTreasure(ClassId)) bindings.ApplyTreasureOwnership(mItemIcon, ClassId, show);
            else mItemIcon.SetAttachment(ItemIconAttachmentType.GotCenterTip, show && bindings.IsGotItem(ClassId));
        }
        private void RefreshProbability()
        {
            if (mIconSlot)
            {
                var offset = mIconSlot.offsetMin;
                offset.y = probability.HasValue ? mProbabilityReservedHeight : 0;
                mIconSlot.offsetMin = offset;
            }
            if (!mProbabilityText) return;
            mProbabilityText.gameObject.SetActive(probability.HasValue);
            if (probability.HasValue) mProbabilityText.SetKey("{0}", bindings.ProbabilityPercent(probability.Value));
        }
        private void Clear()
        {
            if (!mItemIcon) return;
            bindings.RecycleIcon(mItemIcon); mItemIcon = null;
        }
    }
}
