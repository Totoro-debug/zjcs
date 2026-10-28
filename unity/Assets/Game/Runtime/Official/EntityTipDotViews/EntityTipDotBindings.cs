using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityTipDotViews
{
    public abstract class ECEntityTipDotSourceData
    {
        public abstract EntityOwnershipData Ownership { get; }
        public abstract ECViewEntityData Entity { get; }
    }
    public sealed class EntityTipDotExtendInfo
    {
        public bool InteractionUnique;
        public string ReddotGraphName;
    }
    public sealed class EntityTipDotMapInfo { public int MapGroupId; }
    public interface IEntityTipDotQueries
    {
        bool IsLeader { get; }
        long MainPlayerId { get; }
        ECViewEntityData GetEntity(EcEntityIdentifier id);
        int GetContainerClassId(ECViewEntityData entity);
        Pos2d GetPosition(ECViewEntityData entity);
        string GetEntityTag(ECViewEntityData entity);
        EntityTipDotExtendInfo GetExtendInfo(int classId);
        EntityTipDotMapInfo GetMapInfo(int mapClassId);
        string GetReddotGraphName(ECViewEntityData entity, int groupId, int mapClassId, string tag);
    }
    public abstract class EntityTipDotPrefabLease : IDisposable
    {
        public abstract GameObject Result { get; }
        public abstract void Dispose();
    }
    public interface IEntityTipDotPrefabLoader
    {
        EntityTipDotPrefabLease LoadAssetByGuid(string guid);
        GameObject Instantiate(GameObject prefab, Transform parent);
    }
    public static class EntityTipDotServices
    {
        public static IEntityTipDotQueries Queries { get; set; }
        public static IEntityTipDotQueries Require => Queries ??
            throw new InvalidOperationException("Original entity tip-dot queries are not bound.");
    }
}
