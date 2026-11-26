using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    // Original ScoreRewardDialog.Init; the local window host configures the
    // shared regular achievement data source before initializing this dialog.
    public sealed class ScoreRewardDialog : MonoBehaviour
    {
        public WorldMultiLanguageText ScoreRuleText;
        public AchievementPanel AchievementPanel;
        [SerializeField] private WorldStateSwitcher<bool> mRewardCompletedState;
        public void Init(bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule)
        {
            AchievementPanel.SetDataKey(new AchievementPanelKey(key.AchievementType, key.GroupId));
            ScoreRuleText.SetKey(rule.Key, rule.Args);
            mRewardCompletedState.SetState(completed);
        }
    }
}
