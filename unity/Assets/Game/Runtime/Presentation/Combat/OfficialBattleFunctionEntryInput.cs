using System;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Feeds desktop pointer input to the migrated official global-input handler.</summary>
    public sealed class OfficialBattleFunctionEntryInput : MonoBehaviour
    {
        private WorldFunctionEntrysPanelBase panel;
        private Func<bool> hasPanelOrDialog;

        public void Initialize(WorldFunctionEntrysPanelBase target, Func<bool> windowPredicate)
        {
            panel = target;
            hasPanelOrDialog = windowPredicate;
        }

        private void Update()
        {
            if (panel == null || !Input.GetMouseButtonDown(0)) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            panel.HandleGlobalPointerDown(Input.mousePosition, camera, hasPanelOrDialog?.Invoke() == true);
        }
    }
}
