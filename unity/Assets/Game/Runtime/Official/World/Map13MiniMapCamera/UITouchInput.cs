using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.World.Map13MiniMap
{
    [DefaultExecutionOrder(-999)]
    public sealed class UITouchInput : DragControllerBase,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IScrollHandler
    {
        private bool mIsDragging;
        public event Action<Vector3> OnClick;
        public event Action<float,Vector3> OnZoom;
        public void OnPointerClick(PointerEventData eventData)
        {
            if(mIsDragging)mIsDragging=false;
            else OnClick?.Invoke(eventData.position);
        }
        public void OnBeginDrag(PointerEventData eventData){mIsDragging=true;OnDragBegin();}
        public void OnDrag(PointerEventData eventData){base.OnDrag();}
        public void OnEndDrag(PointerEventData eventData){OnDragEnd();}
        public void OnScroll(PointerEventData eventData){OnZoomBegin();OnZoom?.Invoke(-eventData.scrollDelta.y,eventData.position);}
    }
}
