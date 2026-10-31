// Selected official controller; see partner-gift-controller-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerGiftNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerGiftNpcData;
using PlayerItemDataWrap = Zjcs.Official.ProfessionUI.IProfessionItemData;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerGiftLevelInfo;
using NpcFriendshipLevelInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftLevels;
using NpcGiftInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftExperience;
using NpcFriendshipTagInfo = Zjcs.Official.PartnersUI.PartnerGiftTagInfo;
using NpcGiftInfo = Zjcs.Official.PartnersUI.PartnerGiftInfo;
using Player = Zjcs.Official.PartnersUI.IPartnerGiftPlayer;
using NpcMeetType = System.Int32;
using AwardSourceData = System.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;

public class NpcGiftBlueDotItem : ProfessionRedDotItemBase
{
    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        base.Configure(value.PlayerBindings);
    }
    private IPartnerGiftUiServices Services => services ?? PartnerGiftUiServices.Required;
	[SerializeField]
	private bool mIsShowAllNpcGift;

	private List<NpcInfo> mNpcInfos = new List<NpcInfo>();

	private void Awake()
	{
		if (!mIsShowAllNpcGift)
		{
			return;
		}
		mNpcInfos.Clear();
		foreach (KeyValuePair<long, PlayerNpcDataWrap> item2 in Services.Player.Npcs)
		{
			NpcInfo item = Services.GetNpcInfo(item2.Value.NpcClassId);
			mNpcInfos.Add(item);
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnAddNewNpc += MainPlayer_OnAddNewNpc;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null)
		{
			Services.Player.OnAddNewNpc -= MainPlayer_OnAddNewNpc;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnAddNewNpc(int npcClassId, NpcMeetType type)
	{
		NpcInfo item = Services.GetNpcInfo(npcClassId);
		mNpcInfos.Add(item);
		Refresh();
	}

	private void MainPlayer_OnItemCountUpdate(PlayerItemDataWrap itemData, long count, AwardSourceData sourceData)
	{
		if (Services.IsNpcGift(itemData.Info))
		{
			Refresh();
		}
	}

	public void AddNpcInfo(NpcInfo info)
	{
		mNpcInfos.Clear();
		mNpcInfos.Add(info);
		Refresh();
	}

	public override bool ShowRedDot()
	{
		Player mainPlayer = Services.Player;
		if (mainPlayer.IsAddressListFunctionLimited)
		{
			return false;
		}
		NpcGiftInfoParser npcGiftInfoParser = Services.Gifts;
		NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = Services.Levels;
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		foreach (NpcInfo mNpcInfo in mNpcInfos)
		{
			int maxSubRankLevel = npcFriendshipLevelInfoParser.GetMaxSubRankLevel(mNpcInfo.Id, mainPlayer.SubRank);
			PlayerNpcDataWrap byNpcClassId = mainPlayer.GetNpcByClassId(mNpcInfo.Id);
			if (byNpcClassId.FriendShipLevel >= maxSubRankLevel)
			{
				continue;
			}
			dictionary.Clear();
			foreach (KeyValuePair<int, double> item in mNpcInfo.GiftPreferenceDict)
			{
				long itemAmount = mainPlayer.GetItemAmount(item.Key);
				if (itemAmount > 0)
				{
					dictionary.Add(item.Key, itemAmount);
				}
			}
			int allExpByDict = npcGiftInfoParser.GetAllExpByDict(dictionary, mNpcInfo.GiftPreferenceDict);
			if (npcFriendshipLevelInfoParser.GetFriendshipLevelByExp(mNpcInfo.Id, byNpcClassId.FriendShip + allExpByDict, mainPlayer.SubRank) > byNpcClassId.FriendShipLevel)
			{
				return true;
			}
		}
		return false;
	}
}

}
