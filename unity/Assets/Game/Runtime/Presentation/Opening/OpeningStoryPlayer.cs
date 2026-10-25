using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private readonly Func<string, IEnumerator> onHomePlayerAction;
        private readonly AudioSource audio;
        private readonly Dictionary<string, AudioClip> voiceClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        private GameObject dialogPanel;
        private GameObject leftPortrait;
        private GameObject rightPortrait;
        private string leftPortraitPath = string.Empty;
        private string rightPortraitPath = string.Empty;
        private string currentPortraitPath = string.Empty;
        private bool currentPortraitOnLeft;
        private GameObject blackMask;
        private GameObject invisibleMask;
        private readonly List<GameObject> windows = new List<GameObject>();
        private readonly Dictionary<GameObject, string> windowKeys = new Dictionary<GameObject, string>();
        private bool uiHidden;
        private bool skipMode;
        private bool confirmClicked;

        public OpeningStoryPlayer(OpeningAssets assets, OpeningUiView ui, AudioSource audio,
            Action<string> log, Action<bool> onHideAllUi, Action onLeaveFight, Func<bool> isGm,
            Func<string, IEnumerator> onHomePlayerAction = null)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.audio = audio;
            this.log = log ?? (_ => { });
            this.onHideAllUi = onHideAllUi;
            this.onLeaveFight = onLeaveFight;
            this.isGm = isGm ?? (() => false);
            this.onHomePlayerAction = onHomePlayerAction;
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
            yield return CloseAllWindows();
            SetUiHidden(false);
        }

        public void Abort()
        {
            CloseDialogs();
            HideBlackMask();
            HideInvisibleMask();
            CloseAllWindowsImmediately();
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
                    yield return ShowWindow(PanelKey(node.WindowPath), node.Instant);
                    break;
                case "CloseWindowNode":
                    yield return CloseAllWindows(node.Instant);
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
                {
                    IEnumerator homeAction = onHomePlayerAction?.Invoke(node.Behaviour);
                    if (homeAction == null)
                    {
                        log("开场剧情家园动作缺少家园角色，按官方 SafeEnd 转入快进: " + node.Behaviour);
                        result = StoryNodeResult.SafeEnd;
                    }
                    else yield return homeAction;
                    break;
                }
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

            if (dialogPanel == null)
            {
                dialogPanel = ui.Create("talk_dialog");
                // OptionTalkDialogPanel.DoInit -> OptionSlot.Reset hides the prefab's
                // sample options before a normal (non-option) DialogNode is presented.
                Transform options = dialogPanel.transform.Find("OptionSlots/Content");
                if (options != null)
                    foreach (Transform option in options) option.gameObject.SetActive(false);
                dialogPanel.SetActive(true);
                if (assets.Ui.Prefabs.TryGetValue("talk_dialog", out OpeningUiNode dialogPrefab)
                    && dialogPrefab.Window != null)
                    yield return ui.PlayAnimation(dialogPanel, "talk_dialog", dialogPrefab.Window.ShowClip);
            }
            else dialogPanel.SetActive(true);
            Button next = FindButton(dialogPanel, "NextBtn");
            foreach (OpeningDialogLine line in lines)
            {
                yield return ApplyLine(line);
                if (audio != null) audio.Stop();
                PlayVoice(line);
                if (skipMode) continue;
                confirmClicked = false;
                if (next != null)
                {
                    next.onClick.RemoveAllListeners();
                    next.onClick.AddListener(() => confirmClicked = true);
                }
                // Desktop dialogue advances on confirmation. The APK's optional idle
                // autoplay (120-second initial wait, then 2 seconds) is disabled by
                // the confirmed offline interaction requirement.
                while (!confirmClicked && !skipMode)
                    yield return null;
                if (audio != null) audio.Stop();
            }
        }

        private IEnumerator ApplyLine(OpeningDialogLine line)
        {
            assets.Story.Roles.TryGetValue(line.RoleId, out OpeningRole role);
            if (role != null) yield return PresentPortrait(role, line.RoleFace);
            if (!ui.SetTextEnding(dialogPanel, "NpcNameText", role == null ? string.Empty : role.Name))
                log("开场剧情说话人节点缺失");
            if (!ui.SetTextEnding(dialogPanel, "TalkText", line.Text))
                log("开场剧情正文节点缺失");
        }

        private IEnumerator PresentPortrait(OpeningRole role, string face)
        {
            if (string.IsNullOrEmpty(role.PortraitPrefabKey))
            {
                log("官方对白角色立绘未绑定: " + role.Id);
                yield break;
            }

            if (!string.IsNullOrEmpty(currentPortraitPath) && currentPortraitPath == role.PortraitPath)
            {
                SetPortraitFace(currentPortraitOnLeft ? leftPortrait : rightPortrait, role, face);
                yield break;
            }

            if (string.IsNullOrEmpty(currentPortraitPath))
            {
                currentPortraitOnLeft = true;
                leftPortrait = CreatePortrait(role, true, face);
                leftPortraitPath = role.PortraitPath;
                SetPortraitColor(leftPortrait, Color.white);
                currentPortraitPath = role.PortraitPath;
                yield break;
            }

            SetPortraitColor(currentPortraitOnLeft ? leftPortrait : rightPortrait,
                new Color(0.6f, 0.6f, 0.6f, 1f));
            currentPortraitOnLeft = !currentPortraitOnLeft;
            if (currentPortraitOnLeft)
            {
                if (leftPortraitPath != role.PortraitPath)
                {
                    if (leftPortrait != null) UnityEngine.Object.Destroy(leftPortrait);
                    leftPortrait = CreatePortrait(role, true, face);
                    leftPortraitPath = role.PortraitPath;
                }
                else SetPortraitFace(leftPortrait, role, face);
                SetPortraitColor(leftPortrait, Color.white);
            }
            else
            {
                if (rightPortraitPath != role.PortraitPath)
                {
                    if (rightPortrait != null) UnityEngine.Object.Destroy(rightPortrait);
                    rightPortrait = CreatePortrait(role, false, face);
                    rightPortraitPath = role.PortraitPath;
                }
                else SetPortraitFace(rightPortrait, role, face);
                SetPortraitColor(rightPortrait, Color.white);
            }
            currentPortraitPath = role.PortraitPath;
            yield return null;
        }

        private GameObject CreatePortrait(OpeningRole role, bool left, string face)
        {
            Transform parent = FindPortraitParent(left);
            if (parent == null)
            {
                log("官方对白立绘挂点缺失: " + (left ? "Left" : "Right"));
                return null;
            }
            GameObject portrait = ui.Create(role.PortraitPrefabKey, parent);
            portrait.name = "OpeningPortrait_" + role.Id;
            if (!left && !role.NeedFlipOnRight)
            {
                Vector3 scale = portrait.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                portrait.transform.localScale = scale;
            }
            SetPortraitFace(portrait, role, face);
            return portrait;
        }

        private Transform FindPortraitParent(bool left)
        {
            if (dialogPanel == null) return null;
            string[] path = left
                ? new[] { "Content", "CharacterAndTalkContent", "CharacterGroup", "LeftCharacter", "Locator", "PortraitParent" }
                : new[] { "Content", "CharacterAndTalkContent", "CharacterGroup", "RightCharacter", "Mask", "Locator", "PortraitParent" };
            Transform current = dialogPanel.transform;
            foreach (string part in path)
            {
                current = FindDirectChild(current, part);
                if (current == null) return null;
            }
            return current;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            return null;
        }

        private void SetPortraitFace(GameObject portrait, OpeningRole role, string face)
        {
            if (portrait == null) return;
            string spriteName = string.Empty;
            bool enabled = !string.IsNullOrEmpty(face)
                && role.PortraitFaceSprites.TryGetValue(face, out spriteName);
            Vector2 offset = role.PortraitFaceOffsets.TryGetValue(face ?? string.Empty, out Vector2 faceOffset)
                ? faceOffset : role.PortraitFaceOffset;
            if (!ui.SetImage(portrait, "Face", enabled ? spriteName : string.Empty, enabled, offset))
                log("官方对白立绘表情节点缺失: " + role.Id);
        }

        private static void SetPortraitColor(GameObject portrait, Color color)
        {
            if (portrait == null) return;
            foreach (Transform child in portrait.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Main" && child.name != "Face") continue;
                Image image = child.GetComponent<Image>();
                if (image != null) image.color = color;
            }
        }

        private void PlayVoice(OpeningDialogLine line)
        {
            if (audio == null || string.IsNullOrEmpty(line.Voice) || line.DialogType != "Voice") return;
            if (!assets.VoiceFiles.TryGetValue(line.Voice, out string relativePath))
            {
                log("官方开场语音事件没有本地绑定: " + line.Voice);
                return;
            }
            if (!voiceClips.TryGetValue(line.Voice, out AudioClip clip))
            {
                string path = Path.Combine(OpeningAssets.Directory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                try { clip = ReadPcmWave(path, line.Voice); }
                catch (System.Exception error)
                {
                    log("官方开场语音读取失败 " + line.Voice + ": " + error.Message);
                    return;
                }
                voiceClips.Add(line.Voice, clip);
            }
            audio.clip = clip;
            audio.Play();
        }

        private static AudioClip ReadPcmWave(string path, string eventName)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 44 || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F'
                || data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
                throw new InvalidDataException("Opening voice WAV header is invalid: " + eventName);

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int audioFormat = 0;
            int audioOffset = -1;
            int audioLength = 0;
            for (int offset = 12; offset + 8 <= data.Length;)
            {
                int length = BitConverter.ToInt32(data, offset + 4);
                int content = offset + 8;
                if (length < 0 || content + length > data.Length)
                    throw new InvalidDataException("Opening voice WAV chunk is invalid: " + eventName);
                if (data[offset] == 'f' && data[offset + 1] == 'm' && data[offset + 2] == 't' && data[offset + 3] == ' ')
                {
                    if (length < 16) throw new InvalidDataException("Opening voice WAV format is incomplete: " + eventName);
                    audioFormat = BitConverter.ToInt16(data, content);
                    channels = BitConverter.ToInt16(data, content + 2);
                    sampleRate = BitConverter.ToInt32(data, content + 4);
                    bitsPerSample = BitConverter.ToInt16(data, content + 14);
                }
                else if (data[offset] == 'd' && data[offset + 1] == 'a' && data[offset + 2] == 't' && data[offset + 3] == 'a')
                {
                    audioOffset = content;
                    audioLength = length;
                }
                offset = content + length + (length & 1);
            }
            if (audioFormat != 1 || channels <= 0 || sampleRate <= 0 || bitsPerSample != 16
                || audioOffset < 0 || audioLength == 0 || audioLength % (channels * 2) != 0)
                throw new InvalidDataException("Opening voice is not supported PCM16: " + eventName);

            int sampleCount = audioLength / 2;
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToInt16(data, audioOffset + i * 2) / 32768f;
            AudioClip clip = AudioClip.Create(eventName, sampleCount / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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

        private IEnumerator ShowWindow(string panelKey, bool instant = false)
        {
            if (string.IsNullOrEmpty(panelKey) || !assets.Ui.Prefabs.ContainsKey(panelKey))
            {
                log("官方剧情窗口未随载荷交付，跳过展示: " + panelKey);
                yield return null;
                yield break;
            }
            GameObject created = ui.Create(panelKey);
            windows.Add(created);
            windowKeys[created] = panelKey;
            created.SetActive(true);
            if (assets.Ui.Prefabs.TryGetValue(panelKey, out OpeningUiNode prefab) && prefab.Window != null)
                yield return ui.PlayAnimation(created, panelKey, prefab.Window.ShowClip, instant);
        }

        private IEnumerator CloseAllWindows(bool instant = false)
        {
            foreach (GameObject window in windows)
            {
                if (window == null) continue;
                if (windowKeys.TryGetValue(window, out string panelKey)
                    && assets.Ui.Prefabs.TryGetValue(panelKey, out OpeningUiNode prefab)
                    && prefab.Window != null && !instant && !string.IsNullOrEmpty(prefab.Window.HideClip))
                    yield return ui.PlayAnimation(window, panelKey, prefab.Window.HideClip);
                UnityEngine.Object.Destroy(window);
            }
            windows.Clear();
            windowKeys.Clear();
        }

        private void CloseAllWindowsImmediately()
        {
            foreach (GameObject window in windows) if (window != null) UnityEngine.Object.Destroy(window);
            windows.Clear();
            windowKeys.Clear();
        }

        private void CloseDialogs()
        {
            if (audio != null) audio.Stop();
            if (dialogPanel == null) return;
            UnityEngine.Object.Destroy(dialogPanel);
            dialogPanel = null;
            leftPortrait = null;
            rightPortrait = null;
            leftPortraitPath = string.Empty;
            rightPortraitPath = string.Empty;
            currentPortraitPath = string.Empty;
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
            foreach (AudioClip clip in voiceClips.Values)
                if (clip != null) UnityEngine.Object.Destroy(clip);
            voiceClips.Clear();
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
