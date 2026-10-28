using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    // HyperLinkClickListener.OnPointerClick: retain TMP's actual link hit test.
    internal sealed class OfficialSkillHyperlinkPointer : MonoBehaviour, IPointerClickHandler
    {
        public Action<int, Vector2> Clicked;
        public void OnPointerClick(PointerEventData data)
        {
            var text = GetComponent<TMP_Text>();
            int index = TMP_TextUtilities.FindIntersectingLink(text, data.position, data.pressEventCamera);
            if (index >= 0 && int.TryParse(text.textInfo.linkInfo[index].GetLinkID(), out int id))
                Clicked?.Invoke(id, data.position);
        }
    }

    internal sealed class OfficialSkillEntryDataSource : MonoBehaviour, LoopScrollDataSource
    {
        public Action<Transform, int> Bind;
        public void ProvideData(Transform item, int index) => Bind(item, index);
    }
}
