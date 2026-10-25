using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>ChatActionQuickMsgEntryPanel.OnEnable's original collapsed panel state.</summary>
    public sealed class ChatActionQuickMsgEntryPanel : MonoBehaviour
    {
        public GameObject[] mExpendObjs;
        private void OnEnable()
        {
            foreach (GameObject item in mExpendObjs) item.SetActive(false);
        }
    }
}
