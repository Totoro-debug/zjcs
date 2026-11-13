using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.CharacterCreation;
using SoundObject = Zjcs.Official.ProfessionUI.AkGameObj;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleWwiseSoundAcceptance
    {
        private static IEnumerator ControllerSteps()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Use the non-activating windowed Editor launcher for formal battle screenshots.");
            var resize = (IEnumerator)Type.GetType("Zjcs.Editor.Combat.BattleGameViewInput, Zjcs.Editor.Combat", true)
                .GetMethod("ResizePortrait").Invoke(null, new object[] { 675f });
            while (resize.MoveNext()) yield return resize.Current;
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string settingsPath = Path.Combine(output, "wwise-controller-settings.json");
            File.WriteAllText(settingsPath, JsonUtility.ToJson(new HomeSettingsData {
                allSound = .5f, music = .25f, voice = .75f, sound = .125f }));
            byte[] saved = File.ReadAllBytes(settingsPath);
            var settings = new HomeSettingsProfile(settingsPath);
            settings.Load();
            Require(settings.AllSound == .5f && settings.Music == .25f && settings.Voice == .75f && settings.Sound == .125f,
                "isolated saved volume values are loaded");
            var root = new GameObject("Formal battle sound verification");
            var world = new GameObject("Battle world");
            world.transform.SetParent(root.transform, false);
            var camera = new GameObject("Battle camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.tag = "MainCamera";
            var canvas = new GameObject("Battle canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(root.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var events = root.AddComponent<EventSystem>();
            var controller = world.AddComponent<OfficialBattleRuntimeController>();
            Texture2D head = null;
            Sprite portrait = null;
            try
            {
                var photography = CharacterPortraitPhotography.LoadBattleHead();
                var model = CharacterCreationNativeModelFactory.CreateArenaGuardModel(0, 1, null, photography.excludes);
                try
                {
                    model.transform.position = new Vector3(-1024, -1024, -1024);
                    Require(OfficialBattleCharacterAnimationCatalog.TryResolve(model.GetComponent<Animator>().runtimeAnimatorController,
                        photography.animation, out string pose), "original photograph pose resolves");
                    head = CharacterNativePortraitCapture.Capture(model, photography, pose);
                    Require(head.width == 100 && head.height == 120 && head.GetPixels32().Count(p => p.a > 128) > 1000,
                        "original head photograph contains actual character geometry");
                    portrait = Sprite.Create(head, new Rect(0, 0, 100, 120), new Vector2(.5f, .5f), 100);
                }
                finally { UnityEngine.Object.Destroy(model); }
                for (int battle = 0; battle < 3; battle++)
                {
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    var rules = assets.Load();
                    var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 43,
                        Grid = new FightBattleGrid { Width = 8, Height = 8 }, ResultRules = new FightBattleResultRules() };
                    var actor = SoundUnit(1, 1, 2, 2, 100);
                    var target = SoundUnit(2, 2, 4, 2, 1);
                    actor.Properties.Add(new FightPropertyValue { Id = rules.PropertyType("MoveDist"), Value = 4 });
                    int item = rules.MonsterActiveSkillItems(3206, 0).Single();
                    int entity = assets.LoadSkillItems().Required(item).EntityId;
                    var props = rules.CalculateSkillProps(entity, 1, 1, 1)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
                    var skill = rules.CreateSkill(entity, 0, item, 1, 1, 1, props);
                    actor.Skills.Add(skill);
                    actor.SkillState.Slots.Add(rules.CreateSkillSlot(skill,
                        rules.CalculateSkillCooldown(entity, 1, 1, 1, actor.Properties.ToDictionary(p => p.Id, p => p.Value))));
                    state.Units.Add(actor); state.Units.Add(target);
                    controller.Begin(new OfficialBattleRequest { Checkpoint = state, WorldRoot = world.transform,
                        HudParent = canvas.transform, CombatAssets = assets, InitialStepDelay = 0,
                        FightThemeId = battle == 2 ? 1 : 0,
                        TimelinePortrait = unit => unit.Id == 1 ? portrait : null,
                        EnvironmentGameId = 1000, HostPresentation = new OfficialBattleHostPresentation(camera, world.transform, null),
                        Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false,
                            ShowMainPanelDelay = battle == 2 ? .05f : 0 } });
                    const string themeSlotPath = "FightMainPanel/Content/ThemeNpcSlot";
                    if (battle == 2)
                        Require(canvas.transform.Find(themeSlotPath) == null, "the themed HUD respects the original delayed mount");
                    for (int i = 0; i < 120; i++)
                    {
                        controller.Tick(Time.deltaTime);
                        yield return null;
                        if (i >= 29 && canvas.transform.Find(themeSlotPath) != null) break;
                    }
                    Transform themeSlot = canvas.transform.Find(themeSlotPath);
                    Require(themeSlot != null && themeSlot.childCount == (battle == 2 ? 1 : 0),
                        "formal HUD mounts the selected original theme NPC exactly once");
                    if (battle == 2)
                    {
                        var text = themeSlot.GetComponentInChildren<Zjcs.Official.WorldUI.WorldMultiLanguageText>();
                        Require(text != null && text.Text == "加油！", "formal theme NPC uses the original Chinese text");
                    }
                    Require(AkAudioListener.DefaultListeners.ListenerList.Count == 1, "formal Begin mounts original listener");
                    var musicProvider = UnityEngine.Object.FindObjectOfType<Zjcs.Official.BattleBgm.BgmGameObject>();
                    Require(musicProvider != null, "formal Begin mounts original shared BGM object");
                    var musicConfig = Resources.Load<Zjcs.Official.BattleBgm.GameBgmConfig>(
                        "Combat/Wwise/Bgm/Configs/GameBgmConfig");
                    NativeMusicSwitch(musicProvider.gameObject, "BattleMusic",
                        battle == 2 ? "Activity_LuoTianYi_1" : "Map_11_1_Map");
                    NativeMusicPlaying(musicProvider.gameObject, musicConfig.BgmEventConfig.PlayEvent.Id);
                    Rtpc(3240620755u, battle == 0 ? 50 : 62);
                    Rtpc(3659619896u, battle == 0 ? 25 : 37);
                    Rtpc(3676256460u, battle == 0 ? 75 : 87);
                    Rtpc(3673881719u, battle == 0 ? 12 : 6);
                    Require(File.ReadAllBytes(settingsPath).SequenceEqual(saved), "loading and test muting preserve saved volume bytes");
                    controller.SetHudVisible(false); yield return null;
                    Require(!themeSlot.gameObject.activeInHierarchy, "formal visibility hides the original theme slot");
                    Require(AkAudioListener.DefaultListeners.ListenerList.Count == 0, "background battle removes default listener");
                    controller.SetHudVisible(true); yield return null;
                    Require(themeSlot.gameObject.activeInHierarchy && themeSlot.childCount == (battle == 2 ? 1 : 0),
                        "formal visibility restores the same original theme NPC");
                    Require(AkAudioListener.DefaultListeners.ListenerList.Count == 1, "returning battle restores default listener");
                    NativeMusicPlaying(musicProvider.gameObject, musicConfig.BgmEventConfig.PlayEvent.Id);
                    var start = canvas.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/'))?.GetComponent<Button>();
                    Require(start != null && start.IsInteractable(), "formal original start button is available");
                    var click = SoundClick(events, start);
                    while (click.MoveNext()) yield return click.Current;
                    Require(controller.Phase == FightBattlePhase.Play, "simulated pointer starts formal battle");
                    bool nativePlaying = false, hit = false;
                    controller.StepCompleted += result => { if (result.Event.DamageEvents.Count > 0) hit = true; };
                    for (int frame = 0; frame < 360 && !(nativePlaying && hit && !controller.IsPresentingStep); frame++)
                    {
                        controller.Tick(Time.deltaTime);
                        foreach (var emitter in world.GetComponentsInChildren<SoundObject>(true))
                        {
                            uint count = 16;
                            if (AkSoundEngine.GetPlayingIDsFromGameObject(AkSoundEngine.GetAkGameObjectID(emitter.gameObject),
                                ref count, new uint[count]) == AKRESULT.AK_Success && count > 0) nativePlaying = true;
                        }
                        yield return null;
                    }
                    Require(hit && nativePlaying, "actual skill graph dispatches a native playing event and settles damage: hit="
                        + hit + ", nativePlaying=" + nativePlaying + ", phase=" + controller.Phase);
                    controller.SetPaused(true); yield return null; yield return null;
                    Rtpc(414249091u, 0);
                    controller.SetPaused(false); yield return null; yield return null;
                    Rtpc(414249091u, (float)controller.SpeedMultiplier);
                    if (battle == 0)
                    {
                        Require(settings.TrySetAllSound(.625f)
                            && settings.TrySetVolume(HomeAudioChannel.Music, .375f)
                            && settings.TrySetVolume(HomeAudioChannel.Sound, .0625f)
                            && settings.TrySetVolume(HomeAudioChannel.Voice, .875f), "settings save updates native controls");
                        saved = File.ReadAllBytes(settingsPath);
                    }
                    yield return new WaitForEndOfFrame();
                    var capturedFrame = ScreenCapture.CaptureScreenshotAsTexture();
                    Require(capturedFrame != null && capturedFrame.width == Screen.width && capturedFrame.height == Screen.height,
                        "capture the actual full battle render surface");
                    try { File.WriteAllBytes(Path.Combine(output, "theme-formal-battle-" + battle + ".png"), capturedFrame.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(capturedFrame); }
                    controller.DisposeSession(); yield return null;
                    Require(canvas.transform.Find(themeSlotPath) == null, "formal disposal releases the original theme slot");
                    Require(AkSoundEngine.IsInitialized() && AkAudioListener.DefaultListeners.ListenerList.Count == 0,
                        "formal disposal preserves application engine and removes battle listener");
                    Require(UnityEngine.Object.FindObjectOfType<Zjcs.Official.BattleBgm.GameBgm>() == null,
                        "formal disposal releases original scene BGM switch");
                    settings.Load();
                }
                Debug.Log("NATIVE_WWISE_FORMAL_CONTROLLER_PASSED three real graph battles, original start-button pointer, native playing IDs, damage, saved four-volume controls, pause and listener visibility/disposal; original theme NPC and music share one selection through delayed HUD mount.");
            }
            finally
            {
                controller.DisposeSession(); UnityEngine.Object.Destroy(root);
                if (portrait != null) UnityEngine.Object.Destroy(portrait);
                if (head != null) UnityEngine.Object.Destroy(head);
                Time.timeScale = 1;
            }
        }
        private static FightBattleUnitState SoundUnit(int id, int side, int x, int y, int speed)
        {
            const long hp = 1000000;
            var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 2, MonsterId = 3206,
                MonsterTemplateId = 3206, RoleEntityId = 3206, Hp = hp, MaxHp = hp, LastMaxHp = hp,
                Level = 1, SubRank = 1, X = x, Y = y, Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = speed } };
            unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 100 });
            unit.Properties.Add(new FightPropertyValue { Id = 2, Value = 100 });
            unit.Properties.Add(new FightPropertyValue { Id = 3, Value = hp });
            unit.Properties.Add(new FightPropertyValue { Id = 4, Value = speed });
            return unit;
        }
        private static IEnumerator SoundClick(EventSystem events, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "original battle start button is unobstructed");
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
