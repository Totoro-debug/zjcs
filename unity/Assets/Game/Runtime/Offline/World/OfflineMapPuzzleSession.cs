using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    [Serializable] public sealed class MapPuzzleRule
    {
        public int PlacementId, EntityClassId, InitialState, GridX, GridY, LockType;
        public bool InitiallySpawned, ConditionEntityInactiveInUnlock, ConditionEntityNeverDestroy, HasOverlapDetector;
        public MapPuzzleCondition[] LockConditions;
        public int[] TerminalLockIds, SpawnIds;
        public int[] OfflineIgnoredMissingStateTargets;
        public MapPuzzleOverlapInfo OverlapDetector;
        public OfficialWorldStageRule Stage;
        public MapPuzzleSelfState[] SelfStates;
        public MapPuzzleNotify[] Notify;
        public MapPuzzleOtherStates[] OtherStates;
        public int TerminalPuzzleType, ResetTransferActionId, MoveMask, InitialDirection;
        public bool HasPushResult, InitialFlipX;
        public int[] TerminalRemoveIds = Array.Empty<int>(), TerminalBoxIds = Array.Empty<int>();
        public int[] PushBoxDetectorIds = Array.Empty<int>(), PushBoxIds = Array.Empty<int>();
        public MapPuzzleRect MovableRect;
        public MapPuzzleBody[] Body = Array.Empty<MapPuzzleBody>();
        public float[] RealCenter = { 0, 0 };
    }
    [Serializable] public sealed class MapPuzzleRect { public MapPuzzleOffset MinGrid, MaxGrid; }
    [Serializable] public sealed class MapPuzzleBody
    {
        public MapPuzzleOffset Pos;
        public int GroundType;
        public bool IgnoreClick, OverrideGroundType;
    }
    [Serializable] public sealed class MapPuzzleCondition { public int PlacementId, State; }
    [Serializable] public sealed class MapPuzzleSelfState { public int State; }
    [Serializable] public sealed class MapPuzzleNotify { public MapPuzzleNotifyTarget[] Infos; }
    [Serializable] public sealed class MapPuzzleNotifyTarget { public int LoEntId, SubStateIndex; }
    [Serializable] public sealed class MapPuzzleOtherStates { public MapPuzzleStateTarget[] StageDatas; }
    [Serializable] public sealed class MapPuzzleStateTarget { public int LoEntId, State; public bool UseLoEntDefaultState; }
    [Serializable] public sealed class MapPuzzleOffset { public int X, Y; }
    [Serializable] public sealed class MapPuzzleOverlapInfo
    {
        public MapPuzzleOffset TargetPosOffsetThanMe;
        public bool IsEnabledInPrivateMap, IsEnabledInPublicMap, DisableDefaultActiveStatus;
        public int OverlapTimesIsActivity;
        public int[] OverlapDirection;
        public OfficialStageCompCfg ActivityResult;
    }
    [Serializable] public sealed class MapPuzzleEntityState
    {
        public int PlacementId, State, OverlapTimes;
        public bool IsUnlocked, Spawned;
        public bool HasPosition;
        public int GridX, GridY;
    }

    /// <summary>
    /// Local state for the original state, dependency and step-grid locks. Touch transitions,
    /// notifications and result ordering come from the pinned EC records. Results 254/258
    /// implement the user's confirmed offline solve/reset rule.
    /// </summary>
    public sealed partial class OfflineMapPuzzleSession
    {
        private readonly Dictionary<int, MapPuzzleRule> rules;
        private readonly Dictionary<int, MapPuzzleEntityState> states;
        private readonly Func<int, int> externalState;
        private readonly Func<int, bool> externalUnlocked;
        private int executionDepth;
        public IReadOnlyCollection<MapPuzzleRule> Rules => rules.Values;

        public OfflineMapPuzzleSession(MapPuzzleRule[] definitions, MapPuzzleEntityState[] saved,
            Func<int, int> readExternalState = null, Func<int, bool> readExternalUnlocked = null)
        {
            rules = (definitions ?? throw new ArgumentNullException(nameof(definitions))).ToDictionary(value => value.PlacementId);
            states = (saved ?? Array.Empty<MapPuzzleEntityState>()).ToDictionary(value => value.PlacementId, CloneState);
            externalState = readExternalState;
            externalUnlocked = readExternalUnlocked;
            foreach (MapPuzzleRule rule in rules.Values)
            {
                if (!states.ContainsKey(rule.PlacementId)) states.Add(rule.PlacementId, new MapPuzzleEntityState
                {
                    PlacementId = rule.PlacementId, State = rule.InitialState, Spawned = rule.InitiallySpawned,
                    IsUnlocked = rule.LockType == 0
                });
                if (!states[rule.PlacementId].HasPosition)
                {
                    states[rule.PlacementId].HasPosition = true;
                    states[rule.PlacementId].GridX = rule.GridX;
                    states[rule.PlacementId].GridY = rule.GridY;
                }
                if ((rule.LockType == 2 || rule.LockType == 5 || rule.LockType == 7) && rule.LockConditions.Length == 0)
                    throw new InvalidDataException("A pinned map lock has no conditions: " + rule.PlacementId);
                foreach (MapPuzzleCondition condition in rule.LockConditions)
                    if (!rules.ContainsKey(condition.PlacementId))
                        throw new InvalidDataException("Missing map lock target: " + condition.PlacementId);
                if (rule.LockType == 6 && (rule.PushBoxDetectorIds.Length == 0 || rule.PushBoxIds.Length == 0
                    || rule.PushBoxDetectorIds.Concat(rule.PushBoxIds).Any(id => !rules.ContainsKey(id))))
                    throw new InvalidDataException("Missing push-box lock targets: " + rule.PlacementId);
                foreach (MapPuzzleStateTarget target in rule.OtherStates.SelectMany(value => value.StageDatas))
                    if (!rules.ContainsKey(target.LoEntId) && !(rule.OfflineIgnoredMissingStateTargets
                        ?? Array.Empty<int>()).Contains(target.LoEntId))
                        throw new InvalidDataException("Missing map state-update target: " + rule.PlacementId + "/" + target.LoEntId);
            }
        }

        public bool Contains(int id) => rules.ContainsKey(id);
        public int State(int id) => states.TryGetValue(id, out MapPuzzleEntityState state) ? state.State : externalState?.Invoke(id) ?? 0;
        public int OverlapTimes(int id) => states[id].OverlapTimes;
        public bool IsUnlocked(int id) => states.TryGetValue(id, out MapPuzzleEntityState state) ? state.IsUnlocked : externalUnlocked?.Invoke(id) ?? false;
        public bool IsSpawned(int id) => states[id].Spawned;
        public MapPuzzleEntityState[] Save() => states.Values.OrderBy(value => value.PlacementId).Select(CloneState).ToArray();

        private static MapPuzzleEntityState CloneState(MapPuzzleEntityState value) => new MapPuzzleEntityState
        {
            PlacementId = value.PlacementId, State = value.State, OverlapTimes = value.OverlapTimes,
            IsUnlocked = value.IsUnlocked, Spawned = value.Spawned, HasPosition = value.HasPosition,
            GridX = value.GridX, GridY = value.GridY
        };

        public bool InteractionEnabled(int id) => !rules.Values.Any(rule => IsUnlocked(rule.PlacementId)
            && rule.ConditionEntityInactiveInUnlock && rule.LockConditions.Any(condition => condition.PlacementId == id));

        public void SetState(int id, int state)
        {
            states[id].State = state;
            RefreshLocks();
        }

        public void RunStage(int id, int subStateIndex, bool notification = false)
        {
            MapPuzzleRule rule = rules[id];
            OfficialSubStageStateInfo sub = rule.Stage.StateInfos[State(id)].SubStateList[subStateIndex];
            if (sub.Conditions.Count != 0 || sub.Behaviour.CompType != (notification ? 9 : 2))
                throw new InvalidDataException("Unexpected puzzle stage binding: " + id + "/" + subStateIndex);
            if (++executionDepth > 64) throw new InvalidDataException("Recursive puzzle stage notifications.");
            try
            {
                foreach (OfficialStageCompCfg result in sub.Results)
                {
                    switch (result.CompType)
                    {
                        case 209: states[id].State = rule.SelfStates[result.CompIndex].State; break;
                        case 233:
                            if (result.CompIndex < rule.Notify.Length)
                                foreach (MapPuzzleNotifyTarget target in rule.Notify[result.CompIndex].Infos)
                                    RunStage(target.LoEntId, target.SubStateIndex, true);
                            break;
                        case 254:
                            if (rule.TerminalPuzzleType == 3)
                                foreach (int removed in rule.TerminalRemoveIds) states[removed].Spawned = false;
                            else Solve(rule.TerminalLockIds);
                            break;
                        case 258:
                            if (rule.TerminalPuzzleType == 3)
                                RestoreEntities(rule.TerminalRemoveIds.Concat(rule.TerminalBoxIds));
                            else Reset(rule.TerminalLockIds);
                            break;
                        case 234:
                            foreach (int spawned in rule.SpawnIds) states[spawned].Spawned = true;
                            break;
                        case 203:
                            if (result.CompIndex < rule.OtherStates.Length)
                                foreach (MapPuzzleStateTarget target in rule.OtherStates[result.CompIndex].StageDatas)
                                {
                                    if (!states.ContainsKey(target.LoEntId) && (rule.OfflineIgnoredMissingStateTargets
                                        ?? Array.Empty<int>()).Contains(target.LoEntId)) continue;
                                    states[target.LoEntId].State = target.UseLoEntDefaultState ? rules[target.LoEntId].InitialState : target.State;
                                }
                            break;
                        case 500: break;
                        default: throw new InvalidDataException("Unimplemented puzzle stage result: " + result.CompType);
                    }
                }
                if (rule.Stage.AutoNextState) states[id].State++;
            }
            finally { executionDepth--; }
            if (executionDepth == 0) RefreshLocks();
        }

        public void CharacterMoved(MapGridPoint position, MapGridPoint previous, bool publicRegion = false)
        {
            if (position.Equals(previous)) return;
            foreach (MapPuzzleRule rule in rules.Values)
            {
                if (!rule.HasOverlapDetector || !IsSpawned(rule.PlacementId) || !InteractionEnabled(rule.PlacementId)) continue;
                MapPuzzleOverlapInfo info = rule.OverlapDetector;
                if (!(publicRegion ? info.IsEnabledInPublicMap : info.IsEnabledInPrivateMap)) continue;
                if (position.X != rule.GridX + info.TargetPosOffsetThanMe.X || position.Y != rule.GridY + info.TargetPosOffsetThanMe.Y) continue;
                if (info.OverlapDirection.Length > 0 && !info.OverlapDirection.Contains(LookDirection(position, previous))) continue;
                states[rule.PlacementId].OverlapTimes++;
                if (states[rule.PlacementId].OverlapTimes == info.OverlapTimesIsActivity && info.ActivityResult.CompType != 500)
                    throw new InvalidDataException("Unimplemented overlap activation effect: " + info.ActivityResult.CompType);
            }
            RefreshLocks();
        }

        public void RefreshLocks()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (MapPuzzleRule rule in rules.Values)
                {
                    MapPuzzleEntityState state = states[rule.PlacementId];
                    if (state.IsUnlocked || rule.LockType == 0 || rule.LockType == 1) continue;
                    bool met;
                    switch (rule.LockType)
                    {
                        case 2: met = rule.LockConditions.All(condition => State(condition.PlacementId) == condition.State); break;
                        case 5: met = rule.LockConditions.All(condition => IsUnlocked(condition.PlacementId)); break;
                        case 6: met = PushBoxComplete(rule); break;
                        case 7: met = rule.LockConditions.All(condition => DetectorActive(condition.PlacementId)); break;
                        default: throw new InvalidDataException("Unimplemented puzzle lock type: " + rule.LockType);
                    }
                    if (!met) continue;
                    state.IsUnlocked = true;
                    changed = true;
                    // EntityUnlockWorldStateCompleteComponent completes the original client-behaviour
                    // stage when its lock opens. Spawn and state-update results run before AutoNextState.
                    if ((rule.EntityClassId == 6530 || rule.EntityClassId == 6533) && state.State == 0)
                        RunStage(rule.PlacementId, 0, true);
                }
            } while (changed);
        }

        private bool DetectorActive(int id)
        {
            MapPuzzleRule rule = rules[id];
            if (!rule.HasOverlapDetector) throw new InvalidDataException("Step-grid target has no overlap detector: " + id);
            return OverlapTimes(id) == rule.OverlapDetector.OverlapTimesIsActivity;
        }

        private void Solve(IEnumerable<int> lockIds)
        {
            foreach (int id in lockIds)
            {
                MapPuzzleRule rule = rules[id];
                // Confirmed offline auto-puzzle: open the listed locks; do not move boxes or claim rewards.
                if (rule.LockType == 6)
                {
                    states[id].IsUnlocked = true;
                    if ((rule.EntityClassId == 6530 || rule.EntityClassId == 6533) && State(id) == 0)
                        RunStage(id, 0, true);
                    continue;
                }
                foreach (MapPuzzleCondition condition in rule.LockConditions)
                {
                    switch (rule.LockType)
                    {
                        case 2: states[condition.PlacementId].State = condition.State; break;
                        case 5: Solve(new[] { condition.PlacementId }); break;
                        case 7: states[condition.PlacementId].OverlapTimes = rules[condition.PlacementId].OverlapDetector.OverlapTimesIsActivity; break;
                        default: throw new InvalidDataException("Unimplemented automatic puzzle type: " + rule.LockType);
                    }
                }
            }
        }

        private void Reset(IEnumerable<int> lockIds)
        {
            foreach (int id in lockIds)
            {
                MapPuzzleRule rule = rules[id];
                if (rule.LockType == 6)
                {
                    // A completed mountain push-box lock remains open, including after reload/reset.
                    if (!IsUnlocked(id)) RestoreEntities(rule.PushBoxIds);
                    continue;
                }
                states[id].IsUnlocked = false;
                states[id].State = rule.InitialState;
                foreach (MapPuzzleCondition condition in rule.LockConditions)
                {
                    MapPuzzleEntityState target = states[condition.PlacementId];
                    target.State = rules[condition.PlacementId].InitialState;
                    target.OverlapTimes = 0;
                    if (rule.LockType == 5) Reset(new[] { condition.PlacementId });
                }
            }
        }

        private void RestoreEntities(IEnumerable<int> ids)
        {
            foreach (int id in ids.Distinct())
            {
                MapPuzzleRule rule = rules[id];
                MapPuzzleEntityState state = states[id];
                state.State = rule.InitialState;
                state.Spawned = rule.InitiallySpawned;
                state.GridX = rule.GridX;
                state.GridY = rule.GridY;
                state.HasPosition = true;
                state.OverlapTimes = 0;
            }
        }

        // Common.Pos2d.LookDirection(position, previous), including its diagonal tie ordering.
        private static int LookDirection(MapGridPoint source, MapGridPoint target)
        {
            int x = target.X - source.X, y = target.Y - source.Y;
            if (x == 0 && y == 0) return 0;
            if (x > 0) return y > x ? 0 : y < -x ? 1 : 3;
            return y > -x ? 0 : y < x ? 1 : 2;
        }
    }
}
