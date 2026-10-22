using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Plays the official opening story graphs. Node semantics follow the client's
    /// RunNode/ClientRunNode contract: run the node, then the next node through the
    /// Exit port, with SafeEnd switching the remainder of the graph into skip mode.
    /// </summary>
    public sealed class OpeningStoryPlayer
    {
        private readonly OpeningAssets assets;
        private readonly OpeningUiView ui;
        private readonly Action<string> log;
        private readonly Action<bool> onHideAllUi;
        private readonly Action onLeaveFight;
        private readonly Func<bool> isGm;
        private readonly AudioSource audio;

        private GameObject dialogPanel;
        private GameObject blackMask;
        private GameObject invisibleMask;
        private readonly List<GameObject> windows = new List<GameObject>();
        private bool uiHidden;
        private bool skipMode;
        private bool confirmClicked;

        public OpeningStoryPlayer(OpeningAssets assets, OpeningUiView ui, AudioSource audio,
            Action<string> log, Action<bool> onHideAllUi, Action onLeaveFight, Func<bool> isGm)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.audio = audio;
            this.log = log ?? (_ => { });
            this.onHideAllUi = onHideAllUi;
            this.onLeaveFight = onLeaveFight;
            this.isGm = isGm ?? (() => false);
        }

        public bool UiHidden => uiHidden;

        /// <summary>Story-level skip entry (StoryGraph.DoSkip); gated like the official UI.</summary>
        public bool TryRequestSkip(OpeningStoryGraph graph)
        {
            if (graph == null || (!graph.ShowSkipButton && !isGm())) return false;
            skipMode = true;
            return true;
        }

        public IEnumerator Play(string storyName)
        {
            OpeningStoryGraph graph = assets.Story.RequireGraph(storyName);
            skipMode = false;
            OpeningStoryNode node = graph.Start;
            while (node != null)
            {
                var result = StoryNodeResult.Ok;
                yield return RunNode(graph, node, value => result = value);
                if (result == StoryNodeResult.SafeEnd) skipMode = true;
                if (result == StoryNodeResult.ForceEnd) break;
                node = node.Exit.Count > 0 && graph.Nodes.TryGetValue(node.Exit[0], out OpeningStoryNode next)
                    ? next : null;
            }

            // StoryPlayEnvManager.Reset() runs when a graph ends: drop masks, windows and
            // restore the main UI.
            CloseDialogs();
            HideBlackMask();
            HideInvisibleMask();
            CloseAllWindows();
            SetUiHidden(false);
        }

        public void Abort()
        {
            CloseDialogs();
            HideBlackMask();
            HideInvisibleMask();
            CloseAllWindows();
            SetUiHidden(false);
        }

        private IEnumerator RunNode(OpeningStoryGraph graph, OpeningStoryNode node,
            Action<StoryNodeResult> done)
        {
            StoryNodeResult result = StoryNodeResult.Ok;
            if (skipMode && !KeepsRunningWhenSkipped(node.Type))
            {
                done(result);
                yield break;
            }
            switch (node.Type)
            {
                case "StartStoryNode":
                    break;
                case "WaitForSecondsNode":
                    if (node.NeedMask) ShowInvisibleMask();
                    if (!skipMode && node.Seconds > 0f) yield return new WaitForSecondsRealtime(node.Seconds);
                    break;
                case "DialogNode":
                    HideInvisibleMask();
                    yield return PlayDialog(graph.Name, node);
                    if (node.AutoClose) CloseDialogs();
                    break;
                case "ShowBlackMaskNode":
                    HideInvisibleMask();
                    ShowBlackMask();
                    break;
                case "CloseBlackMaskNode":
                    HideBlackMask();
                    break;
                case "HideAllUINode":
                    SetUiHidden(true);
                    break;
                case "ShowAllUINode":
                    SetUiHidden(false);
                    break;
                case "OpenShowPanelNode":
                    ShowInvisibleMask();
                    yield return ShowWindow(PanelKey(node.WindowPath));
                    break;
                case "CloseWindowNode":
                    CloseAllWindows();
                    yield return null;
                    break;
                case "LeaveStoryFightNode":
                    onLeaveFight?.Invoke();
                    break;
                case "PlayBlackScreenStoryNode":
                    ShowInvisibleMask();
                    yield return ShowWindow("first_story");
                    HideInvisibleMask();
                    if (node.KeepBlackScreen) ShowBlackMask();
                    break;
                case "PlayPlayableNode":
                    foreach (string guid in node.Playables)
                        log("开场剧情 playable 不在固定版五包资源清单中，按官方空句柄跳过: " + guid);
                    break;
                case "PlayEnterGameTransitionNode":
                    HideInvisibleMask();
                    ShowBlackMask();
                    yield return ShowWindow("enter_game_transition");
                    HideBlackMask();
                    break;
                case "HomePlayerPlayActionNode":
                    // Offline has no home environment manager; the official node throws and
                    // the graph switches to skip mode through RunNode.GetCodeForException.
                    log("开场剧情家园动作缺少家园环境，按官方 SafeEnd 转入快进: " + node.Behaviour);
                    result = StoryNodeResult.SafeEnd;
                    break;
                case "FinishStoryNode":
                    break;
                default:
                    log("开场剧情节点类型未实现: " + node.Type + " (" + node.Id + ")");
                    break;
            }
            done(result);
        }

        private static bool KeepsRunningWhenSkipped(string type)
        {
            // ClientRunNode.InternalSkip returns immediately for nodes that only show
            // things; nodes that hide/close/leave still execute during skip.
            switch (type)
            {
                case "StartStoryNode":
                case "FinishStoryNode":
                case "HideAllUINode":
                case "ShowAllUINode":
                case "CloseWindowNode":
                case "CloseBlackMaskNode":
                case "LeaveStoryFightNode":
                case "PlayPlayableNode":
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerator PlayDialog(string story, OpeningStoryNode node)
        {
            var lines = new List<OpeningDialogLine>(assets.Story.FindDialogues(story, node.DialogId));
            lines.Sort((left, right) => left.Order.CompareTo(right.Order));
            if (lines.Count == 0)
            {
                log("开场剧情对话数据缺失: " + story + " " + node.DialogId);
                yield break;
            }

            if (dialogPanel == null) dialogPanel = ui.Create("talk_dialog");
            dialogPanel.SetActive(true);
            Button next = FindButton(dialogPanel, "NextBtn");
            foreach (OpeningDialogLine line in lines)
            {
                ApplyLine(line);
                PlayVoice(line);
                if (skipMode) continue;
                confirmClicked = false;
                if (next != null)
                {
                    next.onClick.RemoveAllListeners();
                    next.onClick.AddListener(() => confirmClicked = true);
                }
                float elapsed = 0f;
                float autoDelay = node.AutoPlay ? Mathf.Max(0.5f, node.AutoDura) : float.PositiveInfinity;
                while (!confirmClicked && elapsed < autoDelay)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (audio != null) audio.Stop();
            }
        }

        private void ApplyLine(OpeningDialogLine line)
        {
            assets.Story.Roles.TryGetValue(line.RoleId, out OpeningRole role);
            if (!ui.SetFirstTextEnding("NpcNameText", role == null ? string.Empty : role.Name))
                log("开场剧情说话人节点缺失");
            if (!ui.SetFirstTextEnding("TalkText", line.Text))
                log("开场剧情正文节点缺失");
        }

        private void PlayVoice(OpeningDialogLine line)
        {
            // The fixed-version local payload ships no Wwise bank for these events.
            if (string.IsNullOrEmpty(line.Voice) || line.DialogType != "Voice") return;
            log("开场剧情语音尚未接入本地资源: " + line.Voice);
        }

        private static Button FindButton(GameObject root, string suffix)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name.EndsWith(suffix, StringComparison.Ordinal)) return button;
            return null;
        }

        private static string PanelKey(string windowPath)
        {
            if (string.IsNullOrEmpty(windowPath)) return string.Empty;
            if (windowPath.EndsWith("ExitFightTransitionPanel", StringComparison.Ordinal)) return "exit_fight_transition";
            if (windowPath.EndsWith("EnterGameStoryTransitionPanel", StringComparison.Ordinal)) return "enter_game_transition";
            if (windowPath.EndsWith("FirstStoryPanel", StringComparison.Ordinal)) return "first_story";
            return string.Empty;
        }

        private IEnumerator ShowWindow(string panelKey)
        {
            if (string.IsNullOrEmpty(panelKey) || !assets.Ui.Prefabs.ContainsKey(panelKey))
            {
                log("官方剧情窗口未随载荷交付，跳过展示: " + panelKey);
                yield return null;
                yield break;
            }
            GameObject created = ui.Create(panelKey);
            windows.Add(created);
            created.SetActive(true);
            // The official prefab plays its show animation; the offline copy carries the
            // original layout but no clips, so the wait is one frame plus the popup guard.
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private void CloseAllWindows()
        {
            foreach (GameObject window in windows) if (window != null) UnityEngine.Object.Destroy(window);
            windows.Clear();
        }

        private void CloseDialogs()
        {
            if (dialogPanel == null) return;
            UnityEngine.Object.Destroy(dialogPanel);
            dialogPanel = null;
        }

        private void ShowInvisibleMask()
        {
            if (invisibleMask == null) invisibleMask = CreateOverlay("OpeningInvisibleMask", new Color(0, 0, 0, 0));
            invisibleMask.SetActive(true);
        }

        private void HideInvisibleMask()
        {
            if (invisibleMask != null) invisibleMask.SetActive(false);
        }

        private void ShowBlackMask()
        {
            if (blackMask == null) blackMask = CreateOverlay("OpeningBlackMask", Color.black);
            blackMask.SetActive(true);
        }

        private void HideBlackMask()
        {
            if (blackMask != null) blackMask.SetActive(false);
        }

        private GameObject CreateOverlay(string name, Color color)
        {
            var overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(ui.Root, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = overlay.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            overlay.transform.SetAsLastSibling();
            return overlay;
        }

        private void SetUiHidden(bool hidden)
        {
            if (uiHidden == hidden) return;
            uiHidden = hidden;
            onHideAllUi?.Invoke(hidden);
        }

        public void Dispose()
        {
            Abort();
            if (blackMask != null) UnityEngine.Object.Destroy(blackMask);
            if (invisibleMask != null) UnityEngine.Object.Destroy(invisibleMask);
            blackMask = null;
            invisibleMask = null;
        }
    }

    public enum StoryNodeResult
    {
        Ok,
        SafeEnd,
        ForceEnd,
    }
}
