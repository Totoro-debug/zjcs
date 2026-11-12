// Selected official EntityHandler and MapGoToUtil methods. Only service/type bindings differ.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
public sealed class HomeTaskMapEntryNavigation
{
    public IHomeTaskMapEntryData Data {get;}
    public HomeTaskMapEntryNavigation(IHomeTaskMapEntryData data){Data=data??throw new ArgumentNullException(nameof(data));}
public int FindRegionNearEntry(IHomeTaskMapEntryState player, int mapClassId, int startRegion)
	{
		HomeTaskMapPart mapPartInfo = Data.GetMapPartInfo(mapClassId);
		if (mapPartInfo == null)
		{
			return -1;
		}
		IHomeTaskMapEntryData mapEntryInfoPackParser = Data;
		IHomeTaskMapEntryData mapRegionInfoParser = Data;
		List<int> activedMapEntities = player.GetActivedMapEntries(mapClassId);
		HashSet<int> hashSet = new HashSet<int>();
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(startRegion);
		while (queue.Count > 0)
		{
			int num = queue.Dequeue();
			hashSet.Add(num);
			if (mapEntryInfoPackParser.TryGetMapRegionEntryDatas(mapClassId, num, out var entryDatas))
			{
				foreach (HomeTaskMapSingleEntry item2 in entryDatas)
				{
					if (IsEntryUsable(player, mapClassId, item2, activedMapEntities))
					{
						return item2.LoEntId;
					}
				}
			}
			HomeTaskMapEntryRegion mapRegionInfo = mapRegionInfoParser.Get(mapClassId, num);
			if (mapRegionInfo.AdjacentRegions == null || mapRegionInfo.AdjacentRegions.Length == 0)
			{
				continue;
			}
			int[] adjacentRegions = mapRegionInfo.AdjacentRegions;
			foreach (int item in adjacentRegions)
			{
				if (!hashSet.Contains(item))
				{
					queue.Enqueue(item);
				}
			}
		}
		return mapPartInfo.DefEntryId;
	}
public bool IsEntryUsable(IHomeTaskMapEntryState player, int mapClassId, HomeTaskMapSingleEntry entryData, List<int> entryIds)
	{
		return entryData.EntryType switch
		{
			HomeTaskMapEntryType.Entry => entryIds?.Contains(entryData.LoEntId) ?? false,
			HomeTaskMapEntryType.ActivedEntry => CheckActivedEntryUsable(player, mapClassId, entryData),
			HomeTaskMapEntryType.WayPoint => CheckWayPointUsable(player, mapClassId, entryData),
			HomeTaskMapEntryType.PeriodicityInteractionWayPoint => CheckPeriodicityInteractionWayPointUsable(player, mapClassId, entryData),
			HomeTaskMapEntryType.None => false,
			_ => false,
		};
		static bool CheckActivedEntryUsable(IHomeTaskMapEntryState player2, int mapClassId2, HomeTaskMapSingleEntry mapSingleEntryInfo)
		{
			if (player2.GetMapRegionStatus(mapClassId2, mapSingleEntryInfo.Region) == HomeTaskMapRegionStatus.Public)
			{
				return true;
			}
			return false;
		}
		bool CheckPeriodicityInteractionWayPointUsable(IHomeTaskMapEntryState player2, int mapClassId2, HomeTaskMapSingleEntry wrap)
		{
			if (!CheckWayPointUsable(player2, mapClassId2, wrap))
			{
				return false;
			}
			int periodicityInteractionId = Data.GetPeriodicityInteractionId(mapClassId2, entryData.LoEntId);
			if (periodicityInteractionId <= 0)
			{
				return true;
			}
			int worldPeriodicityInteractionCount = player2.GetWorldPeriodicityInteractionCount(periodicityInteractionId);
			return Data.CheckCanInteraction(periodicityInteractionId, worldPeriodicityInteractionCount) == 2155;
		}
		bool CheckWayPointUsable(IHomeTaskMapEntryState player2, int mapClassId2, HomeTaskMapSingleEntry wrap)
		{
			return player2.GetMapRegionStatus(mapClassId2, entryData.Region) == HomeTaskMapRegionStatus.Public;
		}
	}
public bool GotoMapRegion(IHomeTaskMapEntryState self, IHomeTaskMapEntryRoute mapRootMgr, int mapClassId, int regionId)
	{
		if (self == null)
		{
			return false;
		}
		int num = mapClassId;
		int startRegion = regionId;
		int groupCurrentMapClassIdByMapClassIdAutoWithTagId = mapRootMgr.GetGroupCurrentMap(num);
		if (groupCurrentMapClassIdByMapClassIdAutoWithTagId != num)
		{
			if (mapRootMgr.CurrentMapClassId == groupCurrentMapClassIdByMapClassIdAutoWithTagId)
			{
				mapRootMgr.Broadcast("PLAYER_MAP_GROUP_GOTO_TARGET_MAP_IS_SAME");
				return false;
			}
			num = groupCurrentMapClassIdByMapClassIdAutoWithTagId;
			startRegion = 0;
			if (self.TryGetLastMapEntry(num, out var bestRegion))
			{
				startRegion = bestRegion;
			}
		}
		int entryId = FindRegionNearEntry(self, num, startRegion);
		mapRootMgr.DoEnterMap(num, entryId);
		return true;
	}
}
public sealed class HomeTaskMapPeriodicityRules
{
 public IHomeTaskMapEntryData Data {get;}
 public HomeTaskMapPeriodicityRules(IHomeTaskMapEntryData data){Data=data??throw new ArgumentNullException(nameof(data));}
public int CheckCanInteraction(int groupId, int count, bool isNegation = false)
	{
		HomeTaskMapPeriodicity mapPeriodicityInteractionInfo = Data.GetPeriodicity(groupId);
		if (mapPeriodicityInteractionInfo == null)
		{
			return 2157;
		}
		if (isNegation)
		{
			if (mapPeriodicityInteractionInfo.PeriodicityMaxCount > count)
			{
				return 2171;
			}
		}
		else if (mapPeriodicityInteractionInfo.PeriodicityMaxCount <= count)
		{
			return 2155;
		}
		return 0;
	}
}
}
