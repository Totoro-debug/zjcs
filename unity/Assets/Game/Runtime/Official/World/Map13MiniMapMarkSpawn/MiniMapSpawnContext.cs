using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapSpawnEntity:IMiniMapMarkEntity { EcEntityIdentifier Id { get; } }
    public interface IMiniMapMarkCell { bool ExistEntitys();IEnumerable<IMiniMapSpawnEntity> Entitys { get; } }
    public interface IMiniMapMarkGridGround { IMiniMapMarkCell GetPosData(Pos2d position); }
    public interface IMiniMapMarkPerformance
    { void RegisterSpawnManager(MiniMapMarkSpawnManager manager);void UnregisterSpawnManager(MiniMapMarkSpawnManager manager); }
    public interface IMiniMapMarkTipState { void SetState(ETipDotLevelType value); }
    // Binding bridge to the original tip-dot node component; no replacement UI.
    public sealed class MiniMapMarkTipDotLink:MonoBehaviour { public IMiniMapMarkTipState TipLevelState; }
    public abstract class MiniMapSpawnContext:MiniMapMarkContext
    {
        public abstract IMiniMapMarkDataQueries MarkDataQueries { get; }
        public abstract bool IsEntityInViewRange(IMiniMapSpawnEntity entity);
        public abstract string GetMapEntityMiniMapMark(IMiniMapSpawnEntity entity,int groupId,int map,string tag);
        public abstract IMiniMapDescriptionAssetLease<GameObject> LoadMarkPrefab(string path);
        public abstract GameObject InstantiateMark(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    internal static class MiniMapSpawnDisposable
    {
        public static T AddTo<T>(T value,ref MiniMapSpawnDisposableBag bag) where T:IDisposable{bag.Add(value);return value;}
    }
}
