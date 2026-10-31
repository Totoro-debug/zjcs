// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
public class TipDotNpcCanSendGiftConditionNode : TipDotConditionNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	public bool IsShowAllNpcGift;

	private List<PartnerTipDotNpc> mNpcInfos = new List<PartnerTipDotNpc>();

	public override void Init()
	{
		base.Init();
		mNpcInfos.Clear();

		if (IsShowAllNpcGift)
		{
			foreach (KeyValuePair<long, PartnerTipDotNpcRecord> item2 in Services.Player.NpcManager)
			{
				PartnerTipDotNpc item = Services.GetNpc(item2.Value.NpcClassId);
				mNpcInfos.Add(item);
			}
		}
		else if (NodeKey.InstanceInfo is NpcNodeInstanceInfo npcNodeInstanceInfo)
		{
			mNpcInfos.Add(Services.GetNpc(npcNodeInstanceInfo.NpcId));
		}
		Services.Player.OnAddNewNpc += MainPlayer_OnAddNewNpc;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnAddNewNpc -= MainPlayer_OnAddNewNpc;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	private void MainPlayer_OnAddNewNpc(int npcClassId, NpcMeetType type)
	{
		if (IsShowAllNpcGift)
		{
			PartnerTipDotNpc item = Services.GetNpc(npcClassId);
			mNpcInfos.Add(item);
		}
		SetDirty();
	}

	private void MainPlayer_OnItemCountUpdate(PartnerTipDotItem itemData, long count, object sourceData)
	{
		if (itemData != null && (itemData.Info.ItemType == ItemType.NpcGift || itemData.Info.ItemType == ItemType.NpcUnlockItem))
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		IPartnerTipDotPlayer mainPlayer = Services.Player;
		if (mainPlayer.IsFunctionLimit(65))
		{
			return false;
		}
		IPartnerTipDotServices npcGiftInfoParser = Services;
		IPartnerTipDotServices npcFriendshipLevelInfoParser = Services;
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		foreach (PartnerTipDotNpc mNpcInfo in mNpcInfos)
		{
			int maxSubRankLevel = npcFriendshipLevelInfoParser.GetMaxSubRankLevel(mNpcInfo.Id, mainPlayer.SubRank);
			PartnerTipDotNpcRecord byNpcClassId = mainPlayer.NpcManager.GetByNpcClassId(mNpcInfo.Id);
			if (byNpcClassId == null || byNpcClassId.FriendShipLevel >= maxSubRankLevel)
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

	public override TipDotNodeBase Clone()
	{
		TipDotNpcCanSendGiftConditionNode tipDotNpcCanSendGiftConditionNode = new TipDotNpcCanSendGiftConditionNode();
		tipDotNpcCanSendGiftConditionNode.Fill(this);
		return tipDotNpcCanSendGiftConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotNpcCanSendGiftConditionNode tipDotNpcCanSendGiftConditionNode = source as TipDotNpcCanSendGiftConditionNode;
		if (tipDotNpcCanSendGiftConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotNpcCanSendGiftConditionNode);
	}

	public void Fill(TipDotNpcCanSendGiftConditionNode obj)
	{
		IsShowAllNpcGift = obj.IsShowAllNpcGift;
	}
}

}
