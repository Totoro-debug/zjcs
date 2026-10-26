// Migrated from Framework/ClampingScrollRect.cs; serialized ranges come from the official prefab.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldClampingScrollRect : ScrollRect
    {
        public bool mClampWidth, mClampHeight, mClampContentMove;
        public Vector2 mClampWidthRange, mClampHeightRange;
        private float lastWidth, lastHeight;
        private RectTransform Rect => (RectTransform)transform;
        public override float minWidth => mClampWidth ? mClampWidthRange.x : base.minWidth;
        public override float minHeight => mClampHeight ? mClampHeightRange.x : base.minHeight;
        public override float preferredWidth => mClampWidth && content != null
            ? Mathf.Clamp(content.rect.width, mClampWidthRange.x, mClampWidthRange.y) : base.preferredWidth;
        public override float preferredHeight => mClampHeight && content != null
            ? Mathf.Clamp(content.rect.height, mClampHeightRange.x, mClampHeightRange.y) : base.preferredHeight;
        public override void SetLayoutHorizontal()
        {
            base.SetLayoutHorizontal();
            if (mClampWidth && content != null)
                Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferredWidth);
        }
        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            if (mClampHeight && content != null)
                Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
        }
        public override void OnDrag(PointerEventData data) => Move(data, base.OnDrag);
        public override void OnScroll(PointerEventData data) => Move(data, base.OnScroll);
        private void Move(PointerEventData data, Action<PointerEventData> callback)
        {
            if (content == null) { callback(data); return; }
            bool oldHorizontal = horizontal, oldVertical = vertical;
            horizontal = horizontal && (!mClampContentMove || content.rect.width > Rect.rect.width);
            vertical = vertical && (!mClampContentMove || content.rect.height > Rect.rect.height);
            callback(data);
            horizontal = oldHorizontal;
            vertical = oldVertical;
        }
        private void Update()
        {
            if (content == null) return;
            if (lastWidth == content.rect.width && lastHeight == content.rect.height) return;
            lastWidth = content.rect.width;
            lastHeight = content.rect.height;
            SetDirtyCaching();
        }
    }
}
