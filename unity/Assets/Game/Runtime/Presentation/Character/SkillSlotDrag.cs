using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Original skill-slot drag interaction: dragging one battle/passive slot onto another swaps
    /// the two installed skills inside the selected plan.
    /// </summary>
    internal sealed class SkillSlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static SkillSlotDrag dragging;
        private RectTransform dragLayer;
        private RectTransform ghost;
        private RectTransform sourceIcon;

        public int ItemType;
        public int Slot;
        public Action<int, int, int> Dropped;
        public RectTransform DragLayer { get => dragLayer; set => dragLayer = value; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left
                || dragging != null || dragLayer == null) return;
            sourceIcon = GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(value => value.name == "SkillIcon");
            if (sourceIcon == null || !RectTransformUtility.RectangleContainsScreenPoint(
                sourceIcon, eventData.position, eventData.pressEventCamera))
            {
                sourceIcon = null;
                return;
            }

            GameObject copy = Instantiate(sourceIcon.gameObject, dragLayer, false);
            copy.name = "SkillDragGhost";
            ghost = copy.GetComponent<RectTransform>();
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = sourceIcon.rect.size;
            ghost.localScale = Vector3.one;
            ghost.localRotation = Quaternion.identity;
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            foreach (Button button in copy.GetComponentsInChildren<Button>(true))
            {
                button.interactable = false;
                button.enabled = false;
            }
            sourceIcon.gameObject.SetActive(false);
            dragging = this;
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging == this) MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragging != this) return;
            SkillSlotDrag target = FindTarget(eventData);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, eventData.position,
                EventCamera(eventData), out Vector2 localPointer);
            float radius = sourceIcon == null ? 0f : Mathf.Min(sourceIcon.rect.width, sourceIcon.rect.height);
            if (sourceIcon != null) sourceIcon.gameObject.SetActive(true);
            DestroyGhost();
            dragging = null;
            sourceIcon = null;
            if (target != null && target != this && target.ItemType == ItemType
                && Vector2.Distance(SlotCenter(target), localPointer) < radius)
                Dropped?.Invoke(ItemType, Slot, target.Slot);
        }

        private SkillSlotDrag FindTarget(PointerEventData eventData)
        {
            if (eventData == null || dragLayer == null) return null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, eventData.position,
                EventCamera(eventData), out Vector2 pointer);
            float radius = sourceIcon == null ? 0f : Mathf.Min(sourceIcon.rect.width, sourceIcon.rect.height);
            float nearest = radius * radius;
            SkillSlotDrag result = null;
            Transform group = transform.parent;
            if (group == null) return null;
            foreach (Transform sibling in group)
            {
                SkillSlotDrag candidate = sibling.GetComponent<SkillSlotDrag>();
                if (candidate == null || candidate.ItemType != ItemType) continue;
                float distance = (SlotCenter(candidate) - pointer).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    result = candidate;
                }
            }
            return result;
        }

        private Vector2 SlotCenter(SkillSlotDrag slot)
        {
            return dragLayer.InverseTransformPoint(slot.transform.position);
        }

        private Camera EventCamera(PointerEventData eventData)
        {
            Canvas canvas = dragLayer == null ? null : dragLayer.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : eventData.pressEventCamera;
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghost == null || dragLayer == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, eventData.position,
                EventCamera(eventData), out Vector2 local))
                ghost.anchoredPosition = local;
        }

        private void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
        }

        private void OnDisable()
        {
            if (dragging == this) dragging = null;
            if (sourceIcon != null) sourceIcon.gameObject.SetActive(true);
            DestroyGhost();
            sourceIcon = null;
        }
    }
}
