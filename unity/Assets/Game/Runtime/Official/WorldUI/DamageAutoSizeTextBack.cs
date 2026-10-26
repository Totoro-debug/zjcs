using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>Official DamageAutoSizeTextBack layout; the local language binding invokes OnRefresh.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageAutoSizeTextBack : MonoBehaviour
    {
        private SpriteRenderer mBackground;
        private TMP_Text mTMPText;
        private int mAlignMode;
        private float mPaddingX, mPaddingY, mMinWidth, mMaxWidth, mMinHeight, mMaxHeight, mAnchorLocalX;

        public void Bind(SpriteRenderer background, TMP_Text text, int align, float paddingX, float paddingY,
            float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            mBackground = background;
            mTMPText = text;
            mAlignMode = align;
            mPaddingX = paddingX;
            mPaddingY = paddingY;
            mMinWidth = minWidth;
            mMaxWidth = maxWidth;
            mMinHeight = minHeight;
            mMaxHeight = maxHeight;
            CacheAnchorEdge();
        }

        private void CacheAnchorEdge()
        {
            if (mBackground == null) return;
            float halfWidth = mBackground.size.x * 0.5f * mBackground.transform.localScale.x;
            float x = mBackground.transform.localPosition.x;
            mAnchorLocalX = mAlignMode == 1 ? x + halfWidth : x - halfWidth;
        }

        public void OnRefresh()
        {
            if (mBackground == null || mTMPText == null) return;
            mTMPText.ComputeMarginSize();
            Vector2 preferred = mTMPText.GetPreferredValues();
            Vector3 textScale = mTMPText.transform.lossyScale;
            float width = preferred.x * textScale.x + mPaddingX * 2f;
            float height = preferred.y * textScale.y + mPaddingY * 2f;
            if (width < mMinWidth) width = mMinWidth;
            if (mMaxWidth > 0f && width > mMaxWidth) width = mMaxWidth;
            if (height < mMinHeight) height = mMinHeight;
            if (mMaxHeight > 0f && height > mMaxHeight) height = mMaxHeight;
            Vector3 backgroundScale = mBackground.transform.lossyScale;
            if (backgroundScale.x != 0f && backgroundScale.y != 0f)
            {
                float localWidth = width / backgroundScale.x;
                float localHeight = height / backgroundScale.y;
                mBackground.size = new Vector2(localWidth, localHeight);
                float halfWidth = localWidth * 0.5f * mBackground.transform.localScale.x;
                Vector3 position = mBackground.transform.localPosition;
                position.x = mAlignMode == 1 ? mAnchorLocalX - halfWidth : mAnchorLocalX + halfWidth;
                mBackground.transform.localPosition = position;
            }
        }
    }
}
