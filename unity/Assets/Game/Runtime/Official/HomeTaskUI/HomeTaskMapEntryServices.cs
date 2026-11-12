using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
    public enum HomeTaskMapEntryType { None, Entry, ActivedEntry, WayPoint, PeriodicityInteractionWayPoint }
    public enum HomeTaskMapRegionStatus { None, Private, Public }
    public sealed class HomeTaskMapPart { public int DefEntryId; }
    public sealed class HomeTaskMapEntryRegion { public int[] AdjacentRegions; }
    public sealed class HomeTaskMapSingleEntry { public int LoEntId, Region; public HomeTaskMapEntryType EntryType; }
    public sealed class HomeTaskMapPeriodicity { public int PeriodicityMaxCount; }

    public interface IHomeTaskMapEntryData
    {
        HomeTaskMapPart GetMapPartInfo(int mapClassId);
        bool TryGetMapRegionEntryDatas(int mapClassId,int region,out List<HomeTaskMapSingleEntry> entryDatas);
        HomeTaskMapEntryRegion Get(int mapClassId,int region);
        int GetPeriodicityInteractionId(int mapClassId,int loEntId);
        HomeTaskMapPeriodicity GetPeriodicity(int groupId);
        int CheckCanInteraction(int groupId,int count,bool isNegation=false);
    }
    // Read actual saved activations/status and the official period-adjusted count.
    // No synthetic entry activation or calendar reset is performed here.
    public interface IHomeTaskMapEntryState
    {
        List<int> GetActivedMapEntries(int mapClassId);
        HomeTaskMapRegionStatus GetMapRegionStatus(int mapClassId,int region);
        int GetWorldPeriodicityInteractionCount(int groupId);
        bool TryGetLastMapEntry(int mapClassId,out int region);
    }
    public interface IHomeTaskMapEntryRoute
    {
        int CurrentMapClassId {get;}
        int GetGroupCurrentMap(int mapClassId);
        void Broadcast(string key);
        void DoEnterMap(int mapClassId,int entryId);
    }
}
