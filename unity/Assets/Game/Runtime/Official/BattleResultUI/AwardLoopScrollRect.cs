using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.BattleResultUI
{
    // Selected AwardLoopScrollRect; EventSystemManager's ordered RaycastAll is
    // resolved through the current isolated input event system.
    public sealed class AwardLoopScrollRect : LoopVerticalScrollRect, IPointerClickHandler
    {
        private bool mIsDraging;
        private Bounds mViewBounds;
        private const float kFloatingError = -.001f;
        public override bool IsActive() => base.IsActive() && mViewBounds.size.y - m_ContentBounds.size.y < kFloatingError;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsActive() && mIsDraging) return;
            GameObject hit = eventData.pointerCurrentRaycast.gameObject;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var result in results)
                if (hit != result.gameObject)
                {
                    ExecuteEvents.Execute(result.gameObject, eventData, ExecuteEvents.pointerClickHandler);
                    break;
                }
        }
        public override void OnBeginDrag(PointerEventData eventData) { mIsDraging = true; base.OnBeginDrag(eventData); }
        public override void OnEndDrag(PointerEventData eventData) { base.OnEndDrag(eventData); mIsDraging = false; }
        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            Rect rect = viewRect.rect;
            mViewBounds = new Bounds(rect.center, rect.size);
        }
    }
}
