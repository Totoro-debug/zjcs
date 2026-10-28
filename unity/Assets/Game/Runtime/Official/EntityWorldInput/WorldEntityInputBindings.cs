using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityWorldInput
{
    // Original input consumption boundaries; no world/team/server state is
    // created by these interfaces. Formal hosts must provide each service.
    public interface IWorldInputSupport
    { void ClickStage(LegacyWorldStageComponentData stage, Pos2d position, bool isLogicClick); }
    public interface IWorldInputRootLookup
    {
        int CurrentMapClassId { get; }
        bool TryGetEntity(EcEntityIdentifier id, out MyEntity entity);
    }
    public interface IWorldInputTeamContext
    {
        object MainTeam { get; }
        bool IsLeader { get; }
        bool TryGetStatusByContainerId(long containerId, out object status);
        bool TeamCheck(int mapClassId, object status);
    }
    public interface IWorldInputGeometry
    { GridRect GetPosListGridRect(List<Pos2d> positions); }
    public interface IWorldInputPlayerContext
    {
        long MainPlayerId { get; }
        IWorldInputMapCharacter GetMapCharacterFromController();
    }
    public interface IWorldInputMapCharacter { IWorldInputCharacter Character { get; } }
    public interface IWorldInputCharacter { bool IsLeader { get; } }
    public interface IWorldPlayerInput { bool IsMyself { get; } }
    public interface IWorldInputEntityState
    {
        bool IsRemoving(ECViewEntityData entity);
        bool HasEnded(ECViewEntityData entity);
    }
    public interface IWorldInputPresentation
    {
        WorldInputMonsterInfo GetMonsterInfo(int monsterId);
        string GetMonsterIcon(int iconId);
        string GetMapEntityNameKey(ECViewEntityData entity, int mapClassId);
    }
    public sealed class WorldInputMonsterInfo { public int IconId; }
    public interface IWorldInputSpriteOperation : IDisposable { Sprite Result { get; } }
    public interface IWorldInputResourceLoader { IWorldInputSpriteOperation LoadSprite(string name); }
    public interface IStageBehStage
    {
        int GetStageId(int compIndex);
        object GetStageBehInfo(int compIndex);
    }
    public sealed class WorldInputStageInfo { public bool SupportTeam; }
    public interface IWorldStageBehaviourQueries { WorldInputStageInfo GetStageInfo(int stageId); }
    public abstract class EntityPlayerInteractionComponentData : EntityClientComponentData
    { public abstract bool IsPlayerInteracting(long playerId); }
    // Same consumed R3.DisposableBag semantics already recovered from the APK:
    // ascending disposal against the current count, including reentrant adds.
    internal struct WorldInputResourceBag : IDisposable
    {
        private List<IDisposable> values;
        private int count;
        private bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else { if (values == null) values = new List<IDisposable>(); values.Add(value); count++; }
            return value;
        }
        public void Dispose()
        {
            if (values != null)
            {
                for (int i = 0; i < count; i++) values[i]?.Dispose();
                values = null; count = 0;
            }
            disposed = true;
        }
    }
}
