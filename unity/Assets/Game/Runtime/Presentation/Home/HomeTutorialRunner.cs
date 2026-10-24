using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    public enum HomeTutorialHostResult
    {
        Pending,
        Succeeded,
        Failed,
        Unsupported
    }

    /// <summary>
    /// The host must use local profile rules and receipts for game mutations. Returning
    /// Unsupported keeps the graph at that node; it never treats missing server behavior as success.
    /// </summary>
    public interface IHomeTutorialHost
    {
        bool IsReady { get; }

        bool TryEvaluateCondition(HomeTutorialGuide guide, HomeTutorialNode node,
            out bool result, out string reason);

        HomeTutorialHostResult BeginOrResumeNode(HomeTutorialGuide guide, HomeTutorialNode node,
            IReadOnlyList<HomeTutorialDialogue> dialogues, string receipt, bool isResume, out string reason);

        HomeTutorialHostResult PollNode(HomeTutorialGuide guide, HomeTutorialNode node,
            string receipt, out string reason);

        void EndGuide(HomeTutorialGuide guide, bool completed);
    }

    [Serializable]
    public sealed class HomeTutorialStoryState
    {
        public string Name;
        public int State;
        public int Count;
    }

    [Serializable]
    public sealed class HomeTutorialGuideProgress
    {
        public string Name;
        public bool Completed;
    }

    [Serializable]
    internal sealed class HomeTutorialRunnerSave
    {
        public int Schema = 2;
        public string Version;
        public string SourceSha256;
        public string QueuedGuide;
        public string ActiveGuide;
        public int ActiveNodeId;
        public bool Pending;
        public float WaitRemaining;
        public int OptionSelectionNodeId = -1;
        public int OptionSelectionIndex = -1;
        public string OptionSelectionReceipt;
        public List<HomeTutorialStoryState> Stories = new List<HomeTutorialStoryState>();
        public List<HomeTutorialGuideProgress> Guides = new List<HomeTutorialGuideProgress>();
        public List<string> AppliedReceipts = new List<string>();
    }

    [Serializable]
    internal sealed class HomeTutorialNodePayload
    {
        public int NodeId;
        public int CanStart = 1;
        public int ConditionSatisfied = 1;
        public int Input;
        public int Status;
        public int State;
        public int Furniture;
        public int DialogId;
        public int CanDrag = 1;
        public float Duration;
        public float PositionX;
        public float PositionY;
        public float Seconds;
        public int ClickTimes = 1;
        public int Option;
        public int Count;
        public int AwardId;
        public int FixedRewardId;
        public int[] Options;
        public int IsOn;
        public int Active;
        public int Not;
        public string StoryName;
        public string Name;
        public string Type;
        public string Param;
        public string StoryUIKey;
        public string StoryNodeKey;
        public TutorialScalar Origin;
        public TutorialScalar Target;
        public TutorialPosition TargetPosition;
        public TutorialCurve MoveXCurve;
        public TutorialCurve MoveYCurve;
        public TutorialMaskSetting FurnitureMaskSet;
        public TutorialMaskSetting RepaireBtnMaskSet;
        public TutorialMaskSetting HangUpRewardBtnMaskSet;
        public TutorialMaskSetting ButtonMaskSet;
        public TutorialMaskSetting MaskSetting;
        public TutorialMaskSetting TabMaskSet;
        public TutorialMaskSetting UnlockButtonMaskSet;
        public TutorialMaskSetting BedMaskSetting;
        public TutorialMaskSetting FastHangUpBtnMaskSet;
        public TutorialMaskSetting mFirstMergeItemMaskSet;
    }

    [Serializable]
    internal sealed class TutorialPosition
    {
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class TutorialCurve
    {
        public TutorialKeyframe[] m_Curve;
    }

    [Serializable]
    internal sealed class TutorialKeyframe
    {
        public float time;
        public float value;
        public float inSlope;
        public float outSlope;
    }

    [Serializable]
    internal sealed class TutorialMaskSetting
    {
        public TutorialVector Offset;
        public TutorialVector Scale;
        public int ShowMask = 1;
    }

    [Serializable]
    internal sealed class TutorialVector
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    internal sealed class TutorialScalar
    {
        public int mValueType;
        public int mInt;
        public long mLong;
        public float mFloat;
        public double mDouble;
        public int mBool;
        public string mString;
    }

    /// <summary>
    /// Executes the six fixed-version Home tutorial graphs as connected node/port flows.
    /// Presentation and game mutations are delegated to an explicit host; graph state and
    /// story status are stored locally under the selected profile.
    /// </summary>
    public sealed class HomeTutorialRunner
    {
        private const int MaxSynchronousNodesPerTick = 128;
        private static readonly HashSet<string> HostActionNodeTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "CameraDragNode", "CameraMoveNode", "ClickBtnNode", "DialogNode", "FurnitureBedSleepNode",
            "GetFixAwardNode", "GetFurnitureHangUpAwardNode", "HideAllUINode", "HomeNPCPlayActionNode",
            "HomePlayerPlayActionNode", "MaskFirstMergeFoodNode", "MaskHeapCartItemUnlockButtonNode",
            "OptionDialogNode", "PlayMaskNode", "RepairFurnitureNode", "SetFreeCameraForcePosXNode",
            "ShowAllUINode", "ShowFullMaskNode", "StartStoryNode", "WaitAllNoTipWindowClosedNode",
            "WaitCookingSelectItemNode", "WaitPublicBottomBarActiveNode", "WaitStoryUIActiveNode",
            "WaitTabItemStateNode", "WaitWindowOpenedNode"
        };
        private readonly HomeTutorialCatalog catalog;
        private readonly IHomeTutorialHost host;
        private readonly string savePath;
        private readonly HomeTutorialRunnerSave save;
        private HomeTutorialGuide activeGuide;
        private Dictionary<int, HomeTutorialNode> nodesById;
        private Dictionary<string, List<HomeTutorialConnectionSource>> incoming;
        private bool resumePending;

        public string ActiveGuideName => activeGuide?.name ?? save.ActiveGuide;
        public int ActiveNodeId => save.ActiveNodeId;
        public bool IsPending => save.Pending;
        public bool IsBlocked { get; private set; }
        public string LastError { get; private set; }
        public bool HasQueuedGuide => !string.IsNullOrEmpty(save.QueuedGuide);
        public bool HasActiveGuide => activeGuide != null;

        public int LocalStoryState(string name) => StoryState(name);
        public int LocalStoryCount(string name) => StoryCount(name);
        public bool IsGuideCompleted(string name) =>
            (save.Guides ?? new List<HomeTutorialGuideProgress>()).Any(value => value.Name == name && value.Completed);

        public event Action StateChanged;

        public HomeTutorialRunner(HomeTutorialCatalog catalog, IHomeTutorialHost host, string profileSavePath)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrWhiteSpace(profileSavePath))
                throw new ArgumentException("A profile-local tutorial save path is required.", nameof(profileSavePath));
            savePath = profileSavePath;
            save = Load(savePath, catalog.Data.version, catalog.Data.sourceSha256);
            if (!string.IsNullOrEmpty(save.ActiveGuide))
            {
                activeGuide = catalog.Guide(save.ActiveGuide);
                if (activeGuide == null)
                    throw new FormatException("Saved tutorial guide is missing from the fixed-version catalog: " + save.ActiveGuide);
                BuildIndexes(activeGuide);
                resumePending = save.Pending;
            }
        }

        /// <summary>Queues an official guide. Readiness gates are re-evaluated while it is queued.</summary>
        public bool QueueGuide(string guideName)
        {
            HomeTutorialGuide guide = catalog.Guide(guideName);
            if (guide == null) return false;
            if (activeGuide != null)
            {
                if (activeGuide.name != guideName || !IsBlocked) return false;
                IsBlocked = false;
                LastError = null;
                resumePending = save.Pending;
                Persist();
                return true;
            }
            if (IsGuideCompleted(guideName)) return false;
            save.QueuedGuide = guideName;
            LastError = null;
            IsBlocked = false;
            Persist();
            StateChanged?.Invoke();
            return true;
        }

        public bool RecordOptionSelection(HomeTutorialGuide guide, HomeTutorialNode node,
            string receipt, int optionIndex)
        {
            if (activeGuide == null || guide == null || node == null || guide.name != activeGuide.name
                || node.type != "OptionDialogNode" || node.id != save.ActiveNodeId || !save.Pending
                || receipt != ReceiptFor(activeGuide, node)) return false;
            int[] options = Payload(node).Options ?? Array.Empty<int>();
            if (optionIndex < 0 || optionIndex >= options.Length
                || !HasConnection(node, OptionPortName(optionIndex))) return false;
            if (save.OptionSelectionIndex >= 0)
                return save.OptionSelectionNodeId == node.id && save.OptionSelectionIndex == optionIndex
                    && save.OptionSelectionReceipt == receipt;

            save.OptionSelectionNodeId = node.id;
            save.OptionSelectionIndex = optionIndex;
            save.OptionSelectionReceipt = receipt;
            Persist();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Retries the saved node after its required offline host behavior becomes available.</summary>
        public void RetryBlocked()
        {
            if (!IsBlocked) return;
            IsBlocked = false;
            LastError = null;
            resumePending = save.Pending;
            Persist();
        }

        /// <summary>Persists the current node and releases transient host presentation.</summary>
        public void Suspend()
        {
            Persist();
            resumePending = save.Pending;
            if (activeGuide != null) host.EndGuide(activeGuide, false);
        }

        /// <summary>Advances the graph. Pass Time.unscaledDeltaTime from the owning runtime.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (IsBlocked || !host.IsReady) return;
            if (activeGuide == null)
            {
                if (string.IsNullOrEmpty(save.QueuedGuide)) return;
                HomeTutorialGuide queued = catalog.Guide(save.QueuedGuide);
                if (queued == null) { Block("Queued guide is missing from the catalog."); return; }
                BuildIndexes(queued);
                if (!TryGetStartNode(queued, out HomeTutorialNode start, out HomeTutorialNode startGate))
                {
                    Block("Guide has no unambiguous StartStoryNode/WaitStartStoryNode: " + queued.name);
                    return;
                }
                if (!EvaluateStartGate(queued, startGate, out bool canStart, out string reason))
                {
                    Block(reason);
                    return;
                }
                if (!canStart) return;
                activeGuide = queued;
                save.QueuedGuide = null;
                save.ActiveGuide = queued.name;
                save.ActiveNodeId = start.id;
                save.Pending = false;
                save.WaitRemaining = 0f;
                resumePending = false;
                Persist();
                StateChanged?.Invoke();
            }

            if (save.Pending && save.ActiveNodeId >= 0
                && NodeById(save.ActiveNodeId)?.type == "WaitForSecondsNode")
            {
                save.WaitRemaining = Math.Max(0f, save.WaitRemaining - Math.Max(0f, unscaledDeltaTime));
                if (save.WaitRemaining <= 0f)
                {
                    save.Pending = false;
                    Advance("Exit");
                    RunSynchronousNodes();
                }
                else Persist();
                return;
            }

            if (save.OptionSelectionIndex >= 0)
            {
                if (!MatchesSavedOptionSelection(NodeById(save.ActiveNodeId)))
                {
                    Block("Saved tutorial option selection does not match the active official node.");
                    return;
                }
                int selectedIndex = save.OptionSelectionIndex;
                ClearOptionSelection();
                Advance(OptionPortName(selectedIndex));
                RunSynchronousNodes();
                return;
            }

            if (save.Pending)
            {
                HomeTutorialNode pendingNode = NodeById(save.ActiveNodeId);
                if (pendingNode == null) { Block("Saved pending tutorial node is missing."); return; }
                string receipt = ReceiptFor(activeGuide, pendingNode);
                string reason;
                HomeTutorialHostResult polled = resumePending
                    ? host.BeginOrResumeNode(activeGuide, pendingNode, DialoguesFor(activeGuide, pendingNode),
                        receipt, true, out reason)
                    : host.PollNode(activeGuide, pendingNode, receipt, out reason);
                resumePending = false;
                HandleHostResult(pendingNode, polled, reason, true);
                if (save.Pending || IsBlocked || activeGuide == null) return;
            }
            RunSynchronousNodes();
        }

        private void RunSynchronousNodes()
        {
            for (int i = 0; i < MaxSynchronousNodesPerTick && activeGuide != null && !IsBlocked && !save.Pending; i++)
            {
                HomeTutorialNode node = NodeById(save.ActiveNodeId);
                if (node == null) { Block("Tutorial connection points to a missing node: " + save.ActiveNodeId); return; }
                switch (node.type)
                {
                    case "FinishStoryNode":
                        CompleteGuide();
                        return;
                    case "FinishStoryConditionNode":
                        BeginHostNode(node);
                        return;
                    case "SetStoryStateNode":
                        ApplyStoryState(node);
                        Advance("Exit");
                        continue;
                    case "IfNode":
                        if (!TryEvaluateInput(node, "Input", Payload(node).Input != 0, out bool condition, out string conditionError))
                        {
                            Block(conditionError);
                            return;
                        }
                        Advance(condition ? "True" : "False");
                        continue;
                    case "WaitForSecondsNode":
                        save.WaitRemaining = Math.Max(0f, Payload(node).Seconds);
                        save.Pending = save.WaitRemaining > 0f;
                        Persist();
                        if (!save.Pending) { Advance("Exit"); continue; }
                        return;
                    case "CanGetFixedRewardNode":
                    case "IsStoryStateNode":
                    case "NotNode":
                    case "CompareNode":
                    case "QueryStoryCountNode":
                    case "QueryPlayerConditionNode":
                    case "PlayerConditionNode":
                    case "PlayerOnSceneConditionNode":
                    case "CanRepaireFurnitureConditionNode":
                    case "AllNoTipWinClosedConditionNode":
                    case "TabItemActiveConditionNode":
                    case "WaitHangUpUnlockMetConditionNode":
                    case "WaitStartStoryNode":
                        Block(node.type + " is a condition node and cannot execute as a flow node.");
                        return;
                    default:
                        if (HostActionNodeTypes.Contains(node.type)) BeginHostNode(node);
                        else Block("No runtime implementation is registered for official node " + node.type + ".");
                        return;
                }
            }
            if (activeGuide != null && !IsBlocked && !save.Pending)
                Block("Tutorial exceeded the synchronous node limit; a graph loop may be malformed.");
        }

        private void BeginHostNode(HomeTutorialNode node)
        {
            string receipt = ReceiptFor(activeGuide, node);
            HomeTutorialHostResult result = host.BeginOrResumeNode(activeGuide, node,
                DialoguesFor(activeGuide, node), receipt, resumePending, out string reason);
            resumePending = false;
            HandleHostResult(node, result, reason, false);
        }

        private IReadOnlyList<HomeTutorialDialogue> DialoguesFor(HomeTutorialGuide guide, HomeTutorialNode node)
        {
            if (node == null) return Array.Empty<HomeTutorialDialogue>();
            int dialogueId = node.id;
            if (node.type == "StartStoryNode") dialogueId = 0;
            else if (node.type == "DialogNode" || node.type == "OptionDialogNode")
            {
                HomeTutorialNodePayload payload = Payload(node);
                dialogueId = payload.DialogId;
            }
            return catalog.Dialogues(guide.name, dialogueId);
        }

        private void HandleHostResult(HomeTutorialNode node, HomeTutorialHostResult result, string reason,
            bool cameFromPending)
        {
            switch (result)
            {
                case HomeTutorialHostResult.Pending:
                    save.Pending = true;
                    Persist();
                    StateChanged?.Invoke();
                    return;
                case HomeTutorialHostResult.Succeeded:
                    save.Pending = false;
                    Persist();
                    if (node.type == "OptionDialogNode")
                    {
                        if (!MatchesSavedOptionSelection(node))
                        {
                            Block("OptionDialogNode completed without a durable official option selection.");
                            return;
                        }
                        int selectedIndex = save.OptionSelectionIndex;
                        ClearOptionSelection();
                        Advance(OptionPortName(selectedIndex));
                        return;
                    }
                    if (node.type == "FinishStoryConditionNode") CompleteGuide();
                    else Advance("Exit");
                    return;
                case HomeTutorialHostResult.Failed:
                    save.Pending = false;
                    if (HasConnection(node, "FailExit")) Advance("FailExit");
                    else Block(string.IsNullOrEmpty(reason)
                        ? node.type + " did not report a successful local result."
                        : reason);
                    return;
                default:
                    Block(string.IsNullOrEmpty(reason)
                        ? "Host behavior is not connected for official node " + node.type + "."
                        : reason);
                    return;
            }
        }

        private bool EvaluateStartGate(HomeTutorialGuide guide, HomeTutorialNode gate,
            out bool canStart, out string reason)
        {
            HomeTutorialNodePayload payload = Payload(gate);
            if (!TryEvaluateInput(gate, "CanStart", payload.CanStart != 0, out bool allowed, out reason))
            {
                canStart = false;
                return false;
            }
            if (!TryEvaluateInput(gate, "ConditionSatisfied", payload.ConditionSatisfied != 0,
                    out bool satisfied, out reason))
            {
                canStart = false;
                return false;
            }
            canStart = allowed && satisfied;
            return true;
        }

        private bool TryGetStartNode(HomeTutorialGuide guide, out HomeTutorialNode start,
            out HomeTutorialNode gate)
        {
            start = guide.nodes.FirstOrDefault(node => node.type == "StartStoryNode");
            gate = guide.nodes.FirstOrDefault(node => node.type == "WaitStartStoryNode");
            return start != null && gate != null
                && guide.nodes.Count(node => node.type == "StartStoryNode") == 1
                && guide.nodes.Count(node => node.type == "WaitStartStoryNode") == 1;
        }

        private bool TryEvaluateInput(HomeTutorialNode node, string inputName, bool fallback,
            out bool result, out string reason)
        {
            if (!TryGetInputSource(node.id, inputName, out HomeTutorialNode source, out string outputName))
            {
                result = fallback;
                reason = null;
                return true;
            }
            if (!TryEvaluateOutput(source, outputName, out result, out reason)) return false;
            return true;
        }

        private bool TryEvaluateOutput(HomeTutorialNode node, string outputName, out bool result, out string reason)
        {
            HomeTutorialNodePayload payload = Payload(node);
            switch (node.type)
            {
                case "IsStoryStateNode":
                    result = StoryState(payload.StoryName) == payload.Status;
                    reason = null;
                    return true;
                case "NotNode":
                    if (!TryEvaluateInput(node, "Input", payload.Input != 0, out bool input, out reason))
                    {
                        result = false;
                        return false;
                    }
                    result = !input;
                    return true;
                case "CompareNode":
                    if (!TryReadComparable(node, "Origin", payload.Origin, out double origin, out string originText, out reason)
                        || !TryReadComparable(node, "Target", payload.Target, out double target, out string targetText, out reason))
                    {
                        result = false;
                        return false;
                    }
                    switch (payload.Option)
                    {
                        case 0: result = originText != null || targetText != null ? originText == targetText : origin == target; break;
                        case 1: result = origin > target; break;
                        case 2: result = origin < target; break;
                        case 3: result = origin >= target; break;
                        case 4: result = origin <= target; break;
                        case 5: result = originText != null || targetText != null ? originText != targetText : origin != target; break;
                        default: result = false; break;
                    }
                    reason = null;
                    return true;
                case "QueryStoryCountNode":
                    result = StoryCount(payload.StoryName) != 0;
                    reason = null;
                    return true;
                case "CanGetFixedRewardNode":
                case "QueryPlayerConditionNode":
                case "PlayerConditionNode":
                case "PlayerOnSceneConditionNode":
                case "CanRepaireFurnitureConditionNode":
                case "AllNoTipWinClosedConditionNode":
                case "TabItemActiveConditionNode":
                case "WaitHangUpUnlockMetConditionNode":
                    return host.TryEvaluateCondition(activeGuide, node, out result, out reason);
                default:
                    result = false;
                    reason = "No local condition evaluator is defined for official node " + node.type + ".";
                    return false;
            }
        }

        private bool TryReadComparable(HomeTutorialNode node, string inputName, TutorialScalar fallback,
            out double number, out string text, out string reason)
        {
            if (TryGetInputSource(node.id, inputName, out HomeTutorialNode source, out string outputName))
            {
                if (source.type == "QueryStoryCountNode")
                {
                    number = StoryCount(Payload(source).StoryName);
                    text = null;
                    reason = null;
                    return true;
                }
                if (source.type == "CompareNode" || source.type == "NotNode" || source.type == "IsStoryStateNode")
                {
                    if (!TryEvaluateOutput(source, outputName, out bool boolValue, out reason))
                    {
                        number = 0;
                        text = null;
                        return false;
                    }
                    number = boolValue ? 1 : 0;
                    text = null;
                    return true;
                }
                if (TryResolveConditionFromHost(source, out bool hostValue, out reason))
                {
                    number = hostValue ? 1 : 0;
                    text = null;
                    return true;
                }
                number = 0;
                text = null;
                return false;
            }
            if (fallback == null)
            {
                number = 0;
                text = null;
                reason = "CompareNode input is missing: " + inputName;
                return false;
            }
            number = fallback.mValueType switch
            {
                1 => fallback.mLong,
                2 => fallback.mFloat,
                3 => fallback.mDouble,
                4 => fallback.mBool,
                _ => fallback.mInt
            };
            text = fallback.mValueType == 5 ? fallback.mString : null;
            reason = null;
            return true;
        }

        private bool TryResolveConditionFromHost(HomeTutorialNode node, out bool value, out string reason)
        {
            switch (node.type)
            {
                case "QueryPlayerConditionNode":
                case "PlayerConditionNode":
                case "PlayerOnSceneConditionNode":
                case "CanRepaireFurnitureConditionNode":
                case "AllNoTipWinClosedConditionNode":
                case "TabItemActiveConditionNode":
                case "WaitHangUpUnlockMetConditionNode":
                    return host.TryEvaluateCondition(activeGuide, node, out value, out reason);
                default:
                    value = false;
                    reason = "Comparison input is not a supported scalar node: " + node.type;
                    return false;
            }
        }

        private void ApplyStoryState(HomeTutorialNode node)
        {
            HomeTutorialNodePayload payload = Payload(node);
            string receipt = ReceiptFor(activeGuide, node);
            if ((save.AppliedReceipts ?? (save.AppliedReceipts = new List<string>())).Contains(receipt)) return;
            HomeTutorialStoryState story = GetStory(payload.Name);
            if (story.State != 2 && payload.State == 2) story.Count++;
            story.State = payload.State;
            save.AppliedReceipts.Add(receipt);
            Persist();
        }

        private void Advance(string portName)
        {
            if (activeGuide == null) return;
            HomeTutorialConnection connection = catalog.FirstConnection(NodeById(save.ActiveNodeId), portName);
            save.Pending = false;
            if (connection == null)
            {
                Block("Official tutorial node has no " + portName + " continuation: " + save.ActiveNodeId);
                return;
            }
            save.ActiveNodeId = connection.node;
            resumePending = false;
            Persist();
            StateChanged?.Invoke();
        }

        private void CompleteGuide()
        {
            HomeTutorialGuide finished = activeGuide;
            HomeTutorialGuideProgress progress = GetGuide(finished.name);
            progress.Completed = true;
            string completionReceipt = "home-tutorial:completed:" + finished.name;
            if (!save.AppliedReceipts.Contains(completionReceipt))
            {
                HomeTutorialStoryState story = GetStory(finished.name);
                if (story.State != 2) story.Count++;
                story.State = 2;
                save.AppliedReceipts.Add(completionReceipt);
            }
            save.ActiveGuide = null;
            save.ActiveNodeId = -1;
            save.Pending = false;
            save.WaitRemaining = 0f;
            ClearOptionSelection();
            activeGuide = null;
            nodesById = null;
            incoming = null;
            IsBlocked = false;
            LastError = null;
            Persist();
            host.EndGuide(finished, true);
            StateChanged?.Invoke();
        }

        private void Block(string reason)
        {
            IsBlocked = true;
            LastError = reason;
            Persist();
            StateChanged?.Invoke();
        }

        private bool HasConnection(HomeTutorialNode node, string portName) =>
            catalog.FirstConnection(node, portName) != null;

        private bool MatchesSavedOptionSelection(HomeTutorialNode node)
        {
            return node != null && node.type == "OptionDialogNode" && node.id == save.OptionSelectionNodeId
                && save.OptionSelectionIndex >= 0
                && save.OptionSelectionReceipt == ReceiptFor(activeGuide, node)
                && (Payload(node).Options ?? Array.Empty<int>()).Length > save.OptionSelectionIndex
                && HasConnection(node, OptionPortName(save.OptionSelectionIndex));
        }

        private void ClearOptionSelection()
        {
            save.OptionSelectionNodeId = -1;
            save.OptionSelectionIndex = -1;
            save.OptionSelectionReceipt = null;
        }

        private static string OptionPortName(int index) => "Options " + index.ToString(CultureInfo.InvariantCulture);

        private HomeTutorialNode NodeById(int id) =>
            nodesById != null && nodesById.TryGetValue(id, out HomeTutorialNode node) ? node : null;

        private bool TryGetInputSource(int targetId, string inputName, out HomeTutorialNode source,
            out string sourcePort)
        {
            if (incoming != null && incoming.TryGetValue(InputKey(targetId, inputName), out List<HomeTutorialConnectionSource> values)
                && values.Count > 0)
            {
                source = values[0].Node;
                sourcePort = values[0].Port;
                return true;
            }
            source = null;
            sourcePort = null;
            return false;
        }

        private void BuildIndexes(HomeTutorialGuide guide)
        {
            nodesById = guide.nodes.ToDictionary(value => value.id);
            incoming = new Dictionary<string, List<HomeTutorialConnectionSource>>(StringComparer.Ordinal);
            foreach (HomeTutorialNode node in guide.nodes)
                foreach (HomeTutorialPort port in node.ports ?? Array.Empty<HomeTutorialPort>())
                    foreach (HomeTutorialConnection connection in port.connections ?? Array.Empty<HomeTutorialConnection>())
                    {
                        string key = InputKey(connection.node, connection.port);
                        if (!incoming.TryGetValue(key, out List<HomeTutorialConnectionSource> sources))
                        {
                            sources = new List<HomeTutorialConnectionSource>();
                            incoming.Add(key, sources);
                        }
                        sources.Add(new HomeTutorialConnectionSource(node, port.name));
                    }
        }

        private static string InputKey(int nodeId, string portName) => nodeId + ":" + portName;

        private static string ReceiptFor(HomeTutorialGuide guide, HomeTutorialNode node) =>
            "home-tutorial:" + guide.name + ":" + node.id;

        private HomeTutorialNodePayload Payload(HomeTutorialNode node)
        {
            if (string.IsNullOrEmpty(node.payload)) return new HomeTutorialNodePayload();
            HomeTutorialNodePayload payload = JsonUtility.FromJson<HomeTutorialNodePayload>(node.payload);
            return payload ?? new HomeTutorialNodePayload();
        }

        private int StoryState(string name) => GetStory(name).State;
        private int StoryCount(string name) => GetStory(name).Count;

        private HomeTutorialStoryState GetStory(string name)
        {
            if (string.IsNullOrEmpty(name)) return new HomeTutorialStoryState();
            save.Stories ??= new List<HomeTutorialStoryState>();
            HomeTutorialStoryState story = save.Stories.FirstOrDefault(value => value.Name == name);
            if (story != null) return story;
            story = new HomeTutorialStoryState { Name = name };
            save.Stories.Add(story);
            return story;
        }

        private HomeTutorialGuideProgress GetGuide(string name)
        {
            save.Guides ??= new List<HomeTutorialGuideProgress>();
            HomeTutorialGuideProgress progress = save.Guides.FirstOrDefault(value => value.Name == name);
            if (progress != null) return progress;
            progress = new HomeTutorialGuideProgress { Name = name };
            save.Guides.Add(progress);
            return progress;
        }

        private static HomeTutorialRunnerSave Load(string path, string version, string sourceSha256)
        {
            if (!File.Exists(path)) return new HomeTutorialRunnerSave
            {
                Version = version,
                SourceSha256 = sourceSha256
            };
            HomeTutorialRunnerSave loaded = JsonUtility.FromJson<HomeTutorialRunnerSave>(File.ReadAllText(path));
            if (loaded == null || (loaded.Schema != 1 && loaded.Schema != 2) || loaded.Version != version
                || loaded.SourceSha256 != sourceSha256)
                throw new FormatException("Home tutorial save does not match the fixed official graph version.");
            if (loaded.Schema == 1)
            {
                loaded.Schema = 2;
                loaded.OptionSelectionNodeId = -1;
                loaded.OptionSelectionIndex = -1;
                loaded.OptionSelectionReceipt = null;
            }
            loaded.Stories ??= new List<HomeTutorialStoryState>();
            loaded.Guides ??= new List<HomeTutorialGuideProgress>();
            loaded.AppliedReceipts ??= new List<string>();
            return loaded;
        }

        private void Persist()
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = savePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(savePath))
            {
                string backup = savePath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(temporary, savePath, backup);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else File.Move(temporary, savePath);
        }

        private sealed class HomeTutorialConnectionSource
        {
            public readonly HomeTutorialNode Node;
            public readonly string Port;

            public HomeTutorialConnectionSource(HomeTutorialNode node, string port)
            {
                Node = node;
                Port = port;
            }
        }
    }
}
