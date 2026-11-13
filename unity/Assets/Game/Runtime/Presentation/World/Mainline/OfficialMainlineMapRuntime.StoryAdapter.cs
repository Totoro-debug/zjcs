using System;
using Zjcs.Offline.Story;
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
        private StoryTransition<Map10RuntimeStory> ExecuteStoryNode(Map10RuntimeStory graph,
            Map10RuntimeStoryNode node, Action<int> resume)
        {
            activeStory = graph;
            Action<int> continuation = resume;
            storyResume = target =>
            {
                try { continuation(target); }
                catch (Exception error) { HandleStoryError(error); }
            };
            // Native callbacks and waits retain this node's single-use continuation.
            resume = storyResume;
            int nodeId;
            switch (node.type)
            {
                case "StartStoryNode":
                case "WaitStartStoryNode":
                case "WaitAllNoTipWindowClosedNode":
                case "FinishStoryConditionNode":
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitMapInitedNode":
                    // Begin finishes map/view binding before story input is
                    // enabled, so the original Initialized wait is satisfied.
                    if (session == null || entityView == null || avatar == null)
                        throw new InvalidOperationException("Official story started before map initialization.");
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "PlayerConditionNode":
                case "PlayerOnMapConditionNode":
                case "PlayerOnSceneConditionNode":
                case "WaitMapEntityInStateConditionNode":
                case "AllNoTipWinClosedConditionNode":
                    StartStoryWait(ContinueStoryWhenBooleanCondition(activeStory, node,
                        StoryNext(node, "Output") != 0 ? StoryNext(node, "Output") : StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "WorldStopMoveNode":
                    navigation.StopMovement();
                    if (mapClassId == 13) navigation.LockPlayerMovementForStory();
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "FinishStoryNode":
                    return StoryTransition<Map10RuntimeStory>.Finish;
                case "SetStoryStateNode":
                    SetStoryStatus(node.storyName, node.storyStatus);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "SetTriggerNode":
                    StoryTriggerFields setTrigger = JsonUtility.FromJson<StoryTriggerFields>(node.sourceJson);
                    if (mapClassId != 13 || setTrigger == null || string.IsNullOrWhiteSpace(setTrigger.Key)
                        || node.ports?.Any(port => port.name == "Key" && port.connections?.Length > 0) == true)
                        throw new InvalidDataException("Unsupported official story trigger input in Map" + mapClassId + ".");
                    if (!storyBlackboards.TryGetValue(activeStory.key, out HashSet<string> triggerKeys))
                        storyBlackboards.Add(activeStory.key, triggerKeys = new HashSet<string>(StringComparer.Ordinal));
                    OfficialStoryTriggerSet(triggerKeys, setTrigger.Key);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
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
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "DialogNode":
                    PlayStory(activeStory.key, node.dialogId, () => resume(StoryNext(node, "Exit")), node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "OptionDialogNode":
                    PlayStory(activeStory.key, node.dialogId, () =>
                        ShowStoryOptions(activeStory.key, node, index =>
                            resume(StoryNext(node, "Options " + index)
                                is int target && target > 0 ? target : StoryNext(node, "Exit"))), node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "IfNode":
                    nodeId = StoryNext(node, EvaluateStoryIf(activeStory, node) ? "True" : "False");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "SelectorNode":
                    nodeId = SelectStoryBranch(activeStory, node);
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "SwitchNode":
                    nodeId = SelectStorySwitch(node, StorySwitchValue(activeStory, node));
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "GetMapWitchExploreInfoNode":
                    // The confirmed local query reads the durable activation ledger
                    // used by EvaluateStoryValue for this node's ActiveCount output.
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ClickMapPosInLogicNode":
                    MapGridPoint clickTarget = ResolveStoryMapPosition(activeStory, node, "mTargetPos");
                    if (CanStoryClickGrid(clickTarget)) navigation.ClickLogicalGrid(clickTarget);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitPlayerArriveMapPosAroundNode":
                    MapGridPoint waitTarget = ResolveStoryMapPosition(activeStory, node, "mTargetPos");
                    int waitDistance = OfficialStoryArrivalDistance(
                        JsonUtility.FromJson<StoryMapPositionFields>(node.sourceJson).mDistance);
                    StartStoryWait(ContinueStoryWhenPlayerArrives(waitTarget, waitDistance, StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "EnterMapRegionConditionNode":
                    StoryEnterRegionFields enterRegion = JsonUtility.FromJson<StoryEnterRegionFields>(node.sourceJson);
                    if (enterRegion == null || enterRegion.MapClassId != mapClassId
                        || enterRegion.RegionStatus != 0 || enterRegion.RegionId < -1)
                        throw new InvalidDataException("Unsupported official story region-entry condition in Map" + mapClassId + ".");
                    StartStoryWait(ContinueStoryWhenEnteringRegion(enterRegion,
                        StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "MapRegionLeaveConditionNode":
                    StoryLeaveRegionFields leaveRegion = JsonUtility.FromJson<StoryLeaveRegionFields>(node.sourceJson);
                    if (leaveRegion == null || leaveRegion.MapClassId != mapClassId
                        || leaveRegion.RegionId < -1)
                        throw new InvalidDataException("Unsupported official story region-leave condition in Map" + mapClassId + ".");
                    StartStoryWait(ContinueStoryWhenLeavingRegion(leaveRegion,
                        StoryNext(node, "Output")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "WaitNearEntConditionNode":
                    StoryNearEntityFields nearEntity = JsonUtility.FromJson<StoryNearEntityFields>(node.sourceJson);
                    if (nearEntity == null || nearEntity.MapClassId != mapClassId
                        || nearEntity.IsUseClassId != 1 || nearEntity.EntClassId <= 0)
                        throw new InvalidDataException("Unsupported official story entity proximity condition in Map" + mapClassId + ".");
                    StartStoryWait(ContinueStoryWhenNearEntity(nearEntity,
                        StoryNext(node, "Output")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "ClickMapEntityNode":
                    Map10RuntimeEntity clickedEntity = ResolveStoryEntityPort(activeStory, node, "Entity");
                    navigation.StartMovement(clickedEntity.Grid, clickedEntity);
                    StartStoryWait(ContinueStoryAfterNavigation(StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "ClickMapEntityInLogicNode":
                    Map10RuntimeEntity logicEntity = ResolveStoryEntityPort(activeStory, node, "mMyEntity");
                    navigation.StartMovement(logicEntity.Grid, logicEntity);
                    StoryValueFields clickFields = StoryFields(node);
                    if (clickFields.mNeedWaitLogicComplete != 0)
                    {
                        StartStoryWait(ContinueStoryAfterNavigation(StoryNext(node, "Exit")));
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "MapEntityMoveInteractionNode":
                    Map10RuntimeEntity operateEntity = ResolveStoryEntityPort(activeStory, node, "OperateEntity");
                    Map10RuntimeEntity interactEntity = ResolveStoryEntityPort(activeStory, node, "InteractEntity");
                    if (operateEntity.PlacementId == StoryPlayerPlacementId)
                    {
                        string parentStory = activeStoryRootKey;
                        navigation.StartStoryInteraction(interactEntity, () =>
                        {
                            // An interaction can open a nested official story. In that
                            // case the nested graph owns continuation; otherwise the
                            // current graph advances after the local interaction.
                            if (storyRunning && string.Equals(activeStoryRootKey, parentStory,
                                StringComparison.Ordinal))
                                resume(StoryNext(node, "Exit"));
                        });
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    StartStoryWait(MoveStoryMapEntity(operateEntity, interactEntity, node.moveSpeed,
                        StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "HandInItemNode":
                    RunHandInItemNode(node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "StoryStartStageNode":
                    StartMap14StoryStage(node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "OpenMapNationShopPanelNode":
                    if (mapClassId != 13 || storyContextEntity == null || storyContextEntity.EntityClassId != 8438)
                        throw new InvalidDataException("Unsupported official nation-shop panel in Map" + mapClassId + ".");
                    OpenMap13NationShop(() => resume(StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "PlayerActiveMapNode":
                    int activeMapClassId = JsonUtility.FromJson<StoryMapActivationFields>(node.sourceJson)?.MapClassId ?? 0;
                    if (activeMapClassId <= 0)
                        throw new InvalidDataException("Official story map activation target is invalid: " + activeStory.key);
                    ProgressionResult activation = progressionSave.ApplyMapActivationResult(activeMapClassId, true,
                        "story:" + activeStoryRootKey + ":node:" + node.id + ":active-map:" + activeMapClassId);
                    if (activation.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(activation.Message);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
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
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "SetMapLogicEntityInteractionRangeOverrideByCharacterNode":
                    ApplyStoryInteractionRangeOverride(node);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "AddNpcFollowerNode":
                    StoryFollowerFields followerAdd = JsonUtility.FromJson<StoryFollowerFields>(node.sourceJson);
                    SetNpcFollowers(localSaveFollowerClassIds.Concat(followerAdd?.NpcClassIds ?? Array.Empty<int>()));
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "RemovePlayerNpcFollowerAllNode":
                    SetNpcFollowers(Array.Empty<int>());
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitWindowOpenedNode":
                    if (activeStory.key == "WitchStory2" && storyContextEntity != null
                        && storyContextEntity.EntityClassId == 6620)
                    {
                        statues.WaitForWitchPreviewOpen(() => resume(StoryNext(node, "Exit")));
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    // Other official windows are represented by native panels in the
                    // offline build. Their registration is synchronous, so the wait is
                    // already satisfied when the graph reaches this node.
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitWindowClosedNode":
                    if (activeStory.key == "WitchStory2" && storyContextEntity != null
                        && storyContextEntity.EntityClassId == 6620)
                    {
                        statues.WaitForWitchPreviewClose(() => resume(StoryNext(node, "Exit")));
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ActivityPuzzleCompleteNode":
                    // This node queries the separate timed puzzle activity,
                    // not a mainline map lock. No such activity is active here.
                    nodeId = StoryNext(node, "NotComplete");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "GoToNode":
                    StoryValueFields jump = StoryFields(node);
                    nodeId = jump.GoByNodeId != 0 ? jump.GoByNodeId : jump.GotoId != 0
                        ? jump.GotoId : int.TryParse(jump.GoTo, out int targetNode) ? targetNode : StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "InitiatorActionNode":
                    bool success = ProcessPuzzleTerminalAction(StoryFields(node).ActionType);
                    nodeId = StoryNext(node, success ? "Success" : "Failure");
                    if (nodeId == 0) nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ItemQuikBuyNode":
                    hud.RunStoryQuickBuy(node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "ClickBtnNode":
                case "ClickLoopListItemNode":
                    StoryButtonFields buttonFields = JsonUtility.FromJson<StoryButtonFields>(node.sourceJson);
                    if (node.type == "ClickBtnNode" && buttonFields != null
                        && string.Equals(buttonFields.StoryUIKey, "DailyDungeonDialogPrivateMap.BattleBtn",
                            StringComparison.Ordinal)
                        && storyContextEntity != null && storyContextEntity.StageIds != null
                        && storyContextEntity.StageIds.Any(value => value > 0))
                    {
                        StartEntityStage(storyContextEntity,
                            () => { if (storyRunning) resume(StoryNext(node, "Exit")); });
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    // The official node waits for a registered UI Button. Native
                    // offline panels register their controls before this graph tick;
                    // dispatching the click is therefore equivalent to advancing the
                    // graph once here.
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitStoryUIActiveNode":
                case "WaitPublicBottomBarActiveNode":
                case "WaitTabItemStateNode":
                    // These waits only guard server-created UI. All corresponding
                    // offline panels are created synchronously by the map HUD.
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ShowFullMaskNode":
                case "HideFullMaskNode":
                case "PlayMaskNode":
                case "CameraDragNode":
                case "SetFreeCameraForcePosXNode":
                case "EntityPlayableSupporterViewNode":
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "CameraMoveNode":
                    StoryCameraMoveFields cameraMove = JsonUtility.FromJson<StoryCameraMoveFields>(node.sourceJson);
                    Vector2 cameraMoveTarget = cameraMove == null
                        ? new Vector2(node.moveTargetX, node.moveTargetY) : cameraMove.TargetPosition;
                    storyCameraTarget = StoryWorldPoint(cameraMoveTarget.x, cameraMoveTarget.y);
                    storyCameraSizeTarget = DefaultWorldCameraSize();
                    storyCameraActive = true;
                    StartStoryWait(ContinueStoryAfterDelay(cameraMove == null ? node.duration : cameraMove.Duration,
                        StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "PlayStoryTimelineNode":
                    // Timeline assets are optional in the APK. The map-specific
                    // PlayableNode path above supplies the native equivalent; this
                    // node still preserves its wait duration and graph ordering.
                    StartStoryWait(ContinueStoryAfterDelay(node.playableDuration,
                        StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "StoryInvokeNode":
                case "RegisterStoryPropertyNode":
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitGameResultNode":
                    StartStoryGameResult(node);
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "TalkOptionNode":
                case "ProfessionRoutineOption":
                case "ShowProfessionPromotionOption":
                    nodeId = StoryNext(node, "StaticSelect 0");
                    if (nodeId == 0) nodeId = StoryNext(node, "DynamicExit");
                    if (nodeId == 0) nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "OpenProfessionChangeWinNode":
                case "OpenProfessionPromotionWinNode":
                case "OpenFreeProfessionChangeNode":
                    // Profession windows are owned by the shared native ProfessionUI
                    // service. The offline story has no server callback, so continue
                    // through the corresponding local success/exit branch.
                    nodeId = StoryNext(node, "Success");
                    if (nodeId == 0) nodeId = StoryNext(node, "Exit");
                    if (nodeId == 0) nodeId = StoryNext(node, "DynamicExit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WaitForSecondsNode":
                    StartStoryWait(ContinueStoryAfterDelay(node.seconds, StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "AddMapExclusiveNpcAtPosNode":
                    AddStoryEntity(node, ResolveStorySpawnPosition(activeStory, node));
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "AddMapExclusiveNpcAroundPlayerNode":
                    MapGridPoint nearPlayer = FindAdjacentStoryGrid();
                    AddStoryEntity(node, new Vector2(nearPlayer.X, nearPlayer.Y));
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "RemoveMapExclusiveNpcNode":
                    var removals = JsonUtility.FromJson<StoryNpcRemovalFields>(node.sourceJson);
                    if (removals.RemoveEntities.Any(v => v.StoryPlayableSelect.Type != 0))
                        throw new InvalidDataException("Original NPC removal playable must be bound before this story can run.");
                    RemoveStoryEntities(node.removeEntityClassIds);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "MapEntityMoveNode":
                    int entityNodeId = StoryNext(node, "OperateEntity");
                    if (!storyEntities.TryGetValue(entityNodeId, out GameObject movingEntity))
                        throw new InvalidDataException("Official Map10 story move has no spawned entity: " + entityNodeId);
                    StartStoryWait(MoveStoryEntity(movingEntity, node.destinationX, node.destinationY,
                        Mathf.Max(0.1f, node.moveSpeed), StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
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
                        () => resume(StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "EnterStoryWorldCameraNode":
                    if (node.needHideUi) SetStoryWorldUiHidden(true);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ExitStoryWorldCameraNode":
                    storyCameraActive = false;
                    if (node.needShowUi) SetStoryWorldUiHidden(false);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "HideAllUINode":
                    SetStoryWorldUiHidden(true);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "ShowAllUINode":
                    SetStoryWorldUiHidden(false);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "WorldCameraNode":
                case "CameraMoveAndUnlockAreaNode":
                    Vector2 cameraPoint = ResolveStoryCameraPoint(activeStory, node);
                    storyCameraTarget = StoryWorldPoint(cameraPoint.x, cameraPoint.y) + new Vector3(0f, 0f, -20f);
                    storyCameraSizeTarget = Mathf.Clamp(DefaultWorldCameraSize() + node.viewSizeOffset, 4f, 30f);
                    storyCameraActive = true;
                    StartStoryWait(ContinueStoryAfterDelay(node.duration, StoryNext(node, "Exit")));
                    return StoryTransition<Map10RuntimeStory>.Wait;
                case "SetMapLogicEntitySetStateNode":
                    ApplyStoryMapLogicStates(node.mapLogicStates);
                    if (node.endAsStoryFinish) SetStoryStatus(activeStory.key, 2);
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                case "SubgraphRunNode":
                    if (!storySubgraphs.TryGetValue(node.subgraphName, out Map10RuntimeStory subgraph))
                        throw new InvalidDataException("Official Map" + mapClassId
                            + " story subgraph is not loaded: " + node.subgraphName);
                    return StoryTransition<Map10RuntimeStory>.Call(subgraph, StoryNext(node, "Exit"));
                case "PlayPlayableNode":
                    if (mapClassId == 13 || mapClassId == 16)
                    {
                        StartStoryWait(RunMainlineStoryPlayable(activeStory, node));
                        return StoryTransition<Map10RuntimeStory>.Wait;
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
                        StartStoryWait(ContinueStoryAfterDelay(node.playableDuration,
                            StoryNext(node, "Exit")));
                        return StoryTransition<Map10RuntimeStory>.Wait;
                    }
                    nodeId = StoryNext(node, "Exit");
                    return StoryTransition<Map10RuntimeStory>.Next(nodeId);
                default:
                    throw new InvalidDataException("Unsupported official Map10 story node: " + node.type);
            }
        }

    }
}
