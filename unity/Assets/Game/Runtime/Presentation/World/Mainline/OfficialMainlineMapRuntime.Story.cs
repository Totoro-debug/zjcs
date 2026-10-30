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
        [Serializable]
        private sealed class StoryMapActivationFields { public int MapClassId; }

        [Serializable]
        private sealed class StoryAloneWorldFields { public int MapAloneWorldClassId; }

        [Serializable]
        private sealed class StoryFollowerFields { public int[] NpcClassIds, FollowerClassId; }

        [Serializable]
        private sealed class StoryEnterRegionFields { public int MapClassId, RegionId, RegionStatus, OnlyCheckMove; }

        [Serializable]
        private sealed class StoryLeaveRegionFields { public int MapClassId, RegionId; }

        [Serializable]
        private sealed class StoryNearEntityFields
        {
            public int MapClassId, RegionId, Dist, IsUseClassId, EntClassId, EntStageType;
        }

        [Serializable]
        private sealed class StoryInteractionRangeFields
        {
            public string LoEntityTag;
            public int MapClassId, LoEntityId, IsRevert;
        }

        [Serializable]
        private sealed class StoryTriggerFields { public string Key; }

        private readonly Dictionary<string, HashSet<string>> storyBlackboards =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> storyProperties =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public static bool OfficialStoryEnteredMapRegion(int previousRegion, int currentRegion,
            int targetRegion, bool onlyCheckMove) => (targetRegion == -1 || currentRegion == targetRegion)
                && (!onlyCheckMove || previousRegion != targetRegion);

        public static bool OfficialStoryLeftMapRegion(int previousRegion, int currentRegion,
            int targetRegion) => (targetRegion == -1 || previousRegion == targetRegion)
                && currentRegion != previousRegion;

        public static bool OfficialStoryNearEntity(int playerX, int playerY, int entityX, int entityY,
            int distance) => distance > 0 && Math.Abs(playerX - entityX) + Math.Abs(playerY - entityY) <= distance;

        public static bool OfficialStoryTriggerSet(ISet<string> triggerKeys, string key)
        {
            if (triggerKeys == null || string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Official story triggers require a key set and key.");
            return triggerKeys.Add(key);
        }

        public static bool OfficialStoryTriggerIsSet(IEnumerable<string> triggerKeys, string key) =>
            !string.IsNullOrWhiteSpace(key) && triggerKeys != null && triggerKeys.Contains(key);

        private Map10RuntimeMapTalkEntry ResolveMapTalk(Map10RuntimeEntity entity, int scenario)
        {
            string entityId = "entity_" + entity.EntityClassId + "_name";
            bool stopTagFallback = false;
            Map10RuntimeMapTalkEntry match = FindMapTalk(entityId, scenario, entity.TalkTag, ref stopTagFallback);
            if (match != null || stopTagFallback || string.IsNullOrEmpty(entity.TalkTag)) return match;
            return FindMapTalk(entityId, scenario, string.Empty, ref stopTagFallback);
        }

        private Map10RuntimeMapTalkEntry FindMapTalk(string entityId, int scenario, string tag,
            ref bool stopTagFallback)
        {
            foreach (Map10RuntimeMapTalkEntry entry in mapTalkEntries)
            {
                if (entry == null || entry.EntityId != entityId || entry.Scenario != scenario
                    || (entry.Tag ?? string.Empty) != (tag ?? string.Empty))
                    continue;
                if (!IsMapTalkConditionMet(entry.Condition)) continue;
                bool hasStory = !string.IsNullOrEmpty(entry.TalkId);
                if (!hasStory && entry.DialogId <= 0) continue;
                if (hasStory && stories.TryGetValue(entry.TalkId, out Map10RuntimeStory story)
                    && story.maxCount > 0 && GetStoryPlayCount(entry.TalkId) >= story.maxCount)
                {
                    if (!string.IsNullOrEmpty(entry.Tag) && entry.FouceStopOnTagEqual)
                    {
                        stopTagFallback = true;
                        return null;
                    }
                    continue;
                }
                return entry;
            }
            return null;
        }

        private bool IsMapTalkConditionMet(Map10RuntimeTalkCondition condition)
        {
            if (condition == null || condition._type == "NoneCondition") return true;
            if (condition._type == "CompositeAndCondition")
                return condition.Ids != null && condition.Ids.Length > 0
                    && condition.Ids.All(id => mapTalkConditions.TryGetValue(id, out Map10RuntimeTalkCondition part)
                        && IsMapTalkConditionMet(part));
            if (condition._type == "CompositeOrCondition")
                return condition.Ids != null && condition.Ids.Any(id =>
                    mapTalkConditions.TryGetValue(id, out Map10RuntimeTalkCondition part)
                    && IsMapTalkConditionMet(part));
            if (condition._type == "StoryCountCondition")
                return Compare(GetStoryPlayCount(condition.StoryKey), condition.Op, condition.Count);
            if (condition._type == "StoryStatusCondition")
                return progressionSave != null && progressionSave.StoryStatus(condition.StoryKey) == condition.Status;
            if (condition._type == "MapWorldAloneStatusCondition")
                return progressionSave != null && Compare(
                    progressionSave.WorldAloneStatus(condition.MapClassId), condition.Op, condition.Status);
            if (condition._type == "StagePassCondition")
                return progressionSave.StageWinCount(condition.StageId) > 0
                    || localSave.completedStages.Contains(condition.StageId);
            if (condition._type == "MapLogicEntityEndByTagCondition")
                return progressionSave.Save.World.CompletedEntityTags.Contains(condition.Tag)
                    || localSave.mapLogicStates.Any(value => value != null && value.Tag == condition.Tag
                        && value.State < 0);
            if (condition._type == "PlayerProfessionCondition")
                return condition.Profession == 0;
            if (condition._type == "InitialWeaponCondition")
                return progressionSave != null && progressionSave.Save.Player.InitialWeaponType == condition.WeaponType;
            if (condition._type == "MaxProfessionRankCondition")
                return progressionSave != null && Compare(progressionSave.Save.Player.MaxProfessionRank,
                    condition.Op, condition.Rank);
            return false;
        }

        private void StartStoryGraph(string key, Map10RuntimeEntity context, Action completed)
        {
            if (storyRunning) throw new InvalidOperationException("A Map10 story is already running.");
            if (!stories.TryGetValue(key, out activeStory))
                throw new InvalidDataException("Official Map10 story is missing: " + key);
            storyFrames.Clear();
            storyBlackboards.Clear();
            storyProperties.Clear();
            activeStoryRootKey = key;
            storyRunning = true;
            storyContextEntity = context;
            storyFinished = completed;
            SetStoryStatus(key, 1);
            storyEntities.Clear();
            storyEntityClasses.Clear();
            RegisterStoryProperties(activeStory);
            if (key == "SavingChick" && context != null && context.EntityClassId == 8417)
            {
                storyHiddenContextEntity = mapEntityObject(context.PlacementId);
                if (storyHiddenContextEntity != null) storyHiddenContextEntity.SetActive(false);
            }
            RunStoryGraph(activeStory.entryNodeId);
        }

        private void RegisterStoryProperties(Map10RuntimeStory story)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Map10RuntimeStoryNode node in story.nodes ?? Array.Empty<Map10RuntimeStoryNode>())
            {
                if (node == null || node.type != "RegisterStoryPropertyNode") continue;
                StoryPropertyFields fields = JsonUtility.FromJson<StoryPropertyFields>(node.sourceJson);
                foreach (StoryPropertyReference reference in fields?.references?.RefIds ?? Array.Empty<StoryPropertyReference>())
                    if (reference?.data != null && !string.IsNullOrEmpty(reference.data.Name))
                        values[reference.data.Name] = reference.data.Value ?? string.Empty;
            }
            storyProperties[story.key] = values;
        }

        private void RunStoryGraph(int nodeId)
        {
            try
            {
                for (int count = 0; count < 256 && storyRunning; count++)
                {
                    // StoryGraph.AsyncRun ends normally when MoveNext returns
                    // null, including an unconnected selector Exit.
                    if (nodeId == 0)
                    {
                        if (FinishStoryFrame(out nodeId)) return;
                        continue;
                    }
                    Map10RuntimeStoryNode node = FindStoryNode(activeStory, nodeId);
                    if (node == null) throw new InvalidDataException("Official Map10 story node is missing: " + nodeId);
                    switch (node.type)
                    {
                        case "StartStoryNode":
                        case "WaitStartStoryNode":
                        case "WaitAllNoTipWindowClosedNode":
                        case "FinishStoryConditionNode":
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "WaitMapInitedNode":
                            // Begin finishes map/view binding before story input is
                            // enabled, so the original Initialized wait is satisfied.
                            if (session == null || entityView == null || avatar == null)
                                throw new InvalidOperationException("Official story started before map initialization.");
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "PlayerConditionNode":
                        case "PlayerOnMapConditionNode":
                        case "PlayerOnSceneConditionNode":
                        case "WaitMapEntityInStateConditionNode":
                        case "AllNoTipWinClosedConditionNode":
                            StartCoroutine(ContinueStoryWhenBooleanCondition(activeStory, node,
                                StoryNext(node, "Output") != 0 ? StoryNext(node, "Output") : StoryNext(node, "Exit")));
                            return;
                        case "WorldStopMoveNode":
                            navigation.StopMovement();
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "FinishStoryNode":
                            if (FinishStoryFrame(out nodeId)) return;
                            continue;
                        case "SetStoryStateNode":
                            SetStoryStatus(node.storyName, node.storyStatus);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "SetTriggerNode":
                            StoryTriggerFields setTrigger = JsonUtility.FromJson<StoryTriggerFields>(node.sourceJson);
                            if (mapClassId != 13 || setTrigger == null || string.IsNullOrWhiteSpace(setTrigger.Key)
                                || node.ports?.Any(port => port.name == "Key" && port.connections?.Length > 0) == true)
                                throw new InvalidDataException("Unsupported official story trigger input in Map" + mapClassId + ".");
                            if (!storyBlackboards.TryGetValue(activeStory.key, out HashSet<string> triggerKeys))
                                storyBlackboards.Add(activeStory.key, triggerKeys = new HashSet<string>(StringComparer.Ordinal));
                            OfficialStoryTriggerSet(triggerKeys, setTrigger.Key);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "GetFixAwardNode":
                            if (TryGrantFixedStoryReward(node.awardId, out string rewardNotice))
                            {
                                hud.ShowOfficialNotice(rewardNotice);
                                nodeId = StoryNext(node, "Exit");
                            }
                            else
                            {
                                nodeId = StoryNext(node, "FailExit");
                            }
                            continue;
                        case "DialogNode":
                            PlayStory(activeStory.key, node.dialogId, () => RunStoryGraph(StoryNext(node, "Exit")), node);
                            return;
                        case "OptionDialogNode":
                            PlayStory(activeStory.key, node.dialogId, () =>
                                ShowStoryOptions(activeStory.key, node, index =>
                                    RunStoryGraph(StoryNext(node, "Options " + index)
                                        is int target && target > 0 ? target : StoryNext(node, "Exit"))), node);
                            return;
                        case "IfNode":
                            nodeId = StoryNext(node, EvaluateStoryIf(activeStory, node) ? "True" : "False");
                            continue;
                        case "SelectorNode":
                            nodeId = SelectStoryBranch(activeStory, node);
                            continue;
                        case "SwitchNode":
                            nodeId = SelectStorySwitch(node, StorySwitchValue(activeStory, node));
                            continue;
                        case "GetMapWitchExploreInfoNode":
                            // The confirmed local query reads the durable activation ledger
                            // used by EvaluateStoryValue for this node's ActiveCount output.
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ClickMapPosInLogicNode":
                            MapGridPoint clickTarget = ResolveStoryMapPosition(activeStory, node, "mTargetPos");
                            if (CanStoryClickGrid(clickTarget)) navigation.ClickLogicalGrid(clickTarget);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "WaitPlayerArriveMapPosAroundNode":
                            MapGridPoint waitTarget = ResolveStoryMapPosition(activeStory, node, "mTargetPos");
                            int waitDistance = OfficialStoryArrivalDistance(
                                JsonUtility.FromJson<StoryMapPositionFields>(node.sourceJson).mDistance);
                            StartCoroutine(ContinueStoryWhenPlayerArrives(waitTarget, waitDistance, StoryNext(node, "Exit")));
                            return;
                        case "EnterMapRegionConditionNode":
                            StoryEnterRegionFields enterRegion = JsonUtility.FromJson<StoryEnterRegionFields>(node.sourceJson);
                            if (enterRegion == null || enterRegion.MapClassId != mapClassId
                                || enterRegion.RegionStatus != 0 || enterRegion.RegionId < -1)
                                throw new InvalidDataException("Unsupported official story region-entry condition in Map" + mapClassId + ".");
                            StartCoroutine(ContinueStoryWhenEnteringRegion(enterRegion,
                                StoryNext(node, "Exit")));
                            return;
                        case "MapRegionLeaveConditionNode":
                            StoryLeaveRegionFields leaveRegion = JsonUtility.FromJson<StoryLeaveRegionFields>(node.sourceJson);
                            if (leaveRegion == null || leaveRegion.MapClassId != mapClassId
                                || leaveRegion.RegionId < -1)
                                throw new InvalidDataException("Unsupported official story region-leave condition in Map" + mapClassId + ".");
                            StartCoroutine(ContinueStoryWhenLeavingRegion(leaveRegion,
                                StoryNext(node, "Output")));
                            return;
                        case "WaitNearEntConditionNode":
                            StoryNearEntityFields nearEntity = JsonUtility.FromJson<StoryNearEntityFields>(node.sourceJson);
                            if (nearEntity == null || nearEntity.MapClassId != mapClassId
                                || nearEntity.IsUseClassId != 1 || nearEntity.EntClassId <= 0)
                                throw new InvalidDataException("Unsupported official story entity proximity condition in Map" + mapClassId + ".");
                            StartCoroutine(ContinueStoryWhenNearEntity(nearEntity,
                                StoryNext(node, "Output")));
                            return;
                        case "ClickMapEntityNode":
                            Map10RuntimeEntity clickedEntity = ResolveStoryEntityPort(activeStory, node, "Entity");
                            navigation.StartMovement(clickedEntity.Grid, clickedEntity);
                            StartCoroutine(ContinueStoryAfterNavigation(StoryNext(node, "Exit")));
                            return;
                        case "ClickMapEntityInLogicNode":
                            Map10RuntimeEntity logicEntity = ResolveStoryEntityPort(activeStory, node, "mMyEntity");
                            navigation.StartMovement(logicEntity.Grid, logicEntity);
                            StoryValueFields clickFields = StoryFields(node);
                            if (clickFields.mNeedWaitLogicComplete != 0)
                            {
                                StartCoroutine(ContinueStoryAfterNavigation(StoryNext(node, "Exit")));
                                return;
                            }
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "MapEntityMoveInteractionNode":
                            Map10RuntimeEntity operateEntity = ResolveStoryEntityPort(activeStory, node, "OperateEntity");
                            Map10RuntimeEntity interactEntity = ResolveStoryEntityPort(activeStory, node, "InteractEntity");
                            StartCoroutine(MoveStoryMapEntity(operateEntity, interactEntity, node.moveSpeed,
                                StoryNext(node, "Exit")));
                            return;
                        case "HandInItemNode":
                            RunHandInItemNode(node);
                            return;
                        case "StoryStartStageNode":
                            StartMap14StoryStage(node);
                            return;
                        case "OpenMapNationShopPanelNode":
                            if (mapClassId != 13 || storyContextEntity == null || storyContextEntity.EntityClassId != 8438)
                                throw new InvalidDataException("Unsupported official nation-shop panel in Map" + mapClassId + ".");
                            OpenMap13NationShop(() => RunStoryGraph(StoryNext(node, "Exit")));
                            return;
                        case "PlayerActiveMapNode":
                            int activeMapClassId = JsonUtility.FromJson<StoryMapActivationFields>(node.sourceJson)?.MapClassId ?? 0;
                            if (activeMapClassId <= 0)
                                throw new InvalidDataException("Official story map activation target is invalid: " + activeStory.key);
                            ProgressionResult activation = progressionSave.ApplyMapActivationResult(activeMapClassId, true,
                                "story:" + activeStoryRootKey + ":node:" + node.id + ":active-map:" + activeMapClassId);
                            if (activation.Status == ProgressionResultStatus.InvalidState)
                                throw new InvalidOperationException(activation.Message);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "PlayerWorldAloneAppearNode":
                            int aloneWorldClassId = JsonUtility.FromJson<StoryAloneWorldFields>(node.sourceJson)?.MapAloneWorldClassId ?? 0;
                            if (aloneWorldClassId <= 0)
                                throw new InvalidDataException("Official story alone-world target is invalid: " + activeStory.key);
                            ProgressionResult aloneWorld = progressionSave.MarkWorldAloneAppeared(aloneWorldClassId,
                                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                "story:" + activeStoryRootKey + ":node:" + node.id + ":alone-world:" + aloneWorldClassId);
                            if (aloneWorld.Status == ProgressionResultStatus.InvalidState)
                                throw new InvalidOperationException(aloneWorld.Message);
                            if (!localSave.aloneWorldClassIds.Contains(aloneWorldClassId))
                            {
                                localSave.aloneWorldClassIds = localSave.aloneWorldClassIds.Concat(new[] { aloneWorldClassId })
                                    .Distinct().OrderBy(value => value).ToArray();
                                SaveLocalProgress();
                            }
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "SetMapLogicEntityInteractionRangeOverrideByCharacterNode":
                            ApplyStoryInteractionRangeOverride(node);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "AddNpcFollowerNode":
                            StoryFollowerFields followerAdd = JsonUtility.FromJson<StoryFollowerFields>(node.sourceJson);
                            SetNpcFollowers(localSaveFollowerClassIds.Concat(followerAdd?.NpcClassIds ?? Array.Empty<int>()));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "RemovePlayerNpcFollowerAllNode":
                            SetNpcFollowers(Array.Empty<int>());
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "WaitWindowOpenedNode":
                            if (activeStory.key != "WitchStory2" || storyContextEntity == null
                                || storyContextEntity.EntityClassId != 6620)
                                throw new InvalidDataException("Unsupported official window-open wait in Map" + mapClassId + ".");
                            statues.WaitForWitchPreviewOpen(() => RunStoryGraph(StoryNext(node, "Exit")));
                            return;
                        case "WaitWindowClosedNode":
                            if (activeStory.key != "WitchStory2" || storyContextEntity == null
                                || storyContextEntity.EntityClassId != 6620)
                                throw new InvalidDataException("Unsupported official window-close wait in Map" + mapClassId + ".");
                            statues.WaitForWitchPreviewClose(() => RunStoryGraph(StoryNext(node, "Exit")));
                            return;
                        case "ActivityPuzzleCompleteNode":
                            // This node queries the separate timed puzzle activity,
                            // not a mainline map lock. No such activity is active here.
                            nodeId = StoryNext(node, "NotComplete");
                            continue;
                        case "GoToNode":
                            StoryValueFields jump = StoryFields(node);
                            nodeId = jump.GoByNodeId != 0 ? jump.GoByNodeId : jump.GotoId != 0
                                ? jump.GotoId : int.TryParse(jump.GoTo, out int targetNode) ? targetNode : StoryNext(node, "Exit");
                            continue;
                        case "InitiatorActionNode":
                            bool success = ProcessPuzzleTerminalAction(StoryFields(node).ActionType);
                            nodeId = StoryNext(node, success ? "Success" : "Failure");
                            if (nodeId == 0) nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ItemQuikBuyNode":
                            hud.RunStoryQuickBuy(node);
                            return;
                        case "WaitForSecondsNode":
                            StartCoroutine(ContinueStoryAfterDelay(node.seconds, StoryNext(node, "Exit")));
                            return;
                        case "AddMapExclusiveNpcAtPosNode":
                            AddStoryEntity(node, ResolveStorySpawnPosition(activeStory, node));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "AddMapExclusiveNpcAroundPlayerNode":
                            MapGridPoint nearPlayer = FindAdjacentStoryGrid();
                            AddStoryEntity(node, new Vector2(nearPlayer.X, nearPlayer.Y));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "RemoveMapExclusiveNpcNode":
                            var removals = JsonUtility.FromJson<StoryNpcRemovalFields>(node.sourceJson);
                            if (removals.RemoveEntities.Any(v => v.StoryPlayableSelect.Type != 0))
                                throw new InvalidDataException("Original NPC removal playable must be bound before this story can run.");
                            RemoveStoryEntities(node.removeEntityClassIds);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "MapEntityMoveNode":
                            int entityNodeId = StoryNext(node, "OperateEntity");
                            if (!storyEntities.TryGetValue(entityNodeId, out GameObject movingEntity))
                                throw new InvalidDataException("Official Map10 story move has no spawned entity: " + entityNodeId);
                            StartCoroutine(MoveStoryEntity(movingEntity, node.destinationX, node.destinationY,
                                Mathf.Max(0.1f, node.moveSpeed), StoryNext(node, "Exit")));
                            return;
                        case "MapMeCharacterTransferNode":
                            StoryTransferFields transfer = JsonUtility.FromJson<StoryTransferFields>(node.sourceJson);
                            Map10RuntimeEntity transferTarget = null;
                            if (transfer.MapClassId == mapClassId)
                            {
                                int logicId = string.IsNullOrEmpty(transfer.LoEntityTag) ? transfer.LoEntityId
                                    : mapLogicTags.Values.FirstOrDefault(value => value.MapClass == mapClassId
                                        && value.Tag == transfer.LoEntityTag)?.LogicId ?? 0;
                                if (logicId != 0) mapEntities.TryGetValue(logicId, out transferTarget);
                            }
                            navigation.StartStoryTransfer(transferTarget, transfer.Direction,
                                () => RunStoryGraph(StoryNext(node, "Exit")));
                            return;
                        case "EnterStoryWorldCameraNode":
                            if (node.needHideUi) SetStoryWorldUiHidden(true);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ExitStoryWorldCameraNode":
                            storyCameraActive = false;
                            if (node.needShowUi) SetStoryWorldUiHidden(false);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "HideAllUINode":
                            SetStoryWorldUiHidden(true);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ShowAllUINode":
                            SetStoryWorldUiHidden(false);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "WorldCameraNode":
                        case "CameraMoveAndUnlockAreaNode":
                            Vector2 cameraPoint = ResolveStoryCameraPoint(activeStory, node);
                            storyCameraTarget = StoryWorldPoint(cameraPoint.x, cameraPoint.y) + new Vector3(0f, 0f, -20f);
                            storyCameraSizeTarget = Mathf.Clamp(DefaultWorldCameraSize() + node.viewSizeOffset, 4f, 30f);
                            storyCameraActive = true;
                            StartCoroutine(ContinueStoryAfterDelay(node.duration, StoryNext(node, "Exit")));
                            return;
                        case "SetMapLogicEntitySetStateNode":
                            ApplyStoryMapLogicStates(node.mapLogicStates);
                            if (node.endAsStoryFinish) SetStoryStatus(activeStory.key, 2);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "SubgraphRunNode":
                            if (!storySubgraphs.TryGetValue(node.subgraphName, out Map10RuntimeStory subgraph))
                                throw new InvalidDataException("Official Map" + mapClassId
                                    + " story subgraph is not loaded: " + node.subgraphName);
                            storyFrames.Push(new Map10RuntimeStoryFrame
                            {
                                Parent = activeStory,
                                NextNodeId = StoryNext(node, "Exit")
                            });
                            activeStory = subgraph;
                            nodeId = subgraph.entryNodeId;
                            continue;
                        case "PlayPlayableNode":
                            if (mapClassId == 13)
                            {
                                StartCoroutine(RunMap13StoryPlayable(activeStory, node));
                                return;
                            }
                            if (string.IsNullOrWhiteSpace(node.playableAssetGuid)
                                || !officialPlayables.ContainsKey(node.playableAssetGuid))
                                throw new InvalidDataException("Official Map10 story playable is not loaded: " + node.playableAssetGuid);
                            if (node.playableAssetGuid == "9b248cde91299ee4287533b25b7dd468")
                            {
                                GameObject npc = storyEntities.OrderBy(value => value.Key).Select(value => value.Value)
                                    .LastOrDefault(value => value != null);
                                if (npc != null) entityView.PlayStoryEntityAnimation(npc, "disappear_01");
                            }
                            if (node.waitPlayableStop)
                            {
                                StartCoroutine(ContinueStoryAfterDelay(node.playableDuration,
                                    StoryNext(node, "Exit")));
                                return;
                            }
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        default:
                            throw new InvalidDataException("Unsupported official Map10 story node: " + node.type);
                    }
                }
                if (storyRunning) throw new InvalidDataException("Official Map10 story exceeded its node limit: " + activeStory.key);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                AbortStoryGraph();
                hud.ShowOfficialNotice("剧情暂时无法继续");
            }
        }

        private bool HasStoryInteractionRangeOverride(Map10RuntimeEntity entity, MapGridPoint point)
        {
            return entity != null && localSave?.interactionRangeOverrides != null
                && localSave.interactionRangeOverrides.Any(value => value != null
                    && value.mapClassId == mapClassId && value.logicId == entity.PlacementId
                    && value.x == point.X && value.y == point.Y);
        }

        private void ApplyStoryInteractionRangeOverride(Map10RuntimeStoryNode node)
        {
            StoryInteractionRangeFields fields = JsonUtility.FromJson<StoryInteractionRangeFields>(node.sourceJson);
            if (mapClassId != 13 || fields == null || fields.MapClassId != mapClassId
                || fields.LoEntityId <= 0 || string.IsNullOrWhiteSpace(fields.LoEntityTag))
                throw new InvalidDataException("Unsupported official interaction-range override in Map" + mapClassId + ".");
            int logicId = mapLogicTags.Values.FirstOrDefault(value => value.MapClass == mapClassId
                && value.Tag == fields.LoEntityTag)?.LogicId ?? fields.LoEntityId;
            if (logicId <= 0) throw new InvalidDataException("Official interaction-range override target is missing.");
            localSave.interactionRangeOverrides ??= Array.Empty<Map10RuntimeInteractionRangeOverride>();
            if (fields.IsRevert != 0)
            {
                localSave.interactionRangeOverrides = localSave.interactionRangeOverrides
                    .Where(value => value == null || value.logicId != logicId).ToArray();
            }
            else
            {
                MapGridPoint point = navigation.currentGrid;
                if (!localSave.interactionRangeOverrides.Any(value => value != null && value.logicId == logicId
                    && value.x == point.X && value.y == point.Y))
                    localSave.interactionRangeOverrides = localSave.interactionRangeOverrides
                        .Concat(new[] { new Map10RuntimeInteractionRangeOverride
                        { mapClassId = mapClassId, logicId = logicId, x = point.X, y = point.Y } }).ToArray();
            }
            SaveLocalProgress();
        }

        private IEnumerator ContinueStoryAfterDelay(float seconds, int nextNodeId)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private IEnumerator ContinueStoryAfterNavigation(int nextNodeId)
        {
            while (navigation.IsMoving && storyRunning) yield return null;
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private IEnumerator ContinueStoryWhenBooleanCondition(Map10RuntimeStory story,
            Map10RuntimeStoryNode node, int nextNodeId)
        {
            while (storyRunning && !EvaluateStoryBoolean(story, node)) yield return null;
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private IEnumerator ContinueStoryWhenPlayerArrives(MapGridPoint target, int distance, int nextNodeId)
        {
            storyWaitingForArrival = true;
            try
            {
                while (storyRunning && OfficialStoryManhattanDistance(navigation.currentGrid, target) > distance)
                    yield return null;
                storyWaitingForArrival = false;
                if (storyRunning) RunStoryGraph(nextNodeId);
            }
            finally { storyWaitingForArrival = false; }
        }

        private IEnumerator ContinueStoryWhenEnteringRegion(StoryEnterRegionFields condition, int nextNodeId)
        {
            storyWaitingForArrival = true;
            try
            {
                MapGridPoint previous = navigation.currentGrid;
                int previousRegion = QueryMap13StoryRegion(previous);
                if (condition.OnlyCheckMove == 0 && (condition.RegionId == -1 || previousRegion == condition.RegionId))
                {
                    if (storyRunning) RunStoryGraph(nextNodeId);
                    yield break;
                }

                while (storyRunning)
                {
                    MapGridPoint current = navigation.currentGrid;
                    if (!current.Equals(previous))
                    {
                        int currentRegion = QueryMap13StoryRegion(current);
                        if (OfficialStoryEnteredMapRegion(previousRegion, currentRegion, condition.RegionId,
                                condition.OnlyCheckMove != 0)) break;
                        previous = current;
                        previousRegion = currentRegion;
                    }
                    yield return null;
                }
                storyWaitingForArrival = false;
                if (storyRunning) RunStoryGraph(nextNodeId);
            }
            finally { storyWaitingForArrival = false; }
        }

        private IEnumerator ContinueStoryWhenLeavingRegion(StoryLeaveRegionFields condition, int nextNodeId)
        {
            storyWaitingForArrival = true;
            try
            {
                MapGridPoint previous = navigation.currentGrid;
                int previousRegion = QueryMap13StoryRegion(previous);
                while (storyRunning)
                {
                    MapGridPoint current = navigation.currentGrid;
                    if (!current.Equals(previous))
                    {
                        int currentRegion = QueryMap13StoryRegion(current);
                        if (OfficialStoryLeftMapRegion(previousRegion, currentRegion, condition.RegionId)) break;
                        previous = current;
                        previousRegion = currentRegion;
                    }
                    yield return null;
                }
                storyWaitingForArrival = false;
                if (storyRunning) RunStoryGraph(nextNodeId);
            }
            finally { storyWaitingForArrival = false; }
        }

        private IEnumerator ContinueStoryWhenNearEntity(StoryNearEntityFields condition, int nextNodeId)
        {
            storyWaitingForArrival = true;
            try
            {
                while (storyRunning && !mapEntities.Values.Any(entity => entity.Active
                    && entity.EntityClassId == condition.EntClassId
                    && !localSave.completedEntityPlacements.Contains(entity.PlacementId)
                    && !IsMapLogicEntityDestroyed(entity.PlacementId)
                    && OfficialStoryNearEntity(navigation.currentGrid.X, navigation.currentGrid.Y,
                        entity.Grid.X, entity.Grid.Y, condition.Dist)))
                    yield return null;
                storyWaitingForArrival = false;
                if (storyRunning) RunStoryGraph(nextNodeId);
            }
            finally { storyWaitingForArrival = false; }
        }

        private int QueryMap13StoryRegion(MapGridPoint position)
        {
            if (session == null || !OfficialMapNavigationView.TryGetCell(session.Definition.Navigation,
                    position.X, position.Y, out int area, out int ground)) return -1;
            foreach (KeyValuePair<int, OfficialMainlineRegionConfig> region in regions.regionConfigs)
                if (region.Value.Areas.Contains(area)) return region.Key;
            return -1;
        }

        private IEnumerator MoveStoryEntity(GameObject entity, int destinationX, int destinationY,
            float speed, int nextNodeId)
        {
            if (entity == null || session == null)
            {
                AbortStoryGraph();
                yield break;
            }
            OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                new OfficialMapVector3 { X = entity.transform.position.x, Y = entity.transform.position.y },
                out int startX, out int startY);
            List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
            {
                Start = new MapGridPoint(startX, startY),
                End = new MapGridPoint(destinationX, destinationY),
                PreciseTarget = true
            });
            if (path == null || path.Count == 0)
            {
                AbortStoryGraph();
                hud.ShowOfficialNotice("剧情移动路线不可用");
                yield break;
            }
            foreach (MapGridPoint point in path)
            {
                if (entity == null || !storyRunning) yield break;
                Vector3 target = navigation.GridToWorld(point);
                while (Vector2.Distance(entity.transform.position, target) > 0.025f)
                {
                    entity.transform.position = Vector3.MoveTowards(entity.transform.position, target,
                        speed * Time.deltaTime);
                    yield return null;
                }
            }
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private IEnumerator MoveStoryMapEntity(Map10RuntimeEntity operate, Map10RuntimeEntity interact,
            float speed, int nextNodeId)
        {
            GameObject actor = entityView.FindEntityObject(operate.PlacementId);
            if (actor == null || session == null)
                throw new InvalidDataException("Official story map entity move target is not loaded.");
            List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
            {
                Start = operate.Grid,
                End = interact.Grid,
                PreferDistance = 1,
                PreciseTarget = true,
                BodyOffsets = new[] { new MapGridPoint(0, 0) }
            });
            if (path == null || path.Count == 0)
            {
                if (storyRunning) RunStoryGraph(nextNodeId);
                yield break;
            }
            float movementSpeed = Mathf.Max(0.1f, speed);
            foreach (MapGridPoint point in path.Skip(1))
            {
                if (!storyRunning) yield break;
                Vector3 target = navigation.GridToWorld(point);
                while (Vector2.Distance(actor.transform.position, target) > 0.025f && storyRunning)
                {
                    actor.transform.position = Vector3.MoveTowards(actor.transform.position, target,
                        movementSpeed * Time.deltaTime);
                    yield return null;
                }
            }
            operate.Grid = interact.Grid;
            Interact(interact);
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private void AddStoryEntity(Map10RuntimeStoryNode node, Vector2 grid)
        {
            string modelPath = node.entityClassId == 8402
                ? "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view"
                : mapEntities.Values.FirstOrDefault(value => value.EntityClassId == node.entityClassId)?.ModelPath;
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new InvalidDataException("Official Map10 story NPC model is missing: " + node.entityClassId);
            GameObject entity = entityView.SpawnStoryEntity(node.entityClassId, modelPath, grid);
            storyEntities[node.id] = entity;
            storyEntityClasses[node.id] = node.entityClassId;
        }

        private Vector2 ResolveStorySpawnPosition(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            int positionNodeId = StoryNext(node, "Pos");
            if (positionNodeId == 0) return new Vector2(node.positionX, node.positionY);
            object position = EvaluateStoryValue(story, FindStoryNode(story, positionNodeId));
            if (position is Zjcs.Offline.World.MapGridPoint grid) return new Vector2(grid.X, grid.Y);
            throw new InvalidDataException("Official story NPC position is not a converted map grid: " + node.id);
        }

        private IEnumerable<int> localSaveFollowerClassIds => progressionSave.Save.Player.NpcFollowerClassIds;

        private void SetNpcFollowers(IEnumerable<int> classIds)
        {
            ProgressionResult result = progressionSave.SetNpcFollowerClassIds(classIds);
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidDataException("NPC follower state could not be saved: " + result.Message);
            if (mapClassId == 13) RefreshMap13FollowerObjects();
        }

        private void RefreshMap13FollowerObjects()
        {
            foreach (GameObject follower in map13FollowerObjects.Values)
                if (follower != null) Destroy(follower);
            map13FollowerObjects.Clear();
            if (avatar == null || entityRoot == null || progressionSave?.Save?.Player?.NpcFollowerClassIds == null) return;
            int index = 0;
            var playerSorting = avatar.GetComponent<UnityEngine.Rendering.SortingGroup>();
            foreach (int classId in progressionSave.Save.Player.NpcFollowerClassIds)
            {
                var follower = new GameObject("Map13Follower_" + classId);
                follower.transform.SetParent(entityRoot, false);
                var sorting = follower.AddComponent<UnityEngine.Rendering.SortingGroup>();
                sorting.sortingLayerID = playerSorting == null ? SortingLayer.NameToID("Object") : playerSorting.sortingLayerID;
                sorting.sortingOrder = (playerSorting == null ? 0 : playerSorting.sortingOrder) + index + 1;
                follower.AddComponent<OfficialMap13NpcFollowerView>().Initialize(classId,
                    sorting.sortingLayerID, 0);
                follower.transform.position = avatar.transform.position + follower.GetComponent<OfficialMap13NpcFollowerView>().ModelOffset
                    + new Vector3(-0.8f - index * 0.45f, 0f, 0.01f + index * 0.001f);
                map13FollowerObjects[classId] = follower;
                index++;
            }
        }

        private void UpdateMap13FollowerObjects()
        {
            if (mapClassId != 13 || avatar == null) return;
            int index = 0;
            foreach (int classId in progressionSave?.Save?.Player?.NpcFollowerClassIds ?? new System.Collections.Generic.List<int>())
            {
                if (!map13FollowerObjects.TryGetValue(classId, out GameObject follower) || follower == null) continue;
                Vector3 target = avatar.transform.position + follower.GetComponent<OfficialMap13NpcFollowerView>().ModelOffset
                    + new Vector3(-0.8f - index * 0.45f, 0f, 0.01f + index * 0.001f);
                follower.transform.position = Vector3.MoveTowards(follower.transform.position, target, 5f * Time.deltaTime);
                index++;
            }
        }

        [Serializable] private sealed class StoryNpcRemovalFields { public StoryNpcRemoval[] RemoveEntities; }
        [Serializable] private sealed class StoryNpcRemoval { public StoryNpcRemovalPlayable StoryPlayableSelect; }
        [Serializable] private sealed class StoryNpcRemovalPlayable { public int Type; }

        private void RemoveStoryEntities(int[] classIds)
        {
            if (classIds == null || classIds.Length == 0) return;
            foreach (int nodeId in storyEntityClasses.Where(value => classIds.Contains(value.Value))
                         .Select(value => value.Key).ToArray())
            {
                if (storyEntities.TryGetValue(nodeId, out GameObject entity)) entityView.RemoveStoryEntity(entity);
                storyEntities.Remove(nodeId);
                storyEntityClasses.Remove(nodeId);
            }
        }

        private Vector2 ResolveStoryCameraPoint(Map10RuntimeStory story, Map10RuntimeStoryNode cameraNode)
        {
            Map10RuntimeStoryPort input = cameraNode.ports?.FirstOrDefault(value => value.name == "MoveToTargetPos");
            Map10RuntimeStoryConnection link = input?.connections?.FirstOrDefault();
            Map10RuntimeStoryNode targetNode = link == null ? null : FindStoryNode(story, link.nodeId);
            if (targetNode == null)
                return new Vector2(cameraNode.moveTargetX, cameraNode.moveTargetY);
            if (targetNode.type == "GetPosAroundWorldCamera")
            {
                StorySpatialNodeFields cameraFields = StorySpatialFields(targetNode);
                Vector3 viewPosition = storyCameraActive ? storyCameraTarget : avatar.transform.position;
                return StoryGridPosition(viewPosition) + cameraFields.Offset;
            }
            if (targetNode.type == "GetPosAroundStoryObjectNode" || targetNode.type == "GetEntityPosNode")
                return ResolveStoryPositionNode(targetNode);
            return new Vector2(cameraNode.moveTargetX, cameraNode.moveTargetY);
        }

        private Vector2 ResolveStoryPositionNode(Map10RuntimeStoryNode node)
        {
            if (node.type == "GetPosAroundStoryObjectNode")
            {
                StorySpatialNodeFields fields = StorySpatialFields(node);
                if (storySpatial != null)
                    return ResolveRegisteredStoryPoint(fields.RegisterKey) + fields.Offset;
                Map10RuntimeEntity registered = fields.RegisterKey == "Fangjianbei"
                    ? mapEntities.Values.FirstOrDefault(value => value.EntityClassId == 6001)
                    : null;
                if (registered == null)
                    throw new InvalidDataException("Official Map10 story register key is not bound: " + fields.RegisterKey);
                return new Vector2(registered.Grid.X, registered.Grid.Y) + fields.Offset;
            }
            if (node.type != "GetEntityPosNode")
                throw new InvalidDataException("Unsupported official story position node: " + node.type);
            StorySpatialNodeFields positionFields = StorySpatialFields(node);
            // GetEntityBaseNode.GetEntityType.MainPlayer is 0. A stale LoEntityId
            // field is ignored for this mode by the official GetEntityPosNode.
            if (positionFields.SelectGetType == 0)
                return new Vector2(navigation.currentGrid.X, navigation.currentGrid.Y) + positionFields.Offset;
            if (positionFields.SelectGetType == 6)
            {
                if (storySpatial != null)
                    return ResolveStoryTalkCenter() + positionFields.Offset;
                GameObject storyNpc = storyEntities.OrderBy(value => value.Key).Select(value => value.Value)
                    .LastOrDefault(value => value != null);
                if (storyNpc != null) return StoryGridPosition(storyNpc.transform.position) + positionFields.Offset;
                if (storyContextEntity != null)
                    return new Vector2(storyContextEntity.Grid.X, storyContextEntity.Grid.Y) + positionFields.Offset;
            }
            else if (positionFields.SelectGetType == 4 || positionFields.SelectGetType == 5)
            {
                Map10RuntimeLogicTag tag = positionFields.SelectGetType == 5
                    ? mapLogicTags.Values.FirstOrDefault(value => value.Tag == positionFields.Tag) : null;
                int targetId = positionFields.SelectGetType == 4 ? positionFields.LoEntityId : tag?.LogicId ?? 0;
                if ((tag == null || tag.MapClass == mapClassId)
                    && mapEntities.TryGetValue(targetId, out Map10RuntimeEntity targetEntity))
                    return new Vector2(targetEntity.Grid.X, targetEntity.Grid.Y) + positionFields.Offset;
                string target = tag == null ? "logic entity " + targetId : "Map" + tag.MapClass + " " + positionFields.Tag;
                throw new InvalidDataException("Official story camera target is outside the loaded map: " + target);
            }
            throw new InvalidDataException("Official Map10 story camera target cannot be resolved: " + positionFields.Tag);
        }

        private Vector3 StoryWorldPoint(float x, float y)
        {
            OfficialMapVector3 origin = session.Definition.Navigation.Origin;
            return new Vector3(origin.X + x, origin.Y + y, origin.Z - y * 0.001f - 0.1f);
        }

        private Vector2 StoryGridPosition(Vector3 world)
        {
            OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                new OfficialMapVector3 { X = world.x, Y = world.y }, out int x, out int y);
            return new Vector2(x, y);
        }

        private void SetStoryWorldUiHidden(bool hidden)
        {
            if (storyWorldUiHidden == hidden) return;
            storyWorldUiHidden = hidden;
            if (hud.ui != null) hud.ui.SetPanelActive("WorldCanvas", !hidden);
            if (miniMap.minimapImage != null) miniMap.minimapImage.gameObject.SetActive(!hidden);
        }

        private static Map10RuntimeStoryNode FindStoryNode(Map10RuntimeStory story, int nodeId) =>
            story?.nodes?.FirstOrDefault(value => value != null && value.id == nodeId);

        private static int StoryNext(Map10RuntimeStoryNode node, string portName)
        {
            Map10RuntimeStoryConnection connection = node?.ports?
                .FirstOrDefault(value => value.name == portName)?.connections?.FirstOrDefault();
            return connection == null ? 0 : connection.nodeId;
        }

        private static int StoryOptionId(Map10RuntimeStoryNode node, int index) =>
            node.options != null && index >= 0 && index < node.options.Length ? node.options[index] : 0;

        private bool EvaluateStoryIf(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            Map10RuntimeStoryNode input = FindStoryNode(story, StoryNext(node, "Input"));
            if (input == null)
                throw new InvalidDataException("Official Map" + mapClassId + " story condition input is missing: " + story.key);
            return EvaluateStoryBoolean(story, input);
        }

        private bool CanGetFixedStoryReward(int rewardId)
        {
            if (!fixedRewards.TryGetValue(rewardId, out Map10RuntimeFixedReward reward))
                throw new InvalidDataException("Official Map" + mapClassId + " fixed reward is missing: " + rewardId);
            for (int count = 0; count < reward.CanGetTimes; count++)
                if (!progressionSave.Save.Player.AppliedInventoryEventIds.Contains(FixedStoryRewardReceipt(rewardId, count)))
                    return true;
            return false;
        }

        private bool TryGrantFixedStoryReward(int rewardId, out string notice)
        {
            notice = string.Empty;
            if (!fixedRewards.TryGetValue(rewardId, out Map10RuntimeFixedReward reward))
                throw new InvalidDataException("Official Map" + mapClassId + " fixed reward is missing: " + rewardId);
            int claimIndex = -1;
            for (int count = 0; count < reward.CanGetTimes; count++)
                if (!progressionSave.Save.Player.AppliedInventoryEventIds.Contains(FixedStoryRewardReceipt(rewardId, count)))
                {
                    claimIndex = count;
                    break;
                }
            if (claimIndex < 0) return false;

            string receipt = FixedStoryRewardReceipt(rewardId, claimIndex);
            ProgressionResult result = progressionSave.ApplyInventoryTransaction(receipt, save =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (Map10RuntimeAwardItem item in reward.Items)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        item.ItemId, item.Count, now);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Official fixed story reward could not be applied: " + item.ItemId);
                }
            });
            if (result.Status != ProgressionResultStatus.Applied) return false;
            notice = "获得物品 " + string.Join("、", reward.Items.Select(value => value.ItemId + " ×" + value.Count));
            return true;
        }

        private string FixedStoryRewardReceipt(int rewardId, int claimIndex) =>
            rewardId == 5 && claimIndex == 0 ? "fixed_award:5"
                : MapReceiptPrefix + slotId + ":fixed-reward:" + rewardId + ":" + claimIndex;

        private bool CheckStoryMapEntityState(Map10RuntimeStoryNode node)
        {
            int logicId = node.loEntityId;
            if (!string.IsNullOrEmpty(node.entityTag))
                logicId = mapLogicTags.Values.FirstOrDefault(value => value.Tag == node.entityTag)?.LogicId ?? 0;
            if (logicId <= 0 || !mapEntities.TryGetValue(logicId, out Map10RuntimeEntity entity))
            {
                if (mapClassId == 13 && !string.IsNullOrEmpty(node.entityTag))
                {
                    Map10RuntimeMapLogicState virtualState = localSave.mapLogicStates.FirstOrDefault(value =>
                        value != null && value.Tag == node.entityTag);
                    return (virtualState?.State ?? 0) == node.entityState;
                }
                return false;
            }
            Map10RuntimeLogicTag tag = mapLogicTags.TryGetValue(logicId, out Map10RuntimeLogicTag byId)
                ? byId : mapLogicTags.Values.FirstOrDefault(value => value.LogicId == logicId);
            if (tag == null) return false;
            if (progressionSave.Save.World.CompletedEntityTags.Contains(tag.Tag)) return node.entityState < 0;
            Map10RuntimeMapLogicState saved = localSave.mapLogicStates.FirstOrDefault(value =>
                value != null && value.Tag == tag.Tag);
            return (saved?.State ?? entity.State) == node.entityState;
        }

        private static bool CompareStoryValues(int value, int target, int option)
        {
            switch (option)
            {
                case 0: return value == target;
                case 1: return value != target;
                case 2: return value > target;
                case 3: return value < target;
                case 4: return value >= target;
                case 5: return value <= target;
                default: throw new InvalidDataException("Unsupported official story comparison option: " + option);
            }
        }

        private void ShowStoryOptions(string storyName, Map10RuntimeStoryNode node, Action<int> selected)
        {
            if (node.options == null || node.options.Length == 0 || node.options.Length > 5)
                throw new InvalidDataException("Official Map10 story option count is invalid: " + storyName);
            storyOptionsWaiting = true;
            storyOptionCount = node.options.Length;
            storyOptionSelected = selected;
            hud.ui.SetOptionChoicesVisible(true);
            hud.ui.SetPanelActive("OptionTalkDialog", true);
            for (int index = 0; index < 5; index++)
            {
                string root = StoryOptionSlotPath(index);
                bool active = index < storyOptionCount;
                hud.ui.SetNodeActive("OptionTalkDialog", root, active);
                if (!active) continue;
                int optionId = StoryOptionId(node, index);
                if (!storyOptions.TryGetValue(StoryOptionKey(storyName, optionId), out Map10RuntimeStoryOption option))
                    throw new InvalidDataException("Official Map10 story option is missing: " + storyName + "/" + optionId);
                hud.ui.SetText("OptionTalkDialog", root + "/Content/OptionText/Text", FormatStoryOption(node, index, option.Text));
                hud.ui.SetNodeActive("OptionTalkDialog", root + "/Content/OptionText", true);
                hud.ui.SetNodeActive("OptionTalkDialog", root + "/Content/OptionRemainTimeText", false);
                hud.ui.SetNodeActive("OptionTalkDialog", root + "/Content/OptionText/Tip", !string.IsNullOrEmpty(option.MinorText));
                hud.ui.SetText("OptionTalkDialog", root + "/Content/OptionText/Tip", option.MinorText);
                hud.ui.SetNodeActive("OptionTalkDialog", root + "/Content/DotGroup", false);
                Sprite icon = hud.uiCatalog.FindSprite(option.Icon);
                if (icon != null) hud.ui.SetSprite("OptionTalkDialog", root + "/Content/TypeImg", icon);
                GameObject item = hud.ui.Find("OptionTalkDialog", root);
                Button button = item == null ? null : item.GetComponent<Button>();
                if (button == null) throw new InvalidDataException("Official Map10 option button is missing: " + root);
                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectStoryOption(capturedIndex));
            }
        }

        private void SelectStoryOption(int index)
        {
            if (!storyOptionsWaiting || index < 0 || index >= storyOptionCount) return;
            storyOptionsWaiting = false;
            hud.ui.SetOptionChoicesVisible(false);
            hud.ui.SetPanelActive("OptionTalkDialog", false);
            optionDialoguePortraits?.Clear();
            for (int i = 0; i < 5; i++) hud.ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            Action<int> selected = storyOptionSelected;
            storyOptionSelected = null;
            selected?.Invoke(index);
        }

        private static string StoryOptionSlotPath(int index)
        {
            const string root = "/OptionTalkDialog/OptionSlots/Content/OptionItem ";
            return index == 0 ? root : root + " (" + index + ")";
        }

        private GameObject mapEntityObject(int placementId)
        {
            GameObject entity = entityView == null ? null : entityView.FindEntityObject(placementId);
            if (entity != null) entity.SetActive(false);
            return entity;
        }

        private void FinishStoryGraph()
        {
            FinishStoryGraph(true);
        }

        private void FinishStoryGraph(bool invokeCompletion)
        {
            if (mapClassId == 13) CleanMap13StoryPlayables();
            mapVoice?.StopVoice();
            Action completed = invokeCompletion ? storyFinished : null;
            if (!invokeCompletion && MapProfile.HasNativeUi && !string.IsNullOrEmpty(activeStoryRootKey)
                && progressionSave.StoryStatus(activeStoryRootKey) == 1)
                SetStoryStatus(activeStoryRootKey, 0);
            SetStoryWorldUiHidden(false);
            storyCameraActive = false;
            if (worldCamera != null) worldCamera.orthographicSize = DefaultWorldCameraSize();
            cameraVelocity = Vector3.zero;
            storyCameraVelocity = Vector3.zero;
            if (storyHiddenContextEntity != null) storyHiddenContextEntity.SetActive(true);
            storyHiddenContextEntity = null;
            foreach (GameObject entity in storyEntities.Values) entityView.RemoveStoryEntity(entity);
            storyEntities.Clear();
            storyEntityClasses.Clear();
            hud.ui.SetPanelActive("TalkDialog", false);
            hud.ui.SetPanelActive("OptionTalkDialog", false);
            dialoguePortraits?.Clear();
            optionDialoguePortraits?.Clear();
            activeStory = null;
            storyContextEntity = null;
            storyFinished = null;
            storyRunning = false;
            storyOptionsWaiting = false;
            storyOptionSelected = null;
            storyFrames.Clear();
            activeStoryRootKey = string.Empty;
            completed?.Invoke();
        }

        private void AbortStoryGraph()
        {
            FinishStoryGraph(false);
            if (nativeDialogActive) FinishNativeDialog();
        }
    }
}
