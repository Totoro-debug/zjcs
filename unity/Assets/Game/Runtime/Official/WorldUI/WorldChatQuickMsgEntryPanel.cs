using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>
    /// The official ChatQuickMsgEntryPanel visibility branches for a local
    /// session with no sent message/cooldown. Message services remain unbound.
    /// </summary>
    public sealed class WorldChatQuickMsgEntryPanel : WorldFunctionEntrysPanelBase
    {
        private GameObject emojiPanel;

        public void InitializeEmojiPanel(GameObject target)
        {
            emojiPanel = target;
            emojiPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (emojiPanel != null) emojiPanel.SetActive(false);
        }

        protected override void ExpandPanel()
        {
            emojiPanel.SetActive(true);
            base.ExpandPanel();
        }

        public override void OnFoldEnd()
        {
            base.OnFoldEnd();
            emojiPanel.SetActive(false);
        }
    }
}
