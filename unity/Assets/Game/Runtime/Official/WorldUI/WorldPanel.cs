using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>WorldPanel.Awake / OnEnableAndInited, with local player and NPC team context.</summary>
    public sealed class WorldPanel : MonoBehaviour
    {
        public GameObject[] myHideUI, myWithoutCharacterUIs, myWithCharacterUIs, myLeaderUIs, myMemberUIs;
        public GameObject[] matchContents, downloadContents;

        private void Awake()
        {
            Set(myWithoutCharacterUIs, true);
            Set(myWithCharacterUIs, false);
            Set(myLeaderUIs, false);
            Set(myMemberUIs, false);
        }

        public void Initialize(bool isTeamMember)
        {
            Set(myWithoutCharacterUIs, false);
            Set(myWithCharacterUIs, true);
            Set(myLeaderUIs, !isTeamMember);
            Set(myMemberUIs, isTeamMember);
            // GameMatchMiniPanel.RefreshUI: a local player has no pending online match.
            Set(matchContents, false);
            // PlayAsYouDownloadEntry.Refresh: this offline package contains every runtime asset.
            Set(downloadContents, false);
        }

        public void HideUI() => Set(myHideUI, false);
        public void ShowUI() => Set(myHideUI, true);

        private static void Set(GameObject[] objects, bool active)
        {
            foreach (GameObject item in objects ?? System.Array.Empty<GameObject>()) item.SetActive(active);
        }
    }
}
