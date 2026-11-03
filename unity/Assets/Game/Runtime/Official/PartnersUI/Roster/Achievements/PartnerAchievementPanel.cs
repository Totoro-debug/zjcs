// Selected original friendship achievement UI; see partner-achievement-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using AchievementRules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;
using AchievementType = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementType;
using AchievementState = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementState;
using AchievementInfo = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementInfo;
using AchievementDataKey = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementDataKey;
using AchievementDataBase = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementData;
using Player = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.Player;
using PlayerAchievementRecordDataWrap = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.PlayerAchievementRecordDataWrap;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Object = UnityEngine.Object;
using UnityEngine;

public class PartnerAchievementPanel : MonoBehaviour
{
    public PartnerAchievementPanelBase AchievementPanelBase;

    public AchievementType AchievementType;

    [Tooltip("Json参数，例如当类型为CostItem时，可以在JsonParam指定消耗的物品Id")]
    public string JsonParam;

    private AchievementDataKey mDataKey;

    private IPartnerAchievementUiServices services;
    public void Configure(IPartnerAchievementUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        AchievementPanelBase.Configure(value);
        InitDataKey();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void SetDataKey(AchievementDataKey dataKey)
    {
        mDataKey = dataKey;
        Refresh();
    }

    private void Refresh()
    {
        if (mDataKey != null && ((Behaviour)this).isActiveAndEnabled)
        {
            AchievementPanelBase.InitDataKey(mDataKey);
        }
    }

    private void InitDataKey()
    {
        if (AchievementType != AchievementType.None)
        {
            mDataKey = services.Rules.CreateKey(AchievementType, JsonParam);
        }
    }
}

}
