using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeUiGraphic
    {
        // AppearanceUIPartHandler supplies runtime slots for equipped parts.
        // Its original PartSlot chooses the resulting parent by draw order.
        // These nondrawing native containers are created only for that path;
        // explicitly serialized separator parents keep their original hierarchy.
        private readonly List<Transform> dynamicSeparatorParents = new List<Transform>();

        private void EnsureDynamicSeparatorParents()
        {
            if (!enableSeparatorSlots) return;
            int count = separatorPartInstructions.Count;
            if (separatorParts.Count < count &&
                (separatorSlotNames.Length > 0 || (separatorParts.Count > 0 && dynamicSeparatorParents.Count == 0)))
                throw new InvalidOperationException("Restore every original UI separator transform.");
            while (separatorParts.Count < count)
            {
                var parent = new GameObject("OriginalNativeSeparatorPart", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(transform, false);
                parent.anchorMin = Vector2.zero; parent.anchorMax = Vector2.one;
                parent.pivot = rectTransform.pivot;
                parent.offsetMin = parent.offsetMax = Vector2.zero;
                separatorParts.Add(parent); dynamicSeparatorParents.Add(parent);
            }
            foreach (var parent in dynamicSeparatorParents)
                parent.gameObject.SetActive(separatorParts.IndexOf(parent) < count);
        }

        private void OrderDynamicSeparatorGraphics()
        {
            var next = new Dictionary<Transform, int>();
            for (int index = 0; index < batches.Batches.Count; index++)
            {
                var parent = graphics[index].transform.parent;
                if (!dynamicSeparatorParents.Contains(parent)) continue;
                next.TryGetValue(parent, out int sibling);
                graphics[index].transform.SetSiblingIndex(sibling);
                next[parent] = sibling + 1;
            }
        }

        private void ReturnDynamicSeparatorGraphics()
        {
            if (dynamicSeparatorParents.Count == 0) return;
            foreach (var graphic in graphics)
                if (dynamicSeparatorParents.Contains(graphic.transform.parent))
                    graphic.transform.SetParent(transform, true);
            foreach (var parent in dynamicSeparatorParents) parent.gameObject.SetActive(false);
        }
    }
}
