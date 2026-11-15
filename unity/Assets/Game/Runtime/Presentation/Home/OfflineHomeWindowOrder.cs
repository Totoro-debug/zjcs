using System;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Home
{
    internal static class OfflineHomeWindowOrder
    {
        // WindowContainer.OnWindowShowing assigns the current top window + 10.
        // The local home window owners share one canvas, so read their active
        // popup roots together; the frame's border canvas is not a window.
        internal static void Show(Canvas container, WorldPopupWindow popup)
        {
            int order = container.sortingOrder;
            foreach (var current in container.GetComponentsInChildren<WorldPopupWindow>())
                if (current != popup)
                    order = Math.Max(order, current.Canvas.sortingOrder);
            popup.SetCanvasSortingOrder(order + 10);
        }
    }
}
