using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private void Interact(Map10RuntimeEntity entity)
        {
            if (TryPushPuzzleEntity(entity)) return;
            if (exploration.InteractExploreEntity(entity)) return;
            if (OfficialMainlineStaminaModule.IsStaminaPoint(entity))
            {
                if (entity.EntityClassId == 5011)
                {
                    if (stamina.StaminaCanStart(entity)) ShowStageDialog(entity, null);
                }
                else stamina.CollectStaminaPoint(entity);
                return;
            }
            if (entity.TypeId == 223)
            {
                statues.InteractHero(entity);
                return;
            }
            if (localSave.completedEntityPlacements.Contains(entity.PlacementId)) return;
            if (MapProfile.HasNativeUi && entity.EntityClassId == 6620)
            {
                statues.InteractWitch(entity);
                return;
            }
            if (MapProfile.HasNativeUi && entity.TypeId == 202)
            {
                statues.ShowGoddessDialog(entity);
                return;
            }
            if (IsEntityLocked(entity))
            {
                hud.ShowOfficialNotice("前方有魔物阻挡");
                return;
            }
            if (TryCollectMap12Chest(entity)) return;
            if (TryMap12BuildingInteraction(entity)) return;
            if (puzzles != null && entity.EntityClassId == 6503)
            {
                if (!puzzles.InteractionEnabled(entity.PlacementId)) return;
                SavePuzzleMutation(() =>
                {
                    puzzles.RunStage(entity.PlacementId, 0);
                    SynchronizePuzzleEntities();
                });
                return;
            }
            if (mapClassId == 10 && entity.PlacementId == 216 && !HasStory("FirstMeetChaos"))
            {
                if (chaosNpc != null) entityView.RemoveStoryEntity(chaosNpc);
                MapGridPoint npcGrid = FindAdjacentStoryGrid();
                chaosNpc = entityView.SpawnStoryEntity(8402,
                    "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view",
                    new Vector2(npcGrid.X, npcGrid.Y));
                PlayStory("FirstMeetChaos", 0, () => ShowStageDialog(entity, () =>
                {
                    entityView.RemoveStoryEntity(chaosNpc);
                    chaosNpc = null;
                    CompleteStory("FirstMeetChaos");
                    SaveLocalProgress();
                }));
                return;
            }
            if (entity.TypeId == 503)
            {
                CompleteEntity(entity);
                hud.ShowOfficialNotice("已探索");
                return;
            }
            if (entity.AwardItems.Length > 0)
            {
                CollectEntityAwards(entity);
                return;
            }
            int scenario = entity.EntityClassId == 6501 || entity.EntityClassId == 6502
                ? (entity.State == 0 ? 1 : 6)
                : entity.TypeId == 4 ? 1 : entity.TypeId == 103 ? 6 : 0;
            Map10RuntimeMapTalkEntry talk = scenario > 0 ? ResolveMapTalk(entity, scenario) : null;
            if (talk != null && !string.IsNullOrEmpty(talk.TalkId))
            {
                StartStoryGraph(talk.TalkId, entity, () =>
                {
                    if (entity.StageIds.Any(value => value > 0)) ShowStageDialog(entity, null);
                });
                return;
            }
            if (talk != null && talk.DialogId > 0)
            {
                PlayMapTalkDialog(talk, entity);
                return;
            }
            int stageId = ResolveEntityStageId(entity);
            if (stageId <= 0)
            {
                hud.ShowOfficialNotice("该实体暂无可执行的本地交互");
                return;
            }
            if (stageId == 1001 && !HasStory("FirstBattle"))
            {
                if (!localSave.firstBattleIntroShown)
                {
                    StartFirstBattleIntroduction();
                    return;
                }
                ShowStageDialog(entity, () => PlayStory("FirstBattle", 2, () =>
                    {
                        CompleteStory("FirstBattle");
                        SaveLocalProgress();
                    }));
                return;
            }
            ShowStageDialog(entity, null);
        }

        private void CompleteEntity(Map10RuntimeEntity entity)
        {
            if (!localSave.completedEntityPlacements.Contains(entity.PlacementId))
            {
                localSave.completedEntityPlacements = localSave.completedEntityPlacements
                    .Concat(new[] { entity.PlacementId }).ToArray();
                ProgressionResult recorded = progressionSave.RecordEntityTagCompleted(MapReceiptPrefix + "entity:" + entity.PlacementId,
                    MapReceiptPrefix + slotId + ":entity:" + entity.PlacementId);
                if (recorded.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(recorded.Message);
            }
            regions.RefreshRegionProgress();
            RefreshEntityActivity();
            SaveLocalProgress();
        }

        private void CollectEntityAwards(Map10RuntimeEntity entity)
        {
            foreach (Map10RuntimeAwardItem award in entity.AwardItems)
            {
                string receipt = MapReceiptPrefix + slotId + ":entity:" + entity.PlacementId + ":item:" + award.ItemId;
                ProgressionResult result = progressionSave.ApplyInventoryTransaction(receipt, save =>
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Official Map10 item award could not be applied: " + award.ItemId);
                });
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(result.Message);
            }

            CompleteEntity(entity);
            Map10RuntimeAwardItem first = entity.AwardItems[0];
            hud.ShowOfficialNotice("获得物品 " + first.ItemId + " ×" + first.Count);
        }

        private bool IsEntityLocked(Map10RuntimeEntity entity)
        {
            if (!regions.IsRegionAccessible(entity.Region)) return true;
            if (puzzles != null && puzzles.Contains(entity.PlacementId) && entity.LockType > 1)
                return !puzzles.IsUnlocked(entity.PlacementId);
            if (entity.LockType <= 0 || entity.LockedByEntityGroups.Length == 0) return false;
            return entity.LockedByEntityGroups.Any(groupId => !IsEntityGroupComplete(groupId));
        }

        private bool IsEntityGroupComplete(int groupId)
        {
            if (!mapEntities.TryGetValue(groupId, out Map10RuntimeEntity conditionEntity)) return false;
            return localSave.completedEntityPlacements.Contains(groupId)
                || conditionEntity.StageIds.Any(localSave.completedStages.Contains);
        }

        private void ValidateEntityLockGroups()
        {
            foreach (Map10RuntimeEntity entity in mapEntities.Values)
                foreach (int groupId in entity.LockedByEntityGroups)
                    if (!mapEntities.ContainsKey(groupId))
                        throw new InvalidDataException("Official Map10 lock references a missing entity group: " + groupId);
        }

        private void RefreshEntityActivity()
        {
            if (entityView == null || localSave == null) return;
            SynchronizeMap12ClaimedChests();
            SynchronizeMap12BuildingCompletions();
            foreach (Map10RuntimeEntity entity in mapEntities.Values)
            {
                bool goddess = MapProfile.HasNativeUi && entity.TypeId == 202;
                bool goddessActivated = goddess
                    && progressionSave.Save.World.ActivatedGoddessRegions.Any(value =>
                    value.MapClassId == mapClassId && value.Region == entity.Region);
                int state = entity.State;
                if (mapLogicTags.TryGetValue(entity.PlacementId, out Map10RuntimeLogicTag tag))
                    state = localSave.mapLogicStates.FirstOrDefault(value => value != null && value.Tag == tag.Tag)?.State ?? state;
                if (goddessActivated) state = 1;
                if (entity.TypeId == 223) state = regions.IsPublicRegion(entity.Region) ? 2 : statues.HeroActivated(entity) ? 1 : 0;
                entityView.SetEntityState(entity.PlacementId, state);
                bool spawned = IsPuzzleEntity(entity.PlacementId) ? puzzles.IsSpawned(entity.PlacementId) : entity.Active;
                if (OfficialMainlineStaminaModule.IsStaminaPoint(entity))
                {
                    var placement = stamina.StaminaPlacement(entity);
                    bool[] flags = regions.IsPublicRegion(entity.Region) ? placement.Public : placement.Private;
                    spawned = flags != null && flags.Length > 0 && flags[0]
                        && stamina.Stamina.Available(mapClassId, entity.PlacementId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }
                bool active = (spawned || goddess) && regions.IsRegionAccessible(entity.Region)
                    && (entity.TypeId == 223 || IsMap12StelePresentation(entity)
                        || !localSave.completedEntityPlacements.Contains(entity.PlacementId))
                    && !IsMapLogicEntityDestroyed(entity.PlacementId);
                if (mapClassId == 13 && localSave.completedEntityPlacements.Contains(entity.PlacementId)
                    && map13Trees != null && map13Trees.KeepRemovalActive(entity.PlacementId)) active = true;
                entityView.SetEntityActive(entity.PlacementId, active);
            }
            SynchronizePuzzleGround();
            map12SteleEntities?.Refresh();
            if (MapProfile.HasNativeUi) miniMap.RefreshNativeMiniMapMarks();
        }

        private bool IsMapLogicEntityDestroyed(int placementId)
        {
            if (!mapLogicTags.TryGetValue(placementId, out Map10RuntimeLogicTag tag)) return false;
            return progressionSave.Save.World.CompletedEntityTags.Contains(tag.Tag)
                || localSave.mapLogicStates.Any(value => value != null && value.Tag == tag.Tag && value.State < 0);
        }

        private void ApplyStoryMapLogicStates(Map10RuntimeMapLogicState[] states)
        {
            foreach (Map10RuntimeMapLogicState state in states ?? Array.Empty<Map10RuntimeMapLogicState>())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Tag))
                    throw new InvalidDataException("Official Map10 story map-logic state is malformed.");
                Map10RuntimeLogicTag logicTag = mapLogicTags.Values.FirstOrDefault(value => value.Tag == state.Tag);
                bool virtualMap13Tag = mapClassId == 13 && logicTag == null;
                if (!virtualMap13Tag && (logicTag == null || !mapEntities.ContainsKey(logicTag.LogicId)))
                    throw new InvalidDataException("Official Map10 story map-logic tag is not in the loaded map: " + state.Tag);
                if (state.State < 0)
                {
                    ProgressionResult result = progressionSave.RecordEntityTagCompleted(state.Tag,
                        MapReceiptPrefix + slotId + ":logic-tag:" + state.Tag);
                    if (result.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(result.Message);
                }
                Map10RuntimeMapLogicState saved = localSave.mapLogicStates.FirstOrDefault(value =>
                    value != null && value.Tag == state.Tag);
                if (saved == null)
                    localSave.mapLogicStates = localSave.mapLogicStates.Concat(new[]
                    {
                        new Map10RuntimeMapLogicState { Tag = state.Tag, State = state.State }
                    }).ToArray();
                else saved.State = state.State;
            }
            RefreshEntityActivity();
            SaveLocalProgress();
        }

        private sealed class Map10RuntimeEntity
        {
            public int PlacementId;
            public int State;
            public int EntityClassId;
            public int TypeId;
            public string TalkTag;
            public string ModelPath;
            public int Region;
            public MapGridPoint Grid;
            public int[] StageIds;
            public Map10RuntimeEntityStageBinding[] StageBindings;
            public int[] PeriodicityGroupIds;
            public int[] PlayerPeriodicityGroupIds;
            public bool Active;
            public int LockType;
            public int[] LockedByEntityGroups;
            public int[] LockConditionEntityIds;
            public string LockParamsJson;
            public bool ConditionEntityNeverDestroy;
            public bool ConditionEntityInactiveInUnlock;
            public Map10RuntimeAwardItem[] AwardItems;
            public int ChangeMapClassId;

            public Map10RuntimeEntity(OfficialMap10EntityInfo info)
            {
                PlacementId = info.PlacementId;
                State = info.State;
                EntityClassId = info.EntityClassId;
                TypeId = info.TypeId;
                ModelPath = info.ModelPath;
                Region = info.Region;
                Grid = new MapGridPoint(info.Grid[0], info.Grid[1]);
                StageIds = info.StageIds ?? Array.Empty<int>();
                StageBindings = info.StageBindings ?? Array.Empty<Map10RuntimeEntityStageBinding>();
                PeriodicityGroupIds = info.PeriodicityGroupIds ?? Array.Empty<int>();
                PlayerPeriodicityGroupIds = info.PlayerPeriodicityGroupIds ?? Array.Empty<int>();
                Active = info.Active;
                LockType = info.LockType;
                LockedByEntityGroups = info.LockedByEntityGroups ?? Array.Empty<int>();
                LockConditionEntityIds = info.LockConditionEntityIds;
                LockParamsJson = info.LockParamsJson;
                ConditionEntityNeverDestroy = info.ConditionEntityNeverDestroy;
                ConditionEntityInactiveInUnlock = info.ConditionEntityInactiveInUnlock;
                AwardItems = info.AwardItems ?? Array.Empty<Map10RuntimeAwardItem>();
                ChangeMapClassId = info.ChangeMapClassId;
            }
        }
    }
}
