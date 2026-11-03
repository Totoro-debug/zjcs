// Selected original NPC chapter tab.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using Object = UnityEngine.Object;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerProfileNpcData;
using NpcProfileInfo = Zjcs.Official.Partners.PartnerProfileRules.ProfileInfo;
using UnityEngine;

public class NpcDetailPanelProfileContentTabItem : MonoBehaviour
{
    [SerializeField]
    private TabItem mTabItem;

    [SerializeField]
    private MultiLanguageText mTabText;

    [SerializeField]
    private TipDotNodeLinker mTipDotNodeLinker;

    private IPartnerProfileUiServices services;
    public void Configure(IPartnerProfileUiServices value) { services = value; services.ConfigureTip(mTipDotNodeLinker); }
    public NpcProfileInfo Info { get; private set; }

    public bool CanUnlock { get; private set; }

    public TabItem TabItem => mTabItem;

    public void Init(NpcProfileInfo info, bool canUnlock)
    {
        Info = info;
        CanUnlock = canUnlock;
        mTabText.Format(info.ProfileId);
        mTipDotNodeLinker.SetNodeInstanceInfo(new NpcProfileNodeInstanceInfo(info.Id, info.ProfileId));
    }
}

}
