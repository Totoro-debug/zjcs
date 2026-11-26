using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Common;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeUI;
using Zjcs.Official.StoryMasks;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeStory;
using Zjcs.Offline.HomeStoryDialog;
using Zjcs.Offline.HomeUI;
using Zjcs.Presentation.World;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private FurnitureStories furnitureStories;

        public bool IsFurnitureStoryRunning => furnitureStories != null && furnitureStories.IsRunning;

        internal bool TryPlayFurnitureStory(string storyName, Action onCompleted = null)
            => furnitureStories != null && furnitureStories.TryPlay(storyName, onCompleted);

        /// <summary>
        /// Reusable runner for official Home furniture unlock stories
        /// (<c>furniture-unlock-stories.json</c>), coordinating camera control,
        /// HUD visibility, <see cref="HomeStoryDialogWindow"/>, and
        /// <see cref="NativeStoryMaskHost"/> across buildings.
        /// </summary>
        private sealed partial class FurnitureStories : HomeFurnitureStoryGraph.IHost,
            IHomeStoryDialogEnvironment, IDisposable
        {
            private const int DefaultPlayerRoleId = 100001;

            private readonly HomeFeatureRuntime home;
            private readonly HomeStoryDialogAssets furnitureDialogAssets;
            private HomeStoryDialogAssets dialogAssets;
            private readonly HomeStoryPortraitAssets portraitAssets;
            private readonly HomeStoryOptionAssets optionAssets;
            private readonly HomeStorySettings settings;
            private HomeFurnitureStoryGraph graph;

            private HomeStoryDialogWindow dialogWindow;
            private HomeFurnitureStoryDialogNode dialogNodeRunner;
            private NativeStoryMaskHost maskHost;
            private AudioSource voiceSource;
            private AudioSource soundSource;
            private Coroutine activeCoroutine;
            private int routineGeneration;
            private string activeStory;
            private Action activeCompleted;
            private bool hidMainUi;
            private bool disposed;

            internal bool IsRunning => graph != null && graph.IsRunning;

            internal FurnitureStories(HomeFeatureRuntime home)
            {
                this.home = home ?? throw new ArgumentNullException(nameof(home));
                furnitureDialogAssets = dialogAssets = HomeStoryDialogAssets.LoadDefault();
                portraitAssets = HomeStoryPortraitAssets.LoadDefault();
                optionAssets = HomeStoryOptionAssets.LoadDefault();
                settings = HomeStorySettings.LoadDefault();
                graph = new HomeFurnitureStoryGraph(dialogAssets.StorySource, this);
            }

            internal bool TryPlay(string storyName, Action onCompleted = null)
            {
                if (disposed || string.IsNullOrEmpty(storyName) || graph.IsRunning) return false;
                HomeStoryDialogAssets selected = IsFruitTreeStory(storyName)
                    ? Resources.Load<HomeStoryDialogAssets>("Home/HomeFruitTreeStoryAssets") : furnitureDialogAssets;
                if (!selected || !selected.StorySource) throw new InvalidOperationException("Original story assets are unavailable: " + storyName);
                if (selected != dialogAssets)
                {
                    dialogNodeRunner?.Dispose();
                    dialogNodeRunner = null;
                    dialogWindow?.Dispose();
                    dialogWindow = null;
                    dialogAssets = selected;
                    graph = new HomeFurnitureStoryGraph(dialogAssets.StorySource, this);
                }
                activeStory = storyName;
                activeCompleted = onCompleted;
                hidMainUi = false;

                int startedCount = home.StoryCount(storyName);
                int currentStatus = home.StoryStatus(storyName);
                if (currentStatus < 1)
                {
                    home.profile?.SetStoryProgress(storyName, startedCount, 1,
                        "home:furniture-story:start:" + home.slotId + ":" + storyName);
                }

                try
                {
                    graph.Play(storyName, () => OnStoryFinished(storyName));
                }
                catch
                {
                    RestoreAfterStory();
                    throw;
                }
                return true;
            }

            internal void Cancel()
            {
                if (activeCoroutine != null && home != null)
                {
                    home.StopCoroutine(activeCoroutine);
                    activeCoroutine = null;
                }
                if (graph != null && graph.IsRunning) graph.Cancel();
                RestoreAfterStory();
            }

            public void Dispose()
            {
                if (disposed) return;
                Cancel();
                disposed = true;
                dialogNodeRunner?.Dispose();
                dialogNodeRunner = null;
                dialogWindow?.Dispose();
                dialogWindow = null;
                DestroyMaskHost();
                HomeAudioSettings.Unregister(voiceSource);
                if (voiceSource) Object.Destroy(voiceSource);
                voiceSource = null;
                HomeAudioSettings.Unregister(soundSource);
                if (soundSource) Object.Destroy(soundSource);
                soundSource = null;
            }

            public void Execute(string story, HomeFurnitureStoryGraph.Node node, Action<int?> completed)
            {
                if (disposed || node == null) return;
                switch (node.Type)
                {
                    case "StartStoryNode":
                        if (((int?)node.Data["HideMainUI"] ?? 0) != 0) SetMainUiHidden(true);
                        completed(null);
                        break;
                    case "WaitStartStoryNode":
                        StartRoutine(WaitUntilRoutine(
                            () => ((int?)node.Data["CanStart"] ?? 1) != 0
                                && ((int?)node.Data["ConditionSatisfied"] ?? 1) != 0,
                            () => completed(null)));
                        break;
                    case "WaitAllNoTipWindowClosedNode":
                    case "FinishStoryConditionNode":
                        StartRoutine(WaitUntilRoutine(
                            () => (home.hud == null || !home.hud.AnyModalOpen) && !home.IsWishScreenOpen,
                            () => completed(null)));
                        break;
                    case "SetFreeCameraForcePosXNode":
                        if (home.scene?.Camera != null)
                        {
                            float x = (float?)node.Data["PositionX"] ?? 0f;
                            Vector3 current = home.scene.Camera.transform.position;
                            home.scene.Camera.transform.position = home.scene.Clamp(new Vector3(x, current.y, current.z));
                        }
                        completed(null);
                        break;
                    case "CameraDragNode":
                        home.SetTutorialCameraDragEnabled(((int?)node.Data["CanDrag"] ?? 1) != 0);
                        completed(null);
                        break;
                    case "CameraMoveNode":
                        StartRoutine(CameraMoveRoutine(node.Data, () => completed(null)));
                        break;
                    case "HideAllUINode":
                        SetMainUiHidden(true);
                        completed(null);
                        break;
                    case "ShowAllUINode":
                        SetMainUiHidden(false);
                        completed(null);
                        break;
                    case "DialogNode":
                    case "OptionDialogNode":
                        bool restoreMainUi = ((int?)node.Data["IsHideOtherUI"] ?? 1) != 0 && !hidMainUi;
                        if (restoreMainUi) SetMainUiHidden(true);
                        EnsureDialogWindow();
                        dialogNodeRunner.Begin(story, node, result =>
                        {
                            if (disposed) return;
                            bool autoClose = ((int?)node.Data["AutoCloseDialog"] ?? 0) == 1;
                            StartRoutine(FinishDialogNodeRoutine(autoClose, result, completed, restoreMainUi));
                        }, () => EnsureMaskHost().Manager.Reset());
                        break;
                    case "RepairFurnitureNode":
                        StartRoutine(RepairFurnitureRoutine(node.Data, () => completed(null)));
                        break;
                    default:
                        if (!TryExecuteWaitNode(node, completed))
                            throw new NotSupportedException("Unsupported official furniture story node: " + node.Type);
                        break;
                }
            }

            private void OnStoryFinished(string storyName)
            {
                if (!disposed)
                {
                    if (IsFruitTreeStory(storyName))
                    {
                        home.fruitTree.MarkRepairStorySeen(storyName == "FruitTreeUnlock1" ? 4 : 11);
                        if (storyName == "FruitTreeUnlock1") home.fruitTree.MarkCollectionTutorialSeen();
                    }
                    int nextCount = home.StoryCount(storyName) + 1;
                    home.profile?.SetStoryProgress(storyName, nextCount, 2,
                        "home:furniture-story:finish:" + home.slotId + ":" + storyName + ":"
                        + nextCount.ToString(CultureInfo.InvariantCulture));
                }
                Action callback = activeCompleted;
                RestoreAfterStory();
                callback?.Invoke();
            }

            private void RestoreAfterStory()
            {
                activeStory = null;
                activeCompleted = null;
                routineGeneration++;
                ClearStoryWaitSubscriptions();
                if (activeCoroutine != null && home != null)
                {
                    home.StopCoroutine(activeCoroutine);
                    activeCoroutine = null;
                }
                if (dialogNodeRunner != null)
                {
                    dialogNodeRunner.Dispose();
                    dialogNodeRunner = null;
                }
                if (dialogWindow != null)
                {
                    if (home.hud != null)
                    {
                        home.hud.SetStoryUiPreserved(dialogWindow.Root, false);
                    }
                    dialogWindow.Dispose();
                    dialogWindow = null;
                }
                DestroyMaskHost();
                SetMainUiHidden(false);
                if (home != null)
                {
                    home.hud?.HideTutorialMask();
                    home.SetTutorialCameraDragEnabled(true);
                }
                StopVoice();
            }

            private void SetMainUiHidden(bool hidden)
            {
                if (home.hud == null) return;
                if (hidden)
                {
                    if (!hidMainUi)
                    {
                        hidMainUi = true;
                        home.hud.SetTutorialMainUiVisible(false);
                    }
                }
                else if (hidMainUi)
                {
                    hidMainUi = false;
                    home.hud.SetTutorialMainUiVisible(true);
                }
            }

            private void StartRoutine(IEnumerator routine)
            {
                int run = ++routineGeneration;
                if (activeCoroutine != null && home != null) home.StopCoroutine(activeCoroutine);
                activeCoroutine = null;
                Coroutine started = home.StartCoroutine(WrapRoutine(run, routine));
                if (run == routineGeneration) activeCoroutine = started;
            }

            private IEnumerator WrapRoutine(int run, IEnumerator inner)
            {
                yield return inner;
                if (run == routineGeneration) activeCoroutine = null;
            }

            private static IEnumerator WaitUntilRoutine(Func<bool> ready, Action completed)
            {
                while (!ready()) yield return null;
                completed?.Invoke();
            }

            private IEnumerator CameraMoveRoutine(JObject data, Action completed)
            {
                if (home.scene?.Camera != null)
                {
                    Vector3 start = home.scene.Camera.transform.position;
                    Vector3 target = new Vector3(
                        (float?)data["TargetPosition"]?["x"] ?? start.x,
                        (float?)data["TargetPosition"]?["y"] ?? start.y,
                        start.z);
                    float duration = Math.Max(0f, (float?)data["Duration"] ?? 0f);
                    AnimationCurve curveX = ReadCurve(data["MoveXCurve"]);
                    AnimationCurve curveY = ReadCurve(data["MoveYCurve"]);
                    if (duration <= 0f || curveX == null || curveY == null)
                    {
                        home.scene.Camera.transform.position = home.scene.Clamp(target);
                        completed?.Invoke();
                        yield break;
                    }
                    EnsureMaskHost().Manager.Reset();
                    EnsureMaskHost().Manager.SetInvisibleMask(true);
                    Vector3 delta = target - start;
                    delta.z = 0f;
                    Quaternion rotation = Mathf.Approximately(delta.sqrMagnitude, 0f)
                        ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, delta);
                    float startedAt = Time.time;
                    while (true)
                    {
                        float percent = Mathf.Clamp01((Time.time - startedAt) / duration);
                        Vector3 next = start + rotation * new Vector3(curveX.Evaluate(percent), curveY.Evaluate(percent), 0f) * delta.magnitude;
                        home.scene.Camera.transform.position = home.scene.Clamp(next);
                        if (percent >= 1f) break;
                        yield return null;
                    }
                }
                completed?.Invoke();
            }

            private IEnumerator FinishDialogNodeRoutine(bool autoClose, int? result, Action<int?> completed, bool restoreMainUi)
            {
                if (dialogWindow != null && dialogWindow.Root && !autoClose)
                    dialogWindow.Close();
                while (dialogWindow != null && dialogWindow.Root) yield return null;
                if (home.hud != null && dialogWindow?.Root != null)
                    home.hud.SetStoryUiPreserved(dialogWindow.Root, false);
                if (restoreMainUi) SetMainUiHidden(false);
                completed(result);
            }

            private IEnumerator RepairFurnitureRoutine(JObject data, Action completed)
            {
                int furniture = (int?)data["Furniture"] ?? -1;
                if (home.HasFurnitureForSkin(furniture))
                {
                    completed?.Invoke();
                    yield break;
                }

                bool forbidDrag = ((int?)data["IsForbidDragCam"] ?? 1) != 0;
                MaskSetting furnitureMask = ReadMaskSetting(data["FurnitureMaskSet"]);
                MaskSetting repairBtnMask = ReadMaskSetting(data["RepaireBtnMaskSet"]);
                NativeStoryMaskHost host = EnsureMaskHost();
                try
                {
                    host.Manager.Reset();
                    host.Manager.SetInvisibleMask(true);
                    while (!disposed && ((home.hud != null && home.hud.AnyModalOpen) || home.IsWishScreenOpen))
                        yield return null;
                    host.Manager.SetInvisibleMask(false);

                    while (!disposed && !home.HasFurnitureForSkin(furniture))
                    {
                        if (forbidDrag) home.SetTutorialCameraDragEnabled(false);

                        if (home.storage != null && home.storage.TryGetFurnitureMaskTarget(furniture, out GameObject target))
                        {
                            host.Manager.OpenRectMask(target, furnitureMask);
                        }
                        else if (home.hud != null)
                        {
                            home.hud.ShowTutorialFurnitureMask(furniture, ToTutorialMask(furnitureMask));
                        }

                        while (!disposed && !home.HasFurnitureForSkin(furniture)
                            && (home.hud == null || home.hud.ActivePanelKey != "repair"))
                        {
                            yield return null;
                        }

                        home.hud?.HideTutorialMask();
                        host.Manager.Reset();
                        if (disposed || home.HasFurnitureForSkin(furniture)) break;

                        FurnitureRepairDialog dialog = home.hud?.ActiveRepairDialog;
                        while (!disposed && !home.HasFurnitureForSkin(furniture)
                            && home.hud != null && home.hud.ActivePanelKey == "repair"
                            && (dialog == null || dialog.RepairBtn == null || !dialog.RepairBtn.gameObject.activeInHierarchy))
                        {
                            yield return null;
                            dialog = home.hud?.ActiveRepairDialog;
                        }

                        if (!disposed && !home.HasFurnitureForSkin(furniture)
                            && home.hud != null && home.hud.ActivePanelKey == "repair"
                            && dialog != null && dialog.RepairBtn != null)
                        {
                            host.Manager.OpenRectMask(dialog.RepairBtn, repairBtnMask);
                            while (!disposed && home.hud != null && home.hud.ActivePanelKey == "repair")
                                yield return null;
                            host.Manager.CloseRectMask();
                        }

                        while (!disposed && ((home.hud != null && home.hud.AnyModalOpen) || home.IsWishScreenOpen))
                            yield return null;
                    }
                }
                finally
                {
                    if (maskHost != null) maskHost.Manager.Reset();
                    home.hud?.HideTutorialMask();
                    if (forbidDrag && home != null) home.SetTutorialCameraDragEnabled(true);
                }

                if (!disposed) completed?.Invoke();
            }

            private void EnsureDialogWindow()
            {
                if (dialogWindow == null)
                {
                    Canvas hudCanvas = home.hud?.Canvas;
                    if (hudCanvas == null) throw new InvalidOperationException("Home canvas is unavailable for story dialog.");
                    dialogWindow = new HomeStoryDialogWindow(hudCanvas, dialogAssets, portraitAssets,
                        optionAssets, settings, home.catalog.Font, this, PostUiSound);
                    dialogNodeRunner = new HomeFurnitureStoryDialogNode(home, dialogWindow);
                }
                else if (dialogNodeRunner == null)
                {
                    dialogNodeRunner = new HomeFurnitureStoryDialogNode(home, dialogWindow);
                }
            }

            private NativeStoryMaskHost EnsureMaskHost()
            {
                if (maskHost == null)
                {
                    if (home.hud?.CanvasTransform == null)
                        throw new InvalidOperationException("Home canvas is unavailable for story mask.");
                    maskHost = NativeStoryMaskHost.Create(home.hud.CanvasTransform);
                    home.hud.SetStoryUiPreserved(maskHost.gameObject, true);
                }
                return maskHost;
            }

            private void DestroyMaskHost()
            {
                if (maskHost == null) return;
                home.hud?.SetStoryUiPreserved(maskHost.gameObject, false);
                maskHost.Manager?.Reset();
                Object.Destroy(maskHost.gameObject);
                maskHost = null;
            }

            private void PostUiSound(AudioClip clip, GameObject emitter)
            {
                if (!clip || home == null) return;
                if (soundSource == null)
                {
                    soundSource = home.gameObject.AddComponent<AudioSource>();
                    soundSource.playOnAwake = false;
                    soundSource.loop = false;
                    soundSource.spatialBlend = 0f;
                    HomeAudioSettings.Register(soundSource, HomeAudioChannel.Sound);
                }
                soundSource.PlayOneShot(clip);
            }

            private void StopVoice()
            {
                if (voiceSource == null) return;
                voiceSource.Stop();
                voiceSource.clip = null;
            }

            string IHomeStoryDialogEnvironment.PlayerName => home.profile?.Save?.Player?.CharacterName ?? string.Empty;
            int IHomeStoryDialogEnvironment.PlayerRoleId => DefaultPlayerRoleId;
            RegionCodeEnum IHomeStoryDialogEnvironment.Region() => RegionCodeEnum.CN;
            DateTime IHomeStoryDialogEnvironment.Now() => DateTime.Now;
            bool IHomeStoryDialogEnvironment.IsConditionMet(int conditionType, string conditionParams) => false;
            bool IHomeStoryDialogEnvironment.ContainsText(string key) => !string.IsNullOrEmpty(home.catalog?.LocalizedText(key, null));
            string IHomeStoryDialogEnvironment.Localize(string key) => home.catalog?.LocalizedText(key, key) ?? key;
            WorldPopupWindow IHomeStoryDialogEnvironment.LastWindowIgnoringTips() => home.hud?.LastModalPopupWindowIgnoringTips();
            bool IHomeStoryDialogEnvironment.ActivePanelHasSceneGraphic() => false;
            void IHomeStoryDialogEnvironment.CloseWindow(GameObject window) => home.hud?.CloseDialog();
            void IHomeStoryDialogEnvironment.PostVoice(string eventKey, AudioClip clip, GameObject emitter)
            {
                if (eventKey == settings.DialogVoiceStopEventKey || !clip)
                {
                    StopVoice();
                    return;
                }
                if (voiceSource == null)
                {
                    voiceSource = home.gameObject.AddComponent<AudioSource>();
                    voiceSource.playOnAwake = false;
                    voiceSource.loop = false;
                    voiceSource.spatialBlend = 0f;
                    HomeAudioSettings.Register(voiceSource, HomeAudioChannel.Voice);
                }
                voiceSource.Stop();
                voiceSource.clip = clip;
                voiceSource.Play();
            }
            MyPlayable IHomeStoryDialogEnvironment.PlayWorldNpcPlayable(int roleId, string path) => null;
            void IHomeStoryDialogEnvironment.ReleasePlayableResources() { }

            private static MaskSetting ReadMaskSetting(JToken token)
            {
                var setting = new MaskSetting();
                if (token == null || token.Type == JTokenType.Null) return setting;
                JToken offset = token["Offset"];
                if (offset != null && offset.Type != JTokenType.Null)
                    setting.Offset = new Vector3((float?)offset["x"] ?? 0f, (float?)offset["y"] ?? 0f, (float?)offset["z"] ?? 0f);
                JToken scale = token["Scale"];
                if (scale != null && scale.Type != JTokenType.Null)
                    setting.Scale = new Vector3((float?)scale["x"] ?? 1f, (float?)scale["y"] ?? 1f, (float?)scale["z"] ?? 1f);
                setting.ShowMask = ((int?)token["ShowMask"] ?? 1) != 0;
                setting.TipUIKeyId = (int?)token["TipUIKeyId"] ?? 0;
                return setting;
            }

            private static TutorialMaskSetting ToTutorialMask(MaskSetting setting)
                => new TutorialMaskSetting
                {
                    Offset = new TutorialVector { x = setting.Offset.x, y = setting.Offset.y },
                    Scale = new TutorialVector { x = setting.Scale.x, y = setting.Scale.y },
                    ShowMask = setting.ShowMask ? 1 : 0
                };

            private static AnimationCurve ReadCurve(JToken token)
            {
                if (!(token?["m_Curve"] is JArray keys) || keys.Count == 0) return null;
                var result = new Keyframe[keys.Count];
                for (int index = 0; index < keys.Count; index++)
                {
                    JToken item = keys[index];
                    result[index] = new Keyframe((float?)item["time"] ?? 0f, (float?)item["value"] ?? 0f,
                        (float?)item["inSlope"] ?? 0f, (float?)item["outSlope"] ?? 0f);
                }
                return new AnimationCurve(result);
            }
        }
    }
}
