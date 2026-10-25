using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>
    /// FunctionEntrysPanelBase and its StateAnimatorConfig integer branch, adapted
    /// from the pinned official client. The host supplies global input/window state.
    /// </summary>
    public class WorldFunctionEntrysPanelBase : MonoBehaviour
    {
        public readonly UnityEvent OnExpandPanel = new UnityEvent();
        public readonly UnityEvent OnFoldEndPanel = new UnityEvent();
        public bool Expanded => expanded;
        private RectTransform[] selfRects;
        private Button[] entryButtons;
        private Animator animator;
        private string parameter;
        private int foldedInt, expandedInt;
        private bool ignoreWindows, expanded;

        public void Initialize(Animator target, string parameterName, int folded, int unfolded,
            RectTransform[] rectangles, Button[] buttons, bool ignoreOpenWindows)
        {
            if (animator != null) throw new InvalidOperationException("Function entry was already bound.");
            animator = target != null ? target : throw new ArgumentNullException(nameof(target));
            parameter = parameterName;
            foldedInt = folded;
            expandedInt = unfolded;
            selfRects = rectangles ?? throw new ArgumentNullException(nameof(rectangles));
            entryButtons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            ignoreWindows = ignoreOpenWindows;
            foreach (Button button in entryButtons) button.onClick.AddListener(OnEntryBtnClick);
        }

        public void HandleGlobalPointerDown(Vector2 position, Camera uiCamera, bool hasPanelOrDialog)
        {
            if (!expanded || !ignoreWindows && hasPanelOrDialog) return;
            foreach (RectTransform item in selfRects)
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(item, position, uiCamera,
                    out Vector2 local) && item.rect.Contains(local)) return;
            RequestFoldPanel();
        }

        public void RequestExpandPanel() => ExpandPanel();
        public void RequestFoldPanel() => FoldPanel();

        protected virtual void ExpandPanel()
        {
            SetState(expandedInt);
            OnExpandPanel.Invoke();
        }

        protected virtual void FoldPanel()
        {
            SetState(foldedInt);
            expanded = false;
        }

        private void SetState(int value)
        {
            animator.SetInteger(parameter, value);
            if (animator.isActiveAndEnabled) animator.Update(0f);
        }

        private void OnEntryBtnClick()
        {
            if (expanded) RequestFoldPanel();
            else RequestExpandPanel();
        }

        public void OnExpandEnd() => expanded = true;
        public virtual void OnFoldEnd() => OnFoldEndPanel.Invoke();

        private void OnDestroy()
        {
            if (entryButtons != null)
                foreach (Button button in entryButtons)
                    if (button != null) button.onClick.RemoveListener(OnEntryBtnClick);
        }
    }
}
