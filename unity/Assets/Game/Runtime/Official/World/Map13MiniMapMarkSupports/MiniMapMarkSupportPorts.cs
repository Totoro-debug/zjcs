using System;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Projections only: no fabricated counts, graph levels, availability or owner IDs.
    public sealed class MiniMapPeriodicityInfo { public bool IsIgnoreOwner;public int PeriodicityMaxCount; }
    public sealed class MiniMapReddotDisplayInfo { public string ReddotGraphName; }
    public interface IMiniMapMarkSupportPlayer
    {
        long Id { get; }
        event Action<int,int> OnPeriodicityInteractionChanged;
        int GetWorldPeriodicityInteractionCount(int groupId);
    }
    public interface IMiniMapMarkSupportQueries
    {
        int GetPeriodicityInteractionId(IMiniMapMarkEntity entity);
        int GetOfflinePeriodicityInteractionId(int map,int loEntityId);
        bool TryGetPeriodicity(int id,out MiniMapPeriodicityInfo value);
        MiniMapPeriodicityInfo GetPeriodicity(int id);
        GroundType GetGroundType(int map,Pos2d position);
        MiniMapReddotDisplayInfo GetEntityMapDisplay(int entity,int group,int map,string area,GroundType ground,string tag);
        string GetEntityExtendReddotGraphName(int entity);
    }
    public interface IMiniMapMarkSupportContext
    {
        bool IsLeader { get; }
        bool HasMapContainer { get; }
        IMiniMapMarkSupportPlayer SupportPlayer { get; }
        IMiniMapMarkSupportQueries SupportQueries { get; }
    }
    public interface IMiniMapTipDotController
    {
        void LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback);
        void UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback);
    }
    public interface IMiniMapTipDotSystem
    {
        bool IsShowTipDot { get; }
        Action OnTipDotShowChanged { get;set; }
    }
}
