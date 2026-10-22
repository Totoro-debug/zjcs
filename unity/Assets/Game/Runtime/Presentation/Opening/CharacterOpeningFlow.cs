using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Offline counterpart of MainUIRoot/CreatePlayerStoryFightController: decides whether
    /// the opening runs, plays BeforeFight -> official record -> AfterFight, waits for the
    /// fight-to-home transition (5 s cap) and then opens character creation.
    /// </summary>
    public sealed class CharacterOpeningFlow : MonoBehaviour
    {
        /// <summary>Official skip switch: conditions.csv 21003 (channel/media whitelist).</summary>
        public const int SkipNewbieCutsceneConditionId = 21003;

        private static readonly HashSet<string> PlayedSlots = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] SkipChannels = { "130077" };
        private static readonly string[] SkipMedias = { "C520612" };

        /// <summary>Editor/PlayMode acceptance switch: force the record-fight skip button.</summary>
        public static bool ForceSkipButtonForAcceptance;

        /// <summary>Editor/PlayMode acceptance switch: skip the opening story fight.</summary>
        public static bool SkipOpeningForAcceptance;

        /// <summary>Editor/PlayMode acceptance switch: skip the post-creation FirstStory.</summary>
        public static bool SkipFirstStoryForAcceptance;

        private OpeningAssets assets;
        private OpeningUiView ui;
        private OpeningStoryPlayer story;
        private OpeningFightPlayback playback;
        private AudioSource voiceAudio;
        private OpeningBattlefield battlefield;
        private Camera worldCamera;
        private GameObject worldRoot;
        private bool leftFight;
        private bool hideRequested;

        public static bool HasPlayed(string slotId) => PlayedSlots.Contains(slotId);
        public static void ResetProcessState() => PlayedSlots.Clear();

        /// <summary>Signal returned by the offline SDK stub; empty means no whitelist match.</summary>
        public static bool ShouldSkipNewbieCutscene(Func<string> channel, Func<string> media)
        {
            string currentChannel = channel == null ? string.Empty : channel() ?? string.Empty;
            string currentMedia = media == null ? string.Empty : media() ?? string.Empty;
            foreach (string value in SkipChannels)
                if (string.Equals(value, currentChannel, StringComparison.Ordinal)) return true;
            foreach (string value in SkipMedias)
                if (string.Equals(value, currentMedia, StringComparison.Ordinal)) return true;
            return false;
        }

        public void Begin(string slotId, Action openCreation, Action<string> status, Func<string> channel = null,
            Func<string> media = null, Func<bool> forceSkipButton = null)
        {
            if (openCreation == null) throw new ArgumentNullException(nameof(openCreation));
            if (PlayedSlots.Contains(slotId) || SkipOpeningForAcceptance
                || ShouldSkipNewbieCutscene(channel, media))
            {
                PlayedSlots.Add(slotId);
                openCreation();
                return;
            }
            StartCoroutine(Run(slotId, openCreation, status ?? (_ => { }),
                forceSkipButton ?? (() => ForceSkipButtonForAcceptance)));
        }

        public void PlayFirstStory(Action onFinished, Action<string> status = null)
        {
            if (SkipFirstStoryForAcceptance)
            {
                onFinished?.Invoke();
                return;
            }
            StartCoroutine(RunFirstStory(onFinished, status ?? (_ => { })));
        }

        private IEnumerator Run(string slotId, Action openCreation, Action<string> status, Func<bool> forceSkipButton)
        {
            bool failed = false;
            yield return Prepare(status, error => failed = error);
            if (!failed)
            {
                // Official order: the stage scene and its GAME_ENTITY_LIST units exist
                // before the BeforeFight story graph starts.
                playback = new OpeningFightPlayback(assets, ui, TryLoadModels(),
                    battlefield != null ? battlefield.UnitRoot : worldRoot.transform, battlefield,
                    forceSkipButton, Log);
                yield return playback.Prepare();
                yield return story.Play("BeforeFight");
                yield return playback.Play();
                leftFight = false;
                yield return story.Play("AfterFight");
                if (!leftFight)
                {
                    // MainUIRoot.OnPostStoryFinish waits for Fight -> Home for at most five
                    // seconds, logs on timeout and opens the creation panel regardless.
                    float elapsed = 0f;
                    while (!leftFight && elapsed < 5f)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!leftFight) Log("开场战斗结束后未收到战斗到主场景的切换，已按 5 秒超时继续");
                }
                DestroyWorld();
            }
            Cleanup();
            PlayedSlots.Add(slotId);
            openCreation();
        }

        private IEnumerator RunFirstStory(Action onFinished, Action<string> status)
        {
            bool failed = false;
            yield return Prepare(status, error => failed = error);
            if (!failed) yield return story.Play("FirstStory");
            Cleanup();
            onFinished?.Invoke();
        }

        private IEnumerator Prepare(Action<string> status, Action<bool> failed)
        {
            failed(false);
            try
            {
                assets = OpeningAssets.LoadDefault();
                EnsureCamera();
                TMP_FontAsset font = LoadFont();
                ui = new OpeningUiView(assets.Ui, font, null, 60);
                story = new OpeningStoryPlayer(assets, ui, voiceAudio, Log, OnHideAllUi, OnLeaveFight, IsGameMaster);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Log("开场剧情加载失败，按官方回放失败分支直接打开创角: " + error.Message);
                status(error.Message);
                failed(true);
            }
            yield return null;
        }

        private void Cleanup()
        {
            playback = null;
            story?.Dispose();
            story = null;
            if (ui != null) { ui.Dispose(); ui = null; }
            assets = null;
            hideRequested = false;
            DestroyWorld();
        }

        private void Awake()
        {
            voiceAudio = gameObject.AddComponent<AudioSource>();
            voiceAudio.playOnAwake = false;
        }

        private void EnsureCamera()
        {
            if (worldCamera != null) return;
            if (OpeningBattlefield.PayloadExists)
            {
                try
                {
                    battlefield = OpeningBattlefield.Load(transform, Log);
                    worldCamera = battlefield.StageCamera;
                    Log("官方开场战场已装配: 批次=" + battlefield.BatchCount + " 粒子=" + battlefield.ParticleCount
                        + " 格网=" + battlefield.Size.x + "x" + battlefield.Size.y);
                    return;
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                    Log("官方开场战场装配失败，使用空场景继续: " + error.Message);
                    if (battlefield != null) { battlefield.Dispose(); battlefield = null; }
                }
            }
            else
            {
                Log("官方开场战场载荷缺失: " + OpeningBattlefield.PayloadPath);
            }
            var cameraObject = new GameObject("OpeningFightCamera", typeof(Camera));
            worldCamera = cameraObject.GetComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 9f;
            worldCamera.transform.position = new Vector3(12f, 11f, -10f);
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = Color.black;
            worldCamera.depth = 10;
            worldRoot = new GameObject("OpeningFightWorld");
            worldRoot.transform.SetParent(transform, false);
        }

        private void DestroyWorld()
        {
            if (battlefield != null) { battlefield.Dispose(); battlefield = null; }
            if (worldRoot != null) Destroy(worldRoot);
            worldRoot = null;
            if (worldCamera != null) Destroy(worldCamera.gameObject);
            worldCamera = null;
        }

        private TMP_FontAsset LoadFont()
        {
            try
            {
                return OfficialBattleHudCatalog.LoadDefault().Font;
            }
            catch (Exception error)
            {
                Log("官方打字字体不可用: " + error.Message);
                return TMP_Settings.defaultFontAsset;
            }
        }

        private OfficialBattleModelCatalog TryLoadModels()
        {
            try
            {
                return OfficialBattleModelCatalog.LoadDefault();
            }
            catch (Exception error)
            {
                Log("官方战斗模型目录不可用: " + error.Message);
                return null;
            }
        }

        private void OnHideAllUi(bool hidden)
        {
            hideRequested = hidden;
        }

        private void OnLeaveFight()
        {
            leftFight = true;
            DestroyWorld();
        }

        private bool IsGameMaster() => false;

        public bool UiHidden => hideRequested;

        private void Log(string message) => Debug.Log("[Opening] " + message, this);
    }
}
