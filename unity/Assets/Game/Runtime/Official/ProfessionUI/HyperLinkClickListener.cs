using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionHyperLinkHandler
    {
        void SetParamByStr(string value);
        void OnClick();
    }
    public interface IProfessionHyperLinkHandlerProvider
    {
        IProfessionHyperLinkHandler GetHyperLinkClickHandler(int id, GameObject owner);
    }
    public sealed class HyperLinkClickListener : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI mText;
        private bool mInHyperLink;
        private IProfessionHyperLinkHandler mHyperLinkClickHandler;
        private IProfessionHyperLinkHandlerProvider mHyperLinkHandlerProvider;
        public bool InHyperLink => mInHyperLink;
        private void Awake() => mText = GetComponent<TextMeshProUGUI>();
        public void SetProvider(IProfessionHyperLinkHandlerProvider provider) => mHyperLinkHandlerProvider = provider;
        public void SetHandler(IProfessionHyperLinkHandler handler) => mHyperLinkClickHandler = handler;
        public void OnPointerClick(PointerEventData eventData)
        {
            mInHyperLink = false;
            int index = TMP_TextUtilities.FindIntersectingLink(mText,
                new Vector3(eventData.position.x, eventData.position.y, 0f), eventData.pressEventCamera);
            if (index < 0) return;
            string id = mText.textInfo.linkInfo[index].GetLinkID();
            if (mHyperLinkHandlerProvider != null)
            {
                if (!int.TryParse(id, out int number)) return;
                mHyperLinkClickHandler = mHyperLinkHandlerProvider.GetHyperLinkClickHandler(number, gameObject);
            }
            else if (mHyperLinkClickHandler != null && !string.IsNullOrEmpty(id)) mHyperLinkClickHandler.SetParamByStr(id);
            if (mHyperLinkClickHandler == null) Debug.LogError("HyperLinkClickHandler is null!", this);
            else { mInHyperLink = true; mHyperLinkClickHandler.OnClick(); }
        }
    }
}
