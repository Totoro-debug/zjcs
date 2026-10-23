using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Presentation.Home
{
    [DisallowMultipleComponent]
    public sealed class PetGachaDescriptionLinkHandler : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text description;
        private int[] entryIds = Array.Empty<int>();
        private Action<int, Vector2> onEntryClicked;

        public void Configure(TMP_Text source, int[] targets, Action<int, Vector2> onClicked)
        {
            description = source;
            entryIds = targets ?? Array.Empty<int>();
            onEntryClicked = onClicked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (description == null || entryIds.Length == 0 || eventData == null) return;
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                description, eventData.position, eventData.pressEventCamera);
            if (linkIndex < 0 || linkIndex >= description.textInfo.linkCount) return;
            string linkId = description.textInfo.linkInfo[linkIndex].GetLinkID();
            if (!int.TryParse(linkId, NumberStyles.None, CultureInfo.InvariantCulture, out int targetIndex)
                || targetIndex < 0 || targetIndex >= entryIds.Length)
                return;
            onEntryClicked?.Invoke(entryIds[targetIndex], eventData.position);
        }
    }
}
