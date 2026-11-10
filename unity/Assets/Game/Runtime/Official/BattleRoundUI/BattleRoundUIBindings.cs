using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleRoundUI
{
    // The client view consumes these projections of the round driver and entities.
    // Shared combat calculation stays in the offline battle executor.
    public sealed class BattleRoundData
    {
        public int EntityId;
        public double PreTime, NextTime, Interval;
    }

    public enum BattleRoundUpdateState { None, Advance, Delay, Other }

    public sealed class BattleRoundDriver
    {
        public readonly List<BattleRoundData> DataList = new List<BattleRoundData>();
        public int CurActingEntityId;
        public BattleRoundData FindData(int id) => DataList.Find(data => data.EntityId == id);
        public BattleRoundData GetCurActingData() => FindData(CurActingEntityId);
        public event Action<int> OnDataRemove;
        public event Action<BattleRoundData> OnDataInsert;
        public event Action<BattleRoundData, BattleRoundUpdateState> OnDataUpdate;
        public event Action<int, int> OnCurActingUpdated;
        public void NotifyRemove(int id) => OnDataRemove?.Invoke(id);
        public void NotifyInsert(BattleRoundData data) => OnDataInsert?.Invoke(data);
        public void NotifyUpdate(BattleRoundData data, BattleRoundUpdateState state) => OnDataUpdate?.Invoke(data, state);
        public void NotifyActing(int previous, int current) => OnCurActingUpdated?.Invoke(previous, current);
    }

    public struct BattleRoundDamage
    {
        public int TargetId;
        public long Damage, Shield;
        public bool HasHitEntity;
        public int DamageType;
    }

    public interface IBattleRoundUIBindings
    {
        GamePlayManagerConfig Configuration { get; }
        GameRoundScene Scene { get; }
        Camera SceneCamera { get; }
        BattleRoundDriver Driver { get; }
        bool IsPlaying { get; }
        bool HasEntity(int id);
        bool ShowOnRoundProgress(int id);
        BattleRoundRole GetRole(int id);
        GameObject InstantiateCard(GameObject prefab, Transform parent);
        void PrepareCard(IGameRoundCharacterItem card, int id);
        event Action<BattleRoundDamage> Damage;
        event Action<int, int, int, int> Die;
        event Action<int> EntityAdded;
        event Action SameGameUpdated;
    }
}
