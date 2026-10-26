using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>The scrolling methods of the official FightEndReportSkillTabView.
    /// Initialize is the offline adapter for its UIBase OnEnableAndInited lifecycle.</summary>
    public sealed class WorldBattleReportSkillScrollSync : MonoBehaviour
    {
        [SerializeField] private ScrollRect mTitleScrollRect;
        [SerializeField] private LoopVerticalScrollRect mItemScrollRect;
        [SerializeField] private WorldStateSwitcher<bool> mGMStateSwitcher;
        private bool initialized;

        // Original FightEndReportSkillTabView.Refresh applies the same GM state to its headers.
        public void SetGMState(bool gm) => mGMStateSwitcher.SetState(gm);

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            mItemScrollRect.onValueChanged.AddListener(OnItemScrollRectValueChanged);
            mTitleScrollRect.horizontal = false;
            SetTitleHorizontalNormalizedPosition(0f);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            mItemScrollRect.onValueChanged.RemoveListener(OnItemScrollRectValueChanged);
            initialized = false;
        }

        public void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(mItemScrollRect.content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(mTitleScrollRect.content);
            SyncTitleByItemAnchoredX();
        }

        private void OnItemScrollRectValueChanged(Vector2 position)
        {
            ClampItemHorizontalAnchorX();
            SyncTitleByItemAnchoredX();
        }

        private void ClampItemHorizontalAnchorX()
        {
            if (mItemScrollRect == null || mItemScrollRect.content == null || mItemScrollRect.viewport == null) return;
            float overflow = Mathf.Max(mItemScrollRect.content.rect.width - mItemScrollRect.viewport.rect.width, 0f);
            if (overflow <= 0f) return;
            Vector2 position = mItemScrollRect.content.anchoredPosition;
            float x = Mathf.Clamp(position.x, -overflow, 0f);
            if (!Mathf.Approximately(x, position.x))
            {
                position.x = x;
                mItemScrollRect.content.anchoredPosition = position;
            }
        }

        private void SetTitleHorizontalNormalizedPosition(float position)
        {
            if (mTitleScrollRect != null && mTitleScrollRect.content != null && mTitleScrollRect.viewport != null)
                mTitleScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(position);
        }

        private void SyncTitleByItemAnchoredX()
        {
            if (mTitleScrollRect == null || mTitleScrollRect.content == null || mTitleScrollRect.viewport == null
                || mItemScrollRect == null || mItemScrollRect.content == null || mItemScrollRect.viewport == null) return;
            float itemOverflow = Mathf.Max(mItemScrollRect.content.rect.width - mItemScrollRect.viewport.rect.width, 0f);
            float titleOverflow = Mathf.Max(mTitleScrollRect.content.rect.width - mTitleScrollRect.viewport.rect.width, 0f);
            if (itemOverflow <= 0f)
            {
                Vector2 position = mTitleScrollRect.content.anchoredPosition;
                position.x = 0f;
                mTitleScrollRect.content.anchoredPosition = position;
            }
            else if (titleOverflow > 0f)
            {
                float progress = Mathf.Clamp01(-mItemScrollRect.content.anchoredPosition.x / itemOverflow);
                Vector2 position = mTitleScrollRect.content.anchoredPosition;
                position.x = -progress * titleOverflow;
                mTitleScrollRect.content.anchoredPosition = position;
            }
        }
    }
}
