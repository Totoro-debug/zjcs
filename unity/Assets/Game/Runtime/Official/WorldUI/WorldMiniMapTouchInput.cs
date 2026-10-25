using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Official.WorldUI
{
    // UITouchInput's pointer hooks, using the original DragControllerBase and
    // DragControllerData physics. Mouse wheel replaces the mobile pinch input.
    public sealed class WorldMiniMapTouchInput : DragControllerBase, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public Action<float> Zoom;
        public void OnBeginDrag(PointerEventData eventData) => OnDragBegin();
        void IDragHandler.OnDrag(PointerEventData eventData) => OnDrag();
        public void OnEndDrag(PointerEventData eventData) => OnDragEnd();
        public void OnScroll(PointerEventData eventData) => Zoom?.Invoke(-eventData.scrollDelta.y);
    }
}
