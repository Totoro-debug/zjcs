// Original PlayerUtils recycling queries; explicit query owners replace global services.
using System;
using System.Collections.Generic;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagTipDots;
namespace Zjcs.Official.BagRecycleQueries
{
public sealed class BagRecycleQueryRules
{
    readonly IBagRecycleQueryServices Services;
    public BagRecycleQueryRules(IBagRecycleQueryServices services) => Services = services ?? throw new ArgumentNullException(nameof(services));
public List<PlayerItemDataWrap> GetCanRecycleGift(IBagRecycleQueryPlayer player)
	{
		_ = Services.Tables.NpcGiftInfoParser;
		IBagRecycleNpcTable npcInfoParser = Services.Tables.NpcInfoParser;
		IBagRecycleNpcManager npcManager = player.NpcManager;
		IBagRecycleNpcLevelTable npcFriendshipLevelInfoParser = Services.Tables.NpcFriendshipLevelInfoParser;
		List<PlayerItemDataWrap> list = new List<PlayerItemDataWrap>();
		foreach (PlayerItemDataWrap item in player.FindItemsByItemType(ItemType.NpcUnlockItem))
		{
			if (npcInfoParser.TryGetNpcByExclusiveGift(item.Id, out var npcId))
			{
				PlayerNpcDataWrap byNpcClassId = npcManager.GetByNpcClassId(npcId);
				if (byNpcClassId != null && npcFriendshipLevelInfoParser.GetMaxLevel(byNpcClassId.NpcClassId) == byNpcClassId.FriendShipLevel)
				{
					list.Add(item);
				}
			}
		}
		return list;
	}

public List<PlayerItemDataWrap> GetCanRecycleDoorkey(IBagRecycleQueryPlayer player)
	{
		IReadOnlyCollection<PlayerItemDataWrap> readOnlyCollection = player.FindItemsByItemType(ItemType.DoorKey);
		IBagRecycleItemTagTable itemTagInfoParser = Services.Tables.ItemTagInfoParser;
		List<PlayerItemDataWrap> list = new List<PlayerItemDataWrap>();
		foreach (PlayerItemDataWrap item in readOnlyCollection)
		{
			if (item.Count > 0 && itemTagInfoParser.TryGet(item.Id, out var config) && IsTagInAllRegionActived(player, config.Tag))
			{
				list.Add(item);
			}
		}
		return list;
	}

public bool IsTagInAllRegionActived(IBagRecycleQueryPlayer player, string tag)
	{
		if (string.IsNullOrEmpty(tag))
		{
			return false;
		}
		List<WorldStageTagEntityInfo> worldStageTagEntityInfos = Services.Tables.WorldStageTagMapInfoParser.GetWorldStageTagEntityInfos(tag);
		if (worldStageTagEntityInfos == null || worldStageTagEntityInfos.Count == 0)
		{
			return true;
		}
		foreach (WorldStageTagEntityInfo item in worldStageTagEntityInfos)
		{
			if (!player.RegionIsActived(item.MapClassId, item.Region))
			{
				return false;
			}
		}
		return true;
	}

public List<PlayerItemDataWrap> GetCanRecycleGem(IBagRecycleQueryPlayer player)
	{
		int planesRank = Services.PlanesRank;
		IReadOnlyCollection<PlayerItemDataWrap> readOnlyCollection = player.FindItemsByType(ItemType.Gem);
		List<PlayerItemDataWrap> list = new List<PlayerItemDataWrap>();
		foreach (PlayerItemDataWrap item in readOnlyCollection)
		{
			if ((item.Info.PartInfo as GemPartInfo).Rank < planesRank)
			{
				list.Add(item);
			}
		}
		return list;
	}
}
}
