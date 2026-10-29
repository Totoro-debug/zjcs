using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    /// <summary>Connects the fixed official graph to the local Home session and HUD.</summary>
    public sealed class HomeTutorialHost : IHomeTutorialHost, IDisposable
    {
        private readonly HomeFeatureRuntime runtime;
        private readonly HomeHudView hud;
        private HomeTutorialGuide guide;
        private HomeTutorialNode node;
        private string receipt;
        private Button watchedButton;
        private UnityEngine.Events.UnityAction watchedButtonAction;
        private int observedFurnitureClick = -1;
        private int repairedFurniture = -1;
        private int collectedHangUp = -1;
        private int selectedOptionIndex = -1;
        private float movementStartedAt;
        private Vector3 movementStart;
        private Vector3 movementTarget;
        private AnimationCurve movementX;
        private AnimationCurve movementY;
        private bool disposed;

        public bool IsReady => !disposed && runtime != null && runtime.Scene != null
            && runtime.Session != null && runtime.Profile != null && hud != null;
        public Func<HomeTutorialGuide, HomeTutorialNode, string, int, bool> RecordOptionSelection { get; set; }

        public HomeTutorialHost(HomeFeatureRuntime runtime, HomeHudView hud)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            runtime.FurnitureClickedForTutorial += OnFurnitureClicked;
            runtime.FurnitureRepairedForTutorial += OnFurnitureRepaired;
            runtime.HangUpCollectedForTutorial += OnHangUpCollected;
            runtime.FastForwardAppliedForTutorial += OnFastForwardApplied;
        }

        public bool TryEvaluateCondition(HomeTutorialGuide currentGuide, HomeTutorialNode currentNode,
            out bool result, out string reason)
        {
            HomeTutorialNodePayload payload = Payload(currentNode);
            result = false;
            reason = null;
            switch (currentNode.type)
            {
                case "PlayerOnSceneConditionNode":
                    if (ReadInt(currentNode.payload, "SceneType", out int sceneType) && sceneType != 2)
                    {
                        reason = "PlayerOnSceneConditionNode scene type is outside this Home runtime: " + sceneType;
                        return false;
                    }
                    result = runtime.Scene != null;
                    return true;
                case "AllNoTipWinClosedConditionNode":
                    result = !hud.AnyModalOpen && !runtime.IsWishScreenOpen;
                    return true;
                case "TabItemActiveConditionNode":
                    string tabKey = string.IsNullOrEmpty(payload.StoryUIKey)
                        ? payload.StoryNodeKey : payload.StoryUIKey;
                    result = tabKey == "PublicBottomBar.HomeTabItem"
                        && (hud.SelectedTabIndex == 2) == (payload.IsOn != 0);
                    if (tabKey != "PublicBottomBar.HomeTabItem")
                    {
                        reason = "TabItemActiveConditionNode key is not registered by the local Home HUD: "
                            + tabKey;
                        return false;
                    }
                    return true;
                case "CanRepaireFurnitureConditionNode":
                    result = runtime.CanRepairFurnitureForTutorial(payload.Furniture);
                    return true;
                case "WaitHangUpUnlockMetConditionNode":
                    result = runtime.CanUnlockAnyHangUpForFurniture(payload.Furniture);
                    return true;
                case "PlayerConditionNode":
                case "QueryPlayerConditionNode":
                    return TryEvaluatePlayerCondition(payload, out result, out reason);
                case "CanGetFixedRewardNode":
                    if (payload.FixedRewardId != 7)
                    {
                        reason = "No fixed-reward rule is installed for official reward " + payload.FixedRewardId + ".";
                        return false;
                    }
                    result = runtime.TutorialCanGetFixedReward(payload.FixedRewardId);
                    return true;
                default:
                    reason = "No local condition adapter is registered for official node " + currentNode.type + ".";
                    return false;
            }
        }

        public HomeTutorialHostResult BeginOrResumeNode(HomeTutorialGuide currentGuide,
            HomeTutorialNode currentNode, IReadOnlyList<HomeTutorialDialogue> dialogues, string nodeReceipt,
            bool isResume, out string reason)
        {
            if (!IsReady)
            {
                reason = "Home runtime is not ready for tutorial execution.";
                return HomeTutorialHostResult.Pending;
            }
            if (!isResume || receipt != nodeReceipt)
            {
                ClearNodePresentation();
                guide = currentGuide;
                node = currentNode;
                receipt = nodeReceipt;
                observedFurnitureClick = -1;
                repairedFurniture = -1;
                collectedHangUp = -1;
                selectedOptionIndex = -1;
            }
            return StartOrPoll(dialogues, isResume, out reason);
        }

        public HomeTutorialHostResult PollNode(HomeTutorialGuide currentGuide,
            HomeTutorialNode currentNode, string nodeReceipt, out string reason)
        {
            if (receipt != nodeReceipt || node == null || guide == null)
            {
                reason = "Tutorial host lost the pending official node and cannot report success.";
                return HomeTutorialHostResult.Unsupported;
            }
            return PollCurrent(out reason);
        }

        public void EndGuide(HomeTutorialGuide endedGuide, bool completed)
        {
            ClearNodePresentation();
            hud.SetTutorialMainUiVisible(true);
            runtime.SetTutorialCameraDragEnabled(true);
            guide = null;
            node = null;
            receipt = null;
        }

        public string CollectionReceiptFor(int hangUpId)
        {
            if (node?.type != "GetFurnitureHangUpAwardNode" || !IsHangUpInCurrentNode(hangUpId)) return null;
            return receipt + ":collect:" + hangUpId.ToString(CultureInfo.InvariantCulture);
        }

        public string FastForwardReceiptFor(int hangUpId, int fastHangUpId)
        {
            if (node?.type != "FurnitureBedSleepNode" || Payload(node).Furniture != 0
                || fastHangUpId <= 0 || !runtime.Rules.HangUpsOf(0).Any(value => value.id == hangUpId
                    && value.fastHangUpId == fastHangUpId)) return null;
            return receipt + ":fast:" + fastHangUpId.ToString(CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearNodePresentation();
            runtime.FurnitureClickedForTutorial -= OnFurnitureClicked;
            runtime.FurnitureRepairedForTutorial -= OnFurnitureRepaired;
            runtime.HangUpCollectedForTutorial -= OnHangUpCollected;
            runtime.FastForwardAppliedForTutorial -= OnFastForwardApplied;
            RecordOptionSelection = null;
        }

        private HomeTutorialHostResult StartOrPoll(IReadOnlyList<HomeTutorialDialogue> dialogues,
            bool isResume, out string reason)
        {
            reason = null;
            HomeTutorialNodePayload payload = Payload(node);
            switch (node.type)
            {
                case "StartStoryNode":
                case "DialogNode":
                    if (!hud.HasTutorialDialogue)
                    {
                        hud.ShowOfficialTutorialDialogue(dialogues, () => dialogueCompleted = true);
                        dialogueCompleted = dialogues == null || dialogues.Count == 0;
                    }
                    return dialogueCompleted ? Succeeded(out reason) : Pending(out reason);
                case "OptionDialogNode":
                    if (!hud.HasTutorialDialogue)
                    {
                        HomeTutorialStoryOption[] options = (payload.Options ?? Array.Empty<int>())
                            .Select(optionId => runtime.Tutorials.StoryOption(guide.name, optionId)).ToArray();
                        if (options.Length == 0 || options.Any(value => value == null))
                        {
                            reason = "The official story option table has no local row for tutorial node "
                                + node.id + ".";
                            return HomeTutorialHostResult.Unsupported;
                        }
                        hud.ShowOfficialTutorialOptionDialog(dialogues, options, index =>
                        {
                            if (index < 0 || index >= options.Length || RecordOptionSelection == null
                                || !RecordOptionSelection(guide, node, receipt, index)) return false;
                            selectedOptionIndex = index;
                            return true;
                        });
                    }
                    return selectedOptionIndex >= 0 ? Succeeded(out reason) : Pending(out reason);
                case "RepairFurnitureNode":
                    return PollRepair(payload, out reason);
                case "GetFurnitureHangUpAwardNode":
                    return PollHangUpCollection(payload, out reason);
                case "GetFixAwardNode":
                    if (!runtime.TryClaimTutorialFixedReward(payload.AwardId, receipt, out reason))
                        return HomeTutorialHostResult.Failed;
                    return hud.AnyModalOpen ? Pending(out reason) : Succeeded(out reason);
                case "FurnitureBedSleepNode":
                    return PollBedSleep(payload, out reason);
                case "CameraMoveNode":
                    BeginCameraMove(payload);
                    return PollCameraMove(out reason);
                case "SetFreeCameraForcePosXNode":
                    SetCameraX(payload.PositionX);
                    return Succeeded(out reason);
                case "CameraDragNode":
                    runtime.SetTutorialCameraDragEnabled(payload.CanDrag != 0);
                    return Succeeded(out reason);
                case "HideAllUINode":
                    hud.SetTutorialMainUiVisible(false);
                    return Succeeded(out reason);
                case "ShowAllUINode":
                    hud.SetTutorialMainUiVisible(true);
                    hud.HideTutorialMask();
                    return Succeeded(out reason);
                case "ShowFullMaskNode":
                    hud.ShowTutorialFullMask();
                    return Succeeded(out reason);
                case "PlayMaskNode":
                    try { hud.ShowTutorialStoryUiMask(payload.StoryUIKey, payload.MaskSetting); }
                    catch (Exception error) { reason = error.Message; return HomeTutorialHostResult.Unsupported; }
                    return Succeeded(out reason);
                case "MaskHeapCartItemUnlockButtonNode":
                    Button unlock = hud.FindTutorialUnlockButton();
                    if (unlock == null)
                    {
                        reason = "The active Cart panel has no locally unlockable official hang-up button.";
                        return HomeTutorialHostResult.Unsupported;
                    }
                    try { hud.ShowTutorialButtonMask(unlock, payload.UnlockButtonMaskSet); }
                    catch (Exception error) { reason = error.Message; return HomeTutorialHostResult.Unsupported; }
                    return Succeeded(out reason);
                case "MaskFirstMergeFoodNode":
                    Transform recoveryItem = runtime.CookingRuntime?.GetDefaultRecoveryItem();
                    if (recoveryItem == null)
                    {
                        reason = "The official default recovery recipe is unavailable in the active Cooking panel.";
                        return HomeTutorialHostResult.Unsupported;
                    }
                    try
                    {
                        hud.ShowTutorialTargetMask(recoveryItem as RectTransform,
                            payload.mFirstMergeItemMaskSet);
                    }
                    catch (Exception error) { reason = error.Message; return HomeTutorialHostResult.Unsupported; }
                    return Succeeded(out reason);
                case "ClickBtnNode":
                    return PollButtonClick(payload, out reason);
                case "WaitWindowOpenedNode":
                    return PollWindowOpened(payload, out reason);
                case "WaitStoryUIActiveNode":
                case "WaitTabItemStateNode":
                    return PollHomeTab(payload, out reason);
                case "WaitPublicBottomBarActiveNode":
                    return hud.HasTutorialHomeTab ? Succeeded(out reason) : Pending(out reason);
                case "WaitAllNoTipWindowClosedNode":
                    return !hud.AnyModalOpen && !runtime.IsWishScreenOpen
                        ? Succeeded(out reason) : Pending(out reason);
                case "WaitCookingSelectItemNode":
                    return runtime.CookingRuntime != null && runtime.CookingRuntime.IsSelect
                        ? Succeeded(out reason) : Pending(out reason);
                case "HomeNPCPlayActionNode":
                    reason = "The official Home NPC action "
                        + (ReadString(node.payload, "BehaviourType", out string npcBehaviour) ? npcBehaviour : "<missing>")
                        + " has no converted native NPC animation clip.";
                    return HomeTutorialHostResult.Unsupported;
                case "HomePlayerPlayActionNode":
                    reason = "The official Home player action "
                        + (ReadString(node.payload, "BehaviourType", out string playerBehaviour) ? playerBehaviour : "<missing>")
                        + " has no converted native mesh animation clip.";
                    return HomeTutorialHostResult.Unsupported;
                case "FinishStoryConditionNode":
                    return !hud.AnyModalOpen && !runtime.IsWishScreenOpen
                        ? Succeeded(out reason) : Pending(out reason);
                default:
                    reason = "No local host action is registered for official node " + node.type + ".";
                    return HomeTutorialHostResult.Unsupported;
            }
        }

        private HomeTutorialHostResult PollCurrent(out string reason)
        {
            reason = null;
            HomeTutorialNodePayload payload = Payload(node);
            switch (node.type)
            {
                case "StartStoryNode":
                case "DialogNode":
                    return dialogueCompleted ? Succeeded(out reason) : Pending(out reason);
                case "OptionDialogNode":
                    return selectedOptionIndex >= 0 ? Succeeded(out reason) : Pending(out reason);
                case "RepairFurnitureNode": return PollRepair(payload, out reason);
                case "GetFurnitureHangUpAwardNode": return PollHangUpCollection(payload, out reason);
                case "FurnitureBedSleepNode": return PollBedSleep(payload, out reason);
                case "CameraMoveNode": return PollCameraMove(out reason);
                case "ClickBtnNode": return PollButtonClick(payload, out reason);
                case "WaitWindowOpenedNode": return PollWindowOpened(payload, out reason);
                case "WaitStoryUIActiveNode":
                case "WaitTabItemStateNode": return PollHomeTab(payload, out reason);
                case "WaitPublicBottomBarActiveNode":
                    return hud.HasTutorialHomeTab ? Succeeded(out reason) : Pending(out reason);
                case "WaitAllNoTipWindowClosedNode":
                case "FinishStoryConditionNode":
                    return !hud.AnyModalOpen && !runtime.IsWishScreenOpen
                        ? Succeeded(out reason) : Pending(out reason);
                case "GetFixAwardNode":
                    return !hud.AnyModalOpen ? Succeeded(out reason) : Pending(out reason);
                default:
                    reason = "Pending tutorial host state is not registered for official node " + node.type + ".";
                    return HomeTutorialHostResult.Unsupported;
            }
        }

        private HomeTutorialHostResult PollRepair(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            int furniture = payload.Furniture;
            if (runtime.Session.OwnsFurniture(furniture)) return Succeeded(out reason);
            if (observedFurnitureClick != furniture && hud.ActivePanelKey != "repair")
            {
                hud.ShowTutorialFurnitureMask(furniture, payload.FurnitureMaskSet);
                runtime.SetTutorialCameraDragEnabled(false);
                return Pending(out reason);
            }
            if (hud.ActivePanelKey == "repair")
            {
                Button repair = hud.FindTutorialButton("FurnitureRepairDialog.RepaireBtn");
                if (repair != null)
                {
                    hud.ShowTutorialButtonMask(repair, payload.RepaireBtnMaskSet);
                    return Pending(out reason);
                }
            }
            hud.HideTutorialMask();
            runtime.SetTutorialCameraDragEnabled(true);
            return Pending(out reason);
        }

        private HomeTutorialHostResult PollHangUpCollection(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            if (HasCurrentCollectionReceipt(payload.Furniture)) return Succeeded(out reason);
            if (collectedHangUp > 0 && IsHangUpInCurrentNode(collectedHangUp)) return Succeeded(out reason);
            if (observedFurnitureClick != payload.Furniture && !ExpectedPanelOpen(payload.Furniture))
            {
                hud.ShowTutorialFurnitureMask(payload.Furniture, payload.FurnitureMaskSet);
                runtime.SetTutorialCameraDragEnabled(false);
                return Pending(out reason);
            }
            if (ExpectedPanelOpen(payload.Furniture))
            {
                Button collect = payload.Furniture == 0
                    ? hud.FindTutorialButton("FurnitureBedDialog.CollectBtn")
                    : hud.FindTutorialButton("FurnitureCartDialog.CommonBtn");
                if (collect == null)
                {
                    reason = "The expected official hang-up claim button is missing from the open furniture panel.";
                    return HomeTutorialHostResult.Unsupported;
                }
                hud.ShowTutorialButtonMask(collect, payload.HangUpRewardBtnMaskSet);
                return Pending(out reason);
            }
            hud.HideTutorialMask();
            runtime.SetTutorialCameraDragEnabled(true);
            return Pending(out reason);
        }

        private HomeTutorialHostResult PollBedSleep(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            if (fastForwardApplied || runtime.TutorialFastForwardReceiptApplied(receipt)) return Succeeded(out reason);
            if (observedFurnitureClick != payload.Furniture && hud.ActivePanelKey != "bed")
            {
                hud.ShowTutorialFurnitureMask(payload.Furniture, payload.BedMaskSetting);
                runtime.SetTutorialCameraDragEnabled(false);
                return Pending(out reason);
            }
            if (hud.ActivePanelKey == "bed")
            {
                Button speed = hud.FindTutorialButton("FurnitureBedDialog.FastHangUpBtn");
                if (speed == null)
                {
                    reason = "The official bed fast hang-up button is missing from the local Bed panel.";
                    return HomeTutorialHostResult.Unsupported;
                }
                hud.ShowTutorialButtonMask(speed, payload.FastHangUpBtnMaskSet);
                return Pending(out reason);
            }
            hud.HideTutorialMask();
            runtime.SetTutorialCameraDragEnabled(true);
            return Pending(out reason);
        }

        private HomeTutorialHostResult PollButtonClick(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            string key = payload.StoryUIKey;
            if (watchedButton == null)
            {
                watchedButton = hud.FindTutorialButton(key);
                if (watchedButton == null)
                {
                    reason = "The official tutorial button is not implemented in the local Home HUD: " + key;
                    return HomeTutorialHostResult.Unsupported;
                }
                clickCount = 0;
                watchedButtonAction = () => clickCount++;
                watchedButton.onClick.AddListener(watchedButtonAction);
                hud.ShowTutorialButtonMask(watchedButton, payload.ButtonMaskSet);
            }
            if (clickCount >= Math.Max(1, payload.ClickTimes)) return Succeeded(out reason);
            return Pending(out reason);
        }

        private HomeTutorialHostResult PollWindowOpened(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            string path = ReadString(node.payload, "mWindowPath", out string parsedPath)
                ? parsedPath : null;
            if (string.IsNullOrEmpty(path))
            {
                reason = "WaitWindowOpenedNode has no official window path.";
                return HomeTutorialHostResult.Unsupported;
            }
            if (path.Contains("FurnitureCartDialog", StringComparison.Ordinal))
            {
                if (hud.ActivePanelKey == "cart") return Succeeded(out reason);
                try { hud.ShowTutorialFurnitureMask(10, null); }
                catch (Exception error) { reason = error.Message; return HomeTutorialHostResult.Unsupported; }
                return Pending(out reason);
            }
            if (path.Contains("FurnitureBedDialog", StringComparison.Ordinal))
            {
                if (hud.ActivePanelKey == "bed") return Succeeded(out reason);
                try { hud.ShowTutorialFurnitureMask(0, null); }
                catch (Exception error) { reason = error.Message; return HomeTutorialHostResult.Unsupported; }
                return Pending(out reason);
            }
            reason = "The official window is not provided by the local Home HUD: " + path;
            return HomeTutorialHostResult.Unsupported;
        }

        private HomeTutorialHostResult PollHomeTab(HomeTutorialNodePayload payload, out string reason)
        {
            reason = null;
            if (payload.StoryUIKey != "PublicBottomBar.HomeTabItem")
            {
                reason = "The official story UI key is not registered by the local Home HUD: " + payload.StoryUIKey;
                return HomeTutorialHostResult.Unsupported;
            }
            bool requiredOn = payload.IsOn != 0 || payload.Active != 0;
            bool matches = (hud.SelectedTabIndex == 2) == requiredOn;
            if (matches) return Succeeded(out reason);
            hud.ShowTutorialStoryUiMask(payload.StoryUIKey, payload.TabMaskSet);
            return Pending(out reason);
        }

        private HomeTutorialHostResult PollCameraMove(out string reason)
        {
            reason = null;
            HomeTutorialNodePayload payload = Payload(node);
            float duration = Math.Max(0f, payload.Duration);
            float percent = duration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - movementStartedAt) / duration);
            float x = movementX == null ? percent : movementX.Evaluate(percent);
            float y = movementY == null ? percent : movementY.Evaluate(percent);
            Vector3 next = new Vector3(
                Mathf.LerpUnclamped(movementStart.x, movementTarget.x, x),
                Mathf.LerpUnclamped(movementStart.y, movementTarget.y, y),
                movementStart.z);
            runtime.Scene.Camera.transform.position = runtime.Scene.Clamp(next);
            return percent >= 1f ? Succeeded(out reason) : Pending(out reason);
        }

        private void BeginCameraMove(HomeTutorialNodePayload payload)
        {
            movementStart = runtime.Scene.Camera.transform.position;
            movementTarget = new Vector3(payload.TargetPosition?.x ?? movementStart.x,
                payload.TargetPosition?.y ?? movementStart.y, movementStart.z);
            movementStartedAt = Time.unscaledTime;
            movementX = Curve(payload.MoveXCurve);
            movementY = Curve(payload.MoveYCurve);
        }

        private void SetCameraX(float x)
        {
            Vector3 current = runtime.Scene.Camera.transform.position;
            runtime.Scene.Camera.transform.position = runtime.Scene.Clamp(new Vector3(x, current.y, current.z));
        }

        private bool TryEvaluatePlayerCondition(HomeTutorialNodePayload payload, out bool result, out string reason)
        {
            result = false;
            reason = null;
            string type = (payload.Type ?? string.Empty).Trim();
            string param = payload.Param ?? string.Empty;
            bool met;
            switch (type)
            {
                case "Level":
                    if (!ReadInt(param, "Level", out int level)) return Unsupported("Level condition has no Level parameter.", out reason);
                    met = runtime.Level >= level;
                    break;
                case "HasItem":
                    if (!ReadInt(param, "ClassId", out int itemId)) return Unsupported("HasItem condition has no ClassId parameter.", out reason);
                    long count = ReadLong(param, "Count", out long required) ? required : 1;
                    met = runtime.ItemCount(itemId) >= count;
                    break;
                case "StoryCount":
                    if (!ReadString(param, "StoryKey", out string storyKey)
                        || !ReadInt(param, "Count", out int targetCount))
                        return Unsupported("StoryCount condition parameters are incomplete.", out reason);
                    string op = ReadString(param, "Op", out string parsedOp) ? parsedOp : ">=";
                    met = Compare(runtime.StoryCount(storyKey), op, targetCount);
                    break;
                default:
                    return Unsupported("Player condition type has no local adapter: " + type, out reason);
            }
            result = payload.Not != 0 ? !met : met;
            return true;
        }

        private bool ExpectedPanelOpen(int furniture) => furniture == 0
            ? hud.ActivePanelKey == "bed" : furniture == 10 && hud.ActivePanelKey == "cart";

        private bool HasCurrentCollectionReceipt(int furniture)
        {
            foreach (HomeHangUpRecord hangUp in runtime.Rules.HangUpsOf(furniture))
            {
                string expected = receipt + ":collect:" + hangUp.id.ToString(CultureInfo.InvariantCulture);
                if (runtime.Session.Save.AppliedReceipts.Contains(expected)) return true;
            }
            return false;
        }

        private bool IsHangUpInCurrentNode(int hangUpId) => runtime.Rules.HangUpsOf(Payload(node).Furniture)
            .Any(value => value.id == hangUpId);

        private void OnFurnitureClicked(int furnitureType)
        {
            observedFurnitureClick = furnitureType;
            if (node?.type == "RepairFurnitureNode" && Payload(node).Furniture == furnitureType
                || node?.type == "GetFurnitureHangUpAwardNode" && Payload(node).Furniture == furnitureType
                || node?.type == "FurnitureBedSleepNode" && Payload(node).Furniture == furnitureType)
            {
                hud.HideTutorialMask();
                runtime.SetTutorialCameraDragEnabled(true);
            }
        }

        private void OnFurnitureRepaired(int furnitureType)
        {
            if (node?.type == "RepairFurnitureNode" && Payload(node).Furniture == furnitureType)
                repairedFurniture = furnitureType;
        }

        private void OnHangUpCollected(int hangUpId, long count, string appliedReceipt)
        {
            if (count > 0 && appliedReceipt == CollectionReceiptFor(hangUpId)) collectedHangUp = hangUpId;
        }

        private void OnFastForwardApplied(string appliedReceipt)
        {
            if (node?.type == "FurnitureBedSleepNode" && appliedReceipt != null
                && appliedReceipt.StartsWith(receipt + ":fast:", StringComparison.Ordinal)) fastForwardApplied = true;
        }

        private HomeTutorialHostResult PollRepairState(HomeTutorialNodePayload payload, out string reason)
        {
            if (runtime.Session.OwnsFurniture(payload.Furniture) || repairedFurniture == payload.Furniture)
                return Succeeded(out reason);
            return PollRepair(payload, out reason);
        }

        private static HomeTutorialNodePayload Payload(HomeTutorialNode value)
        {
            if (string.IsNullOrEmpty(value?.payload)) return new HomeTutorialNodePayload();
            return JsonUtility.FromJson<HomeTutorialNodePayload>(value.payload) ?? new HomeTutorialNodePayload();
        }

        private static AnimationCurve Curve(TutorialCurve data)
        {
            if (data?.m_Curve == null || data.m_Curve.Length == 0) return null;
            return new AnimationCurve(data.m_Curve.Select(key =>
                new Keyframe(key.time, key.value, key.inSlope, key.outSlope)).ToArray());
        }

        private static bool ReadInt(string source, string key, out int value)
        {
            value = 0;
            Match match = Regex.Match(source ?? string.Empty,
                "[\\\"']" + Regex.Escape(key) + "[\\\"']\\s*:\\s*(-?\\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool ReadLong(string source, string key, out long value)
        {
            value = 0;
            Match match = Regex.Match(source ?? string.Empty,
                "[\\\"']" + Regex.Escape(key) + "[\\\"']\\s*:\\s*(-?\\d+)");
            return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        private static bool ReadString(string source, string key, out string value)
        {
            value = null;
            Match match = Regex.Match(source ?? string.Empty,
                "[\\\"']" + Regex.Escape(key) + "[\\\"']\\s*:\\s*[\\\"']([^\\\"']*)[\\\"']");
            if (!match.Success) return false;
            value = match.Groups[1].Value;
            return true;
        }

        private static bool Compare(long value, string operation, long target) => operation switch
        {
            "==" => value == target,
            "!=" => value != target,
            ">" => value > target,
            "<" => value < target,
            ">=" => value >= target,
            "<=" => value <= target,
            _ => false
        };

        private static bool Unsupported(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static HomeTutorialHostResult Pending(out string reason)
        {
            reason = null;
            return HomeTutorialHostResult.Pending;
        }

        private static HomeTutorialHostResult Succeeded(out string reason)
        {
            reason = null;
            return HomeTutorialHostResult.Succeeded;
        }

        private void ClearNodePresentation()
        {
            RemoveButtonListener();
            hud?.ClearOfficialTutorialDialogue();
            hud?.HideTutorialMask();
            hud?.SetTutorialMainUiVisible(true);
            runtime?.SetTutorialCameraDragEnabled(true);
            movementX = null;
            movementY = null;
            dialogueCompleted = false;
            fastForwardApplied = false;
        }

        private void RemoveButtonListener()
        {
            if (watchedButton != null && watchedButtonAction != null)
                watchedButton.onClick.RemoveListener(watchedButtonAction);
            watchedButton = null;
            watchedButtonAction = null;
            clickCount = 0;
        }

        private bool dialogueCompleted;
        private bool fastForwardApplied;
        private int clickCount;
    }
}
