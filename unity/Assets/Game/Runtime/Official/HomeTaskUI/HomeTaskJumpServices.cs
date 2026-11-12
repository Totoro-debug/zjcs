using System;
namespace Zjcs.Official.HomeTaskUI
{
    public sealed class HomeTaskJumpText
    {
        public string Key {get;}
        public object[] Args {get;}
        public HomeTaskJumpText(string key,params object[] args){Key=key;Args=args;}
    }
    public sealed class HomeTaskJumpRegion {public int Region;}
    public sealed class HomeTaskJumpTag {public int MapClass,LogicId;}
    public interface IHomeTaskJumpDialog
    {
        void Init(HomeTaskJumpText text,Action confirm);
        void Init(string key,Action confirm);
    }
    // The real map/window owner supplies these operations; this port never
    // chooses an approximate entry point or creates an online result.
    public interface IHomeTaskJumpHost
    {
        bool IsOnMap();
        bool IsOnMap(int mapClassId);
        bool IsInTheMap(int mapClassId);
        bool HasMapContainer {get;}
        int CurrentMapClassId {get;}
        int WorldMapRegionProgressCount {get;}
        int GetGroupCurrentMap(int mapClassId);
        bool IsMapAccessible(int mapClassId);
        bool TryOpenMapEnterByFastClear(int mapClassId);
        bool TryOpenMapGalaxyPanel(int mapClassId);
        DateTime GetDateTime();
        DateTime GetRegionUnlockTime(int mapClassId,int region);
        bool IsRegionUnlocked(int mapClassId,int region,DateTime now);
        HomeTaskJumpRegion GetPreviousRegion(int mapClassId,int region);
        bool TryGetLastMapEntry(int mapClassId,out int region);
        string GetRegionNameKey(int mapClassId,int region);
        object FormatDetailedTimeSpan(TimeSpan span);
        void Broadcast(string key,params object[] args);
        void Popup(Action<IHomeTaskJumpDialog> initialize);
        void GotoMapRegion(int mapClassId,int region);
        void NavigateRegion(int mapClassId,int region,Action<object> completed);
        void NavigateFantasyLadder(Action<object> completed);
        void TraceTaskWhenEnterMap(long taskId);
        HomeTaskJumpTag GetTag(string tag);
        HomeTaskJumpRegion GetEntityRegion(int mapClassId,int logicId);
        void EnterNearEntity(int mapClassId,int logicId);
        void LeaveMap();
        bool CanTraceEntityToJump(int mapClassId,int region);
    }
}
