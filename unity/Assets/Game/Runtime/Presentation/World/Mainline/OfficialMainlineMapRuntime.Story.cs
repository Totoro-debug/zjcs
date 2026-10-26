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
            activeStoryRootKey = key;
            storyRunning = true;
            storyContextEntity = context;
            storyFinished = completed;
            SetStoryStatus(key, 1);
            storyEntities.Clear();
            storyEntityClasses.Clear();
            if (key == "SavingChick" && context != null && context.EntityClassId == 8417)
            {
                storyHiddenContextEntity = mapEntityObject(context.PlacementId);
                if (storyHiddenContextEntity != null) storyHiddenContextEntity.SetActive(false);
            }
            RunStoryGraph(activeStory.entryNodeId);
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
                        case "FinishStoryNode":
                            if (FinishStoryFrame(out nodeId)) return;
                            continue;
                        case "SetStoryStateNode":
                            SetStoryStatus(node.storyName, node.storyStatus);
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
                            AddStoryEntity(node, new Vector2(node.positionX, node.positionY));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "AddMapExclusiveNpcAroundPlayerNode":
                            MapGridPoint nearPlayer = FindAdjacentStoryGrid();
                            AddStoryEntity(node, new Vector2(nearPlayer.X, nearPlayer.Y));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "MapEntityMoveNode":
                            int entityNodeId = StoryNext(node, "OperateEntity");
                            if (!storyEntities.TryGetValue(entityNodeId, out GameObject movingEntity))
                                throw new InvalidDataException("Official Map10 story move has no spawned entity: " + entityNodeId);
                            StartCoroutine(MoveStoryEntity(movingEntity, node.destinationX, node.destinationY,
                                Mathf.Max(0.1f, node.moveSpeed), StoryNext(node, "Exit")));
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

        private IEnumerator ContinueStoryAfterDelay(float seconds, int nextNodeId)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (storyRunning) RunStoryGraph(nextNodeId);
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
            if (targetNode.type == "GetPosAroundStoryObjectNode")
            {
                if (MapProfile.HasNativeUi)
                    return ResolveRegisteredStoryPoint(targetNode.registerKey)
                        + new Vector2(targetNode.offsetX, targetNode.offsetY);
                Map10RuntimeEntity registered = targetNode.registerKey == "Fangjianbei"
                    ? mapEntities.Values.FirstOrDefault(value => value.EntityClassId == 6001)
                    : null;
                if (registered == null)
                    throw new InvalidDataException("Official Map10 story register key is not bound: " + targetNode.registerKey);
                return new Vector2(registered.Grid.X + targetNode.offsetX,
                    registered.Grid.Y + targetNode.offsetY);
            }
            if (targetNode.type != "GetEntityPosNode")
                return new Vector2(cameraNode.moveTargetX, cameraNode.moveTargetY);
            // GetEntityBaseNode.GetEntityType.MainPlayer is 0. A stale LoEntityId
            // field is ignored for this mode by the official GetEntityPosNode.
            if (targetNode.selectGetType == 0)
                return new Vector2(navigation.currentGrid.X + targetNode.offsetX, navigation.currentGrid.Y + targetNode.offsetY);
            if (targetNode.selectGetType == 6)
            {
                if (MapProfile.HasNativeUi)
                    return ResolveStoryTalkCenter() + new Vector2(targetNode.offsetX, targetNode.offsetY);
                GameObject storyNpc = storyEntities.OrderBy(value => value.Key).Select(value => value.Value)
                    .LastOrDefault(value => value != null);
                if (storyNpc != null) return StoryGridPosition(storyNpc.transform.position);
                if (storyContextEntity != null)
                    return new Vector2(storyContextEntity.Grid.X, storyContextEntity.Grid.Y);
            }
            else if (targetNode.selectGetType == 4 || targetNode.selectGetType == 5)
            {
                Map10RuntimeLogicTag tag = targetNode.selectGetType == 5
                    ? mapLogicTags.Values.FirstOrDefault(value => value.Tag == targetNode.tag) : null;
                int targetId = targetNode.selectGetType == 4 ? targetNode.loEntityId : tag?.LogicId ?? 0;
                if ((tag == null || tag.MapClass == mapClassId)
                    && mapEntities.TryGetValue(targetId, out Map10RuntimeEntity targetEntity))
                    return new Vector2(targetEntity.Grid.X + targetNode.offsetX, targetEntity.Grid.Y + targetNode.offsetY);
                string target = tag == null ? "logic entity " + targetId : "Map" + tag.MapClass + " " + targetNode.tag;
                throw new InvalidDataException("Official story camera target is outside the loaded map: " + target);
            }
            throw new InvalidDataException("Official Map10 story camera target cannot be resolved: " + targetNode.tag);
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
            if (logicId <= 0 || !mapEntities.TryGetValue(logicId, out Map10RuntimeEntity entity)) return false;
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
