// Migrated from client-source/WorldStageInteractionPreviewContentLayoutElement.cs.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldStageInteractionPreviewContentLayoutElement : UIBehaviour, ILayoutElement
    {
        public VerticalLayoutGroup mVerticalLayoutGroup;
        public float mMaxHeight, mMinHeight;
        public int mLayoutPriority;
        private float mPreferredHeight;
        private bool mIsInit, mIsDirty;

        public float minWidth => -1f;
        public float preferredWidth => -1f;
        public float flexibleWidth => -1f;
        public float minHeight { get { CalculatePreferredHeight(); return mIsInit ? mPreferredHeight : mMaxHeight; } }
        public float preferredHeight { get { CalculatePreferredHeight(); return mIsInit ? mPreferredHeight : mMaxHeight; } }
        public float flexibleHeight => -1f;
        public int layoutPriority => mLayoutPriority;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { mIsDirty = true; CalculatePreferredHeight(); }
        protected override void OnEnable() { base.OnEnable(); mIsDirty = true; if (mIsInit) Refresh(); }
        protected override void OnDisable() { base.OnDisable(); mIsDirty = true; }
        public void Init() { mIsInit = true; Refresh(); }
        public void Refresh()
        {
            if (!IsActive()) return;
            mIsDirty = true;
            CalculatePreferredHeight();
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void CalculatePreferredHeight()
        {
            if (!mIsDirty || mVerticalLayoutGroup == null) return;
            mPreferredHeight = mVerticalLayoutGroup.padding.top + mVerticalLayoutGroup.padding.bottom;
            foreach (RectTransform child in mVerticalLayoutGroup.transform)
            {
                float preferred = -1f;
                foreach (ILayoutElement element in child.GetComponents<ILayoutElement>())
                    preferred = Mathf.Max(preferred, element.preferredHeight);
                mPreferredHeight += preferred > 0 ? preferred : child.rect.height;
            }
            mPreferredHeight += mVerticalLayoutGroup.spacing * (mVerticalLayoutGroup.transform.childCount - 1);
            mPreferredHeight = Mathf.Clamp(mPreferredHeight, mMinHeight, mMaxHeight);
            mIsDirty = false;
        }
    }
}
