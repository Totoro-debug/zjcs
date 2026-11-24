using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeTipDots;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeRelations;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeBedRuntimeValidation
{
    private const string Active = "Zjcs.HomeBedRuntimeValidation.Active";
    private static int errors;
    private static HomeFeatureRuntime home;

    static HomeBedRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
            {
                AudioListener.volume = 0f;
                Application.runInBackground = true;
                Application.logMessageReceived += OnLog;
                var runner = new GameObject("HomeBedValidationRunner");
                Object.DontDestroyOnLoad(runner);
                runner.AddComponent<Runner>();
            }
        };

        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false)
                && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            {
                Finish(new TimeoutException("Bed runtime E2E validation timed out."));
            }
        };
    }

    public static void Import()
    {
        ValidateOwnedProject();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("HOME_BED_IMPORT_PASSED");
    }

    public static void Run()
    {
        ValidateOwnedProject();
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        ValidationGameView.Portrait(675, 1463);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 480f);
        EditorApplication.EnterPlaymode();
    }

    private static void ValidateOwnedProject()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_BED_PROJECT");
        if (string.IsNullOrEmpty(project)
            || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
        {
            throw new InvalidOperationException("Select the independent validation project explicitly.");
        }
    }

    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            Debug.LogWarning("[CapturedError] " + text + "\n" + stack);
        }
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        try { home?.Close(); } catch { }
        home = null;
        Application.logMessageReceived -= OnLog;
        if (error == null)
        {
            Debug.Log("HOME_BED_RUNTIME_PASSED: RepairNotice Lv1, CanRepair Lv3, HomeBed TipDots [2]/[6], native repair + effect + cold persistence, TipLevels -1/0/1, ExpForLevelUpPercentTip, QuickReceive ON/OFF + persistence, free/item fast-forward, all 7 skins, and HomeMutex verified.");
        }
        else
        {
            Debug.LogException(error);
        }
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private sealed class NativePointer : IHomeSceneClickPointer
    {
        public bool LeftDown { get; private set; }
        public bool LeftUp { get; private set; }
        public Vector2 Position { get; private set; }
        public int Width => Screen.width;
        public int Height => Screen.height;

        public void ClickWorldPoint(HomeFeatureRuntime runtime, Vector3 worldPoint)
        {
            Camera cam = runtime.Scene.Camera;
            Position = cam.WorldToScreenPoint(worldPoint);
            Physics.SyncTransforms();
            object nativeInput = Field<object>(runtime, "nativeSceneInput");
            MethodInfo tick = nativeInput.GetType().GetMethod("Tick");
            MethodInfo late = nativeInput.GetType().GetMethod("LateTick");
            LeftDown = true;
            tick.Invoke(nativeInput, null);
            LeftDown = false;
            late.Invoke(nativeInput, null);
            LeftUp = true;
            tick.Invoke(nativeInput, null);
            LeftUp = false;
            late.Invoke(nativeInput, null);
        }
    }

    private static string Work => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
    private static int checksCount;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("CHECK FAILED: " + message);
        checksCount++;
        Debug.Log("CHECK(" + checksCount + "): " + message);
    }

    private static T Field<T>(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo info = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return (T)info.GetValue(target);
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static T Prop<T>(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            PropertyInfo info = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return (T)info.GetValue(target);
            type = type.BaseType;
        }
        throw new MissingMemberException(target.GetType().FullName, name);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        Type type = target.GetType();
        while (type != null)
        {
            MethodInfo info = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) return info.Invoke(target, args);
            type = type.BaseType;
        }
        throw new MissingMethodException(target.GetType().FullName, name);
    }

    private static HomeFeatureRuntime OpenHome(string slotId, string root, NativePointer pointer)
    {
        HomeFeatureRuntime instance = HomeFeatureRuntime.BeginWithNativePointer(slotId, root, null, pointer);
        instance.TutorialAutoStartEnabled = false;
        return instance;
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    private static void FocusBed(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.Bed);
        Require(obj != null, "Bed native instance must exist");
        Vector3 pos = runtime.Scene.Position;
        pos.x = obj.transform.position.x;
        pos.y = obj.transform.position.y;
        runtime.Scene.Camera.transform.position = runtime.Scene.Clamp(pos);
        Physics.SyncTransforms();
    }

    private static BedFurniture BedController(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.Bed);
        return obj ? obj.GetComponentInChildren<BedFurniture>(true) : null;
    }

    private static TipDotNodeLinker FindTipLinker(GameObject root, string graphName, string nodeName)
        => root.GetComponentsInChildren<TipDotNodeLinker>(true)
            .Single(value => value.ConfiguredNodeKey.GraphName == graphName && value.ConfiguredNodeKey.NodeName == nodeName);

    private static Collider FindTouchCollider(ClickableHomeObj owner)
        => Field<List<GameObject>>(owner, "mTouchList")
            .Select(go => go ? go.GetComponent<Collider>() : null)
            .First(c => c != null && c.enabled && c.gameObject.activeInHierarchy);

    private static void ClickUiButton(Button button)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "Target UI button must be clickable: " + (button ? button.name : "null"));
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)button.transform;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Simulated pointer must hit button " + button.name + " at " + screenPoint + ", instead hit: " + string.Join(",", hits.Select(h => h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void ClickUiToggle(Toggle toggle)
    {
        Require(toggle != null && toggle.gameObject.activeInHierarchy && toggle.interactable,
            "Target Toggle must be clickable: " + (toggle ? toggle.name : "null"));
        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)toggle.transform;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == toggle.gameObject || hits[0].gameObject.transform.IsChildOf(toggle.transform)),
            "Simulated pointer must hit toggle " + toggle.name + " at " + screenPoint + ", instead hit: " + string.Join(",", hits.Select(h => h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator Capture(string filename)
    {
        yield return new WaitForEndOfFrame();
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        Require(tex != null && tex.height > tex.width && tex.width >= 500 && tex.height >= 900,
            "Rendered Game View screenshot must be portrait: " + (tex ? (tex.width + "x" + tex.height) : "null"));
        File.WriteAllBytes(Path.Combine(Work, filename), tex.EncodeToPNG());
        int magenta = tex.GetPixels32().Count(p => p.r > 230 && p.b > 230 && p.g < 25);
        Debug.Log("FRAME " + filename + ": " + tex.width + "x" + tex.height + " magenta=" + magenta);
        Object.Destroy(tex);
        Require(magenta <= 20, "Rendered screenshot must have zero shader magenta errors (" + filename + ", magenta=" + magenta + ")");
    }

    private static IEnumerator WaitForDialogReady<T>(Action<T> onReady) where T : MonoBehaviour
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        T dialog = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            dialog = Object.FindObjectOfType<T>();
            if (dialog != null && dialog.gameObject.activeInHierarchy)
            {
                var wp = dialog.GetComponent<WorldPopupWindow>();
                if (wp == null || wp.State == WorldPopupWindow.EState.Show) break;
            }
            yield return null;
        }
        yield return WaitFrames(2);
        onReady?.Invoke(dialog);
    }

    private static void SeedBedIncome(HomeFeatureRuntime runtime, long settledCount)
    {
        Require(runtime.Session.Mutate("bed-test:seed-income:" + settledCount + ":" + Guid.NewGuid().ToString("N"), save =>
        {
            OfflineHomeIncomeEntry entry = save.Income.FirstOrDefault(v => v.HangUpId == 1);
            if (entry == null)
            {
                entry = new OfflineHomeIncomeEntry { HangUpId = 1 };
                save.Income.Add(entry);
            }
            entry.StoredSeconds = 0;
            entry.SettledCount = settledCount;
        }), "Seed Bed settled income to " + settledCount);

        object storage = Field<object>(runtime, "storage");
        Invoke(storage, "NotifyHangUpDataChanged", 1);
        runtime.HangUpHud?.Source.NotifyDataChanged(1);
        runtime.Hud.RefreshAll();
    }

    private static IEnumerator CloseModalDialogsAndAwards(HomeFeatureRuntime runtime)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var confirm = Object.FindObjectOfType<WorldConfirmDialog>();
            if (confirm != null && confirm.gameObject.activeInHierarchy)
            {
                ClickUiButton(confirm.CancelBtn ? confirm.CancelBtn : confirm.ConfirmBtn);
                yield return WaitFrames(4);
                continue;
            }
            if (runtime.Hud != null && runtime.Hud.AnyModalOpen)
            {
                runtime.Hud.CloseDialog();
                yield return WaitFrames(6);
                continue;
            }
            var awardPanel = Object.FindObjectOfType<AwardListPanel>();
            if (awardPanel != null && awardPanel.gameObject.activeInHierarchy)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                continue;
            }
            break;
        }
        yield return WaitFrames(3);
    }

    private static IEnumerator WaitForFastProgressAndConfirmations(float maxSeconds)
    {
        float deadline = Time.realtimeSinceStartup + maxSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            var confirm = Object.FindObjectOfType<WorldConfirmDialog>();
            if (confirm != null && confirm.gameObject.activeInHierarchy && confirm.ConfirmBtn != null && confirm.ConfirmBtn.interactable)
            {
                ClickUiButton(confirm.ConfirmBtn);
                yield return WaitFrames(3);
                continue;
            }
            var bedDialog = Object.FindObjectOfType<FurnitureBedDialog>();
            var fastDialog = Object.FindObjectOfType<FurnitureFastHangUpDialog>();
            var progress = Object.FindObjectOfType<Zjcs.Official.HomeFastHangUpProgressUI.FastHangUpProgressBarDialog>();
            var award = Object.FindObjectOfType<AwardListPanel>();
            if ((bedDialog == null || !bedDialog.gameObject.activeInHierarchy)
                && (fastDialog == null || !fastDialog.gameObject.activeInHierarchy)
                && (progress == null || !progress.gameObject.activeInHierarchy)
                && (award == null || !award.gameObject.activeInHierarchy))
            {
                break;
            }
            yield return null;
        }
        yield return WaitFrames(4);
    }

    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var steps = new Stack<IEnumerator>();
            steps.Push(RunChecks());
            Exception failure = null;
            while (steps.Count > 0)
            {
                object current;
                try
                {
                    if (!steps.Peek().MoveNext())
                    {
                        steps.Pop();
                        continue;
                    }
                    current = steps.Peek().Current;
                    if (current is IEnumerator nested)
                    {
                        steps.Push(nested);
                        continue;
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    break;
                }
                yield return current;
            }

            if (failure == null && errors > 0)
            {
                failure = new InvalidOperationException("Unexpected Unity errors observed: " + errors);
            }
            Finish(failure);
        }
    }

    private static IEnumerator RunChecks()
    {
        checksCount = 0;
        string saveRoot = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot);
        draft.name = "床铺测试";
        Require(creation.TryCreateCharacter(slot, draft, out string createError), "Create character: " + createError);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out createError),
            "Set initial sword: " + createError);

        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot), slot);
        var expCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 1;
            p.SubRank = 1;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[1];
        }, "bed-test:level1").Status != ProgressionResultStatus.InvalidState, "Set Level 1");

        var pointer = new NativePointer();

        // Phase 1: Level 1 -> RepairNotice + blocked click + TipDot hidden
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusBed(home);
        yield return WaitFrames(3);

        Require(home.Scene.NativeBedActors.Count == 0, "Legacy NativeHomeBed placeholder is retired at runtime");
        GameObject bedObj = home.NativeFurnitureInstance(EFurniture.Bed);
        BedFurniture bed = BedController(home);
        HomeNotice notice = bedObj ? bedObj.GetComponentInChildren<HomeNotice>(true) : null;
        Require(bedObj != null && bedObj.transform.parent.name == "BedSlot", "Native Bed mounts in BedSlot");
        Require(bed != null && bed.HangUpServices != null, "BedFurniture binds IHomeHangUpFurnitureServices");
        Require(bed.GetState() == FurnitureBase.EFurnitureState.RepairNotice, "Bed enters RepairNotice at Level 1 (actual=" + bed.GetState() + ")");
        Require(bed.TipLevel == -1, "Bed TipLevel is -1 when unrepaired");
        SpriteRenderer awardIcon = Field<SpriteRenderer>(bed, "AwardIcon");
        var bedWindowAssets = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
        Require(awardIcon != null && awardIcon.sprite == bedWindowAssets.bedRewardImage && awardIcon.sprite != null,
            "Bed AwardIcon binds official experience sprite (" + (awardIcon && awardIcon.sprite ? awardIcon.sprite.name : "null") + ")");
        Require(notice != null && notice.ShowNotice, "HomeNotice shows RepairNotice banner at Level 1");
        TMP_Text noticeText = notice.GetComponentInChildren<TMP_Text>(true);
        Require(noticeText != null && noticeText.text.Contains("3") && noticeText.text.Contains("解锁"),
            "RepairNotice banner shows Level 3 unlock requirement: " + (noticeText ? noticeText.text : "null"));

        TipDotNodeLinker unlockLinker = FindTipLinker(bedObj, "HomeBed", "2");
        TipDotNodeLinker tipLinker = FindTipLinker(bedObj, "HomeBed", "6");
        Require(!unlockLinker.IsShowTip && !tipLinker.IsShowTip, "HomeBed TipDots [2] and [6] are hidden at Level 1");
        yield return Capture("01-bed-repair-notice.png");

        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        yield return WaitFrames(4);
        Require(Prop<FurnitureRepairDialog>(home.Hud, "ActiveRepairDialog") == null && Object.FindObjectOfType<FurnitureBedDialog>() == null,
            "Clicking Bed in RepairNotice opens neither Repair nor Bed dialog");

        home.Close();
        home = null;
        yield return WaitFrames(3);

        // Phase 2: Level 3 -> CanRepair + HomeBedCanUnlock [2] + Repair Dialog + Repair Effect + Persistence
        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 3;
            p.SubRank = 1;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[3];
        }, "bed-test:level3").IsApplied, "Advance character to Level 3");

        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusBed(home);
        yield return WaitFrames(3);

        bedObj = home.NativeFurnitureInstance(EFurniture.Bed);
        bed = BedController(home);
        unlockLinker = FindTipLinker(bedObj, "HomeBed", "2");
        tipLinker = FindTipLinker(bedObj, "HomeBed", "6");
        var tipManager = Field<ITipDotNodeManager>(Field<OfflinePartnerTipDotRuntime>(home, "partnerTipDots"), "manager");
        var bedAssets = OfflineHomeTipDotAssets.LoadDefault();
        var authoredNodes = bedAssets.LoadGraph("HomeBed").nodes.Cast<TipDotGraphNode>().ToArray();
        TipDotNodeBase GraphNode(int id) => tipManager.FindNode(authoredNodes.Single(n => n.TipDotNodeId == id).GetNodeKey());

        Require(bed.GetState() == FurnitureBase.EFurnitureState.CanRepair, "Bed transitions to CanRepair at Level 3");
        Require(GraphNode(2).IsShowTip && unlockLinker.IsShowTip && !GraphNode(6).IsShowTip && !tipLinker.IsShowTip,
            "HomeBedCanUnlock [2] lights up while UnlockedBedHasTip [6] stays off before repair");
        yield return Capture("02-bed-can-repair.png");

        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        yield return WaitFrames(5);
        FurnitureRepairDialog repairDialog = Prop<FurnitureRepairDialog>(home.Hud, "ActiveRepairDialog") ?? Object.FindObjectOfType<FurnitureRepairDialog>();
        float repairPopupDeadline = Time.realtimeSinceStartup + 5f;
        while (repairDialog != null && Time.realtimeSinceStartup < repairPopupDeadline)
        {
            var wp = repairDialog.GetComponent<WorldPopupWindow>();
            if (wp == null || wp.State == WorldPopupWindow.EState.Show) break;
            yield return null;
        }
        Require(repairDialog != null && repairDialog.gameObject.activeInHierarchy && repairDialog.Furniture == EFurniture.Bed,
            "Clicking CanRepair Bed opens official FurnitureRepairDialog immediately (no repair story)");
        yield return Capture("03-bed-repair-dialog.png");

        ClickUiButton(repairDialog.RepairBtn);
        yield return WaitFrames(3);
        GameObject repairEffect = Field<GameObject>(bed, "mRepairEffect");
        Require(repairEffect != null && repairEffect.activeInHierarchy, "Native mRepairEffect (Eff_Ui_Home_Open_Bed) plays upon Bed repair");
        yield return Capture("03b-bed-repair-effect.png");

        yield return new WaitForSecondsRealtime(1.3f);
        yield return WaitFrames(4);
        Require(!repairEffect.activeInHierarchy, "Native repair effect finishes after mRepairEffectFullTime");
        Require(home.Session.OwnsFurniture(0) && home.Session.Definitions.Any(d => d.HangUpId == 1 && d.Unlocked),
            "Bed repair commits ownership (Bed) and unlocks HangUpId 1");
        Require(bed.GetState() == FurnitureBase.EFurnitureState.Functional,
            "Repaired Bed enters Functional state");
        Require(!GraphNode(2).IsShowTip && !unlockLinker.IsShowTip,
            "HomeBedCanUnlock [2] turns off after repair");

        var notifyDialog = Object.FindObjectOfType<FurnitureRepairedNotifyDialog>();
        if (notifyDialog != null)
        {
            var closeNotify = notifyDialog.GetComponentsInChildren<CloseWindowBtn>(true)
                .FirstOrDefault(b => b.GetComponent<Button>() != null && b.gameObject.activeInHierarchy);
            if (closeNotify != null) ClickUiButton(closeNotify.GetComponent<Button>());
            else home.Hud.CloseDialog();
            yield return WaitFrames(6);
        }
        yield return CloseModalDialogsAndAwards(home);
        yield return Capture("04-bed-repaired.png");

        home.Close();
        home = null;
        yield return WaitFrames(3);

        // Cold save reload check
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusBed(home);
        yield return WaitFrames(3);
        bedObj = home.NativeFurnitureInstance(EFurniture.Bed);
        bed = BedController(home);
        Require(bed.GetState() == FurnitureBase.EFurnitureState.Functional, "Repaired Functional state persists across cold Home reload");

        // Phase 3: TipLevels (-1, 0, 1), ExpForLevelUpPercentTip, QuickReceive ON/OFF, FastForward, HomeBed [6] TipDot
        long capacity = home.Session.Preview(1).Capacity;
        Require(capacity > 0, "Bed unlocked capacity must be positive: " + capacity);
        var tipConfig = Zjcs.Offline.HomeHangUpUI.OfflineHomeHangUpTipConfiguration.LoadDefault();
        var expTables = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        long levelUpExp = expTables.LevelExpParser.GetLevelUpExp(1, home.Profile.Save.Player.Level);

        // TipLevel == -1 (count = 1 < 0.005 * capacity && < 0.1 * levelUpExp)
        SeedBedIncome(home, 1);
        yield return WaitFrames(3);
        Require(bed.TipLevel == -1 && !bed.CanReceiveAward(), "Count=1 gives TipLevel -1 and opens Bed dialog on click");
        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        FurnitureBedDialog bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy && bedDialog.HangUpRewardBtnImg.sprite == bedWindowAssets.bedRewardImage,
            "Clicking Bed at TipLevel -1 opens FurnitureBedDialog with official sprites");
        yield return Capture("05-bed-dialog-open.png");
        yield return CloseModalDialogsAndAwards(home);

        // ExpForLevelUpPercentTip check (0.1 * levelUpExp <= 0.005 * capacity at Lv 3 if capacity large enough)
        long expThreshold = Math.Max(1L, (long)Math.Ceiling(levelUpExp * tipConfig.expForLevelUpPercentTip));
        long threshold0 = Math.Max(1L, (long)Math.Ceiling(capacity * 0.005));
        long threshold1 = Math.Max(1L, (long)Math.Ceiling(capacity * 0.05));
        if (expThreshold < threshold0)
        {
            SeedBedIncome(home, expThreshold);
            yield return WaitFrames(3);
            Require(bed.TipLevel == 0, "ExpForLevelUpPercentTip (" + expThreshold + ") raises TipLevel to 0 before capacity 0.5% (" + threshold0 + ")");
        }

        // TipLevel == 0 (between 0.5% and 5% capacity)
        long countLevel0 = Math.Max(threshold0 + 1, Math.Min(threshold1 - 1, (threshold0 + threshold1) / 2));
        SeedBedIncome(home, countLevel0);
        yield return WaitFrames(3);
        Require(bed.TipLevel == 0 && home.HangUpHud.TipLevel(EFurniture.Bed) == 0 && bed.CanReceiveAward(),
            "Count=" + countLevel0 + "/" + capacity + " activates TipLevel 0 on both BedFurniture and HomeHangUpHudRuntime");
        yield return Capture("06-bed-tip-level-0.png");

        // TipLevel == 1 (>= 5% capacity)
        long countLevel1 = Math.Max(threshold1 + 1, (long)(capacity * 0.1));
        SeedBedIncome(home, countLevel1);
        yield return WaitFrames(3);
        Require(bed.TipLevel == 1 && home.HangUpHud.TipLevel(EFurniture.Bed) == 1 && bed.CanReceiveAward(),
            "Count=" + countLevel1 + "/" + capacity + " activates TipLevel 1 on both BedFurniture and HomeHangUpHudRuntime");
        yield return Capture("07-bed-tip-level-1.png");

        // QuickReceive ON direct collect
        long expBeforeQuick = home.Profile.Save.Player.Experience;
        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        yield return WaitFrames(4);
        Require(Object.FindObjectOfType<FurnitureBedDialog>() == null,
            "Clicking Bed with QuickReceive ON & TipLevel>=0 collects directly without opening FurnitureBedDialog");
        Require(home.Profile.Save.Player.Experience > expBeforeQuick,
            "QuickReceive ON click immediately grants Bed experience (" + expBeforeQuick + " -> " + home.Profile.Save.Player.Experience + ")");
        AwardListPanel awardPopup = null;
        yield return WaitForDialogReady<AwardListPanel>(dlg => awardPopup = dlg);
        Require(awardPopup != null && awardPopup.gameObject.activeInHierarchy,
            "QuickReceive ON click displays OfflineHomeBedAwards queue popup");
        Require(bed.TipLevel == -1 && home.HangUpHud.TipLevel(EFurniture.Bed) == -1,
            "Bed TipLevel resets to -1 immediately after quick-receive collect");
        yield return Capture("08-bed-quick-receive-award.png");
        yield return new WaitForSecondsRealtime(2.5f);
        yield return CloseModalDialogsAndAwards(home);

        // Re-query tipManager reference after earlier reload
        tipManager = Field<ITipDotNodeManager>(Field<OfflinePartnerTipDotRuntime>(home, "partnerTipDots"), "manager");
        tipLinker = FindTipLinker(bedObj, "HomeBed", "6");

        // UnlockedBedHasTip [6]: FreeFast RedDot ON initially -> use free fast-forward -> RedDot clears
        int fastId = home.Rules.HangUp(1).fastHangUpId;
        HomeFastHangUpRecord fastRecord = home.Rules.FastHangUp(fastId);
        int fastItemId = fastRecord.items.First(item => item != null && item.itemId > 0).itemId;
        Require(home.HomeTipServices.CanFreeFastHangUp(fastId) && GraphNode(6).IsShowTip && tipLinker.IsShowTip,
            "UnlockedBedHasTip [6] is active with FreeFast RedDot while daily free sleep remains");

        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy, "Open Bed dialog to invoke free fast hang-up");
        long expBeforeFreeFast = home.Profile.Save.Player.Experience;
        ClickUiButton(bedDialog.FastHangUpBtn);
        yield return WaitFrames(3);
        var confirmDlg = Object.FindObjectOfType<WorldConfirmDialog>();
        if (confirmDlg != null && confirmDlg.gameObject.activeInHierarchy)
        {
            ClickUiButton(confirmDlg.ConfirmBtn);
            yield return WaitFrames(3);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (Object.FindObjectOfType<Zjcs.Official.HomeFastHangUpProgressUI.FastHangUpProgressBarDialog>() != null)
        {
            yield return Capture("09-bed-sleep-progress.png");
        }
        yield return WaitForFastProgressAndConfirmations(15f);
        yield return CloseModalDialogsAndAwards(home);
        Require(home.Profile.Save.Player.Experience > expBeforeFreeFast && !home.HomeTipServices.CanFreeFastHangUp(fastId),
            "Free fast hang-up awards experience and consumes daily free charge");
        Require(!GraphNode(6).IsShowTip && !tipLinker.IsShowTip,
            "UnlockedBedHasTip [6] hides after free sleep is used with 0 fast items");

        // ItemFast BlueDot check: seed 12 official fast-hangup items
        Require(home.Profile.ApplyCharacterMutation(p =>
        {
            OfflineItemStack stack = p.Inventory.FirstOrDefault(s => s.ItemId == fastItemId);
            if (stack == null) p.Inventory.Add(new OfflineItemStack { ItemId = fastItemId, Count = 12 });
            else stack.Count += 12;
        }, "bed-test:grant-fast-items").IsApplied, "Grant 12 official Time Crystal fast-hangup items (itemId=" + fastItemId + ")");
        home.Profile.Reload();
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(5);

        Require(GraphNode(6).IsShowTip && GraphNode(6).TipDotInfo.Data == "12" && tipLinker.IsShowTip,
            "UnlockedBedHasTip [6] shows BlueDot count='12' on GraphNode & native TipDotNodeLinker");
        yield return Capture("10-bed-item-fast-tipdot.png");

        // Open BedDialog & trigger Item FastHangUp dialog -> execute item fast-forward
        SeedBedIncome(home, 0);
        yield return WaitFrames(3);
        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy, "Open Bed dialog for item fast hang-up");
        ClickUiButton(bedDialog.FastHangUpBtn);
        yield return WaitFrames(3);
        confirmDlg = Object.FindObjectOfType<WorldConfirmDialog>();
        if (confirmDlg != null && confirmDlg.gameObject.activeInHierarchy)
        {
            ClickUiButton(confirmDlg.ConfirmBtn);
            yield return WaitFrames(3);
        }
        FurnitureFastHangUpDialog fastDialog = null;
        yield return WaitForDialogReady<FurnitureFastHangUpDialog>(dlg => fastDialog = dlg);
        Require(fastDialog != null && fastDialog.gameObject.activeInHierarchy, "Non-free FastHangUpBtn opens FurnitureFastHangUpDialog");
        yield return Capture("11-bed-fast-selection-dialog.png");
        long itemsBeforeFast = home.Profile.InventoryCount(fastItemId);
        ClickUiButton(fastDialog.FastBtn);
        yield return WaitForFastProgressAndConfirmations(15f);
        yield return CloseModalDialogsAndAwards(home);
        home.Profile.Reload();
        long itemsAfterFast = home.Profile.InventoryCount(fastItemId);
        Require(itemsAfterFast < itemsBeforeFast,
            "Item fast hang-up consumes Time Crystal item (" + itemsBeforeFast + " -> " + itemsAfterFast + ")");
        Require(home.Session.Preview(1).Count > 0
            && (itemsAfterFast > 0 ? (GraphNode(6).IsShowTip && GraphNode(6).TipDotInfo.Data == itemsAfterFast.ToString()) : !GraphNode(6).IsShowTip),
            "Item fast hang-up stores output in Bed and updates UnlockedBedHasTip count=" + itemsAfterFast);

        // QuickReceive gate (Function 164 unlocks at Level >= 30): hidden while < 30, visible & toggleable at Level 30 (SubRank 4)
        Require(home.Rules.IsFunctionLimited(164), "QuickReceive Function 164 is gated below Level 30");
        SeedBedIncome(home, 0);
        yield return WaitFrames(3);
        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy, "Open Bed dialog below Level 30 to confirm QuickReceiveToggle gated");
        Toggle gatedToggle = bedDialog.GetComponentsInChildren<Toggle>(true).Single();
        Require(!gatedToggle.gameObject.activeInHierarchy, "QuickReceiveToggle is hidden while Function 164 is locked (< Level 30)");
        yield return CloseModalDialogsAndAwards(home);

        home.Profile.Reload();
        Require(home.Profile.ApplyCharacterMutation(p =>
        {
            p.Level = 30;
            p.SubRank = 4;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[30];
        }, "bed-test:level30").IsApplied, "Advance character to Level 30 SubRank 4 for QuickReceive unlock");
        home.Profile.Reload();
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(4);
        Require(!home.Rules.IsFunctionLimited(164), "QuickReceive Function 164 unlocks at Level 30");

        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy, "Open Bed dialog at Level 30 to toggle QuickReceive OFF");
        Toggle quickToggle = bedDialog.GetComponentsInChildren<Toggle>(true).Single();
        Require(quickToggle.gameObject.activeInHierarchy && quickToggle.interactable && quickToggle.isOn,
            "QuickReceiveToggle is visible, interactable, and ON by default once Level 30 unlocks Function 164");
        ClickUiToggle(quickToggle);
        yield return WaitFrames(3);
        Require(!home.GetHangUpQuickReceiveState(EFurniture.Bed) && !quickToggle.isOn,
            "QuickReceive toggle persists OFF state to OfflineHomeIncomeSession");
        yield return CloseModalDialogsAndAwards(home);

        SeedBedIncome(home, countLevel1);
        yield return WaitFrames(3);
        Require(bed.TipLevel == 1 && !bed.CanReceiveAward(),
            "Even at TipLevel 1, CanReceiveAward() is false when QuickReceive is OFF");
        pointer.ClickWorldPoint(home, FindTouchCollider(bed).bounds.center);
        bedDialog = null;
        yield return WaitForDialogReady<FurnitureBedDialog>(dlg => bedDialog = dlg);
        Require(bedDialog != null && bedDialog.gameObject.activeInHierarchy,
            "Clicking Bed with QuickReceive OFF & TipLevel 1 opens FurnitureBedDialog instead of quick collect");
        yield return Capture("12-bed-quick-off-dialog.png");
        yield return CloseModalDialogsAndAwards(home);

        // Verify QuickReceive OFF persists across cold close & reopen
        home.Close();
        home = null;
        yield return WaitFrames(3);

        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusBed(home);
        yield return WaitFrames(3);
        bed = BedController(home);
        Require(!home.GetHangUpQuickReceiveState(EFurniture.Bed) && bed.TipLevel == 1 && !bed.CanReceiveAward(),
            "QuickReceive OFF preference and Bed TipLevel persist across cold Home reload");
        Require(home.SetHangUpQuickReceiveState(EFurniture.Bed, true), "Restore QuickReceive ON for skin showcase");

        // Phase 4: All 7 Bed skins + HomeMutex verification
        var skins = new (int id, string prefabName, bool hasMutex, string frameName)[]
        {
            (4215, "BedChristmas", false, "13-skin-4215-christmas.png"),
            (4222, "BedChineseStyle", true, "14-skin-4222-chinesestyle.png"),
            (4234, "Bed1stAnniversary", true, "15-skin-4234-anniversary.png"),
            (4244, "BedChineseOdyssey", false, "16-skin-4244-odyssey.png"),
            (4254, "BedNon_Human", false, "17-skin-4254-nonhuman.png"),
            (4257, "BedHalloween", false, "18-skin-4257-halloween.png"),
            (4201, "Bed", false, "19-skin-4201-default.png"),
        };

        foreach (var (skinId, prefabName, hasMutex, frameName) in skins)
        {
            Require(home.HomeProgression.EquipSkin(home.Session, 0, (int)EFurniture.Bed, skinId, _ => 1, "bed-test:skin:" + skinId),
                "Equip Bed skin " + skinId);
            home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
            home.RefreshNativeFurnitureSkins();
            yield return WaitFrames(4);
            GameObject live = home.NativeFurnitureInstance(EFurniture.Bed);
            Require(live != null && live.activeInHierarchy && live.name == prefabName + "(Clone)"
                && live.transform.parent.name == "BedSlot",
                "Switched Bed skin " + skinId + " mounts " + prefabName + "(Clone)");
            Require(live.GetComponentsInChildren<Component>(true).All(c => c != null),
                "Bed skin " + prefabName + " has zero missing scripts");
            Require(live.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null)),
                "Bed skin " + prefabName + " binds valid materials");
            BedFurniture skinBed = live.GetComponentInChildren<BedFurniture>(true);
            Require(skinBed != null && skinBed.HangUpServices != null && skinBed.TipLevel == 1,
                "Bed skin " + prefabName + " binds HangUpServices and preserves TipLevel");
            if (hasMutex)
            {
                HomeMutex[] mutexes = live.GetComponentsInChildren<HomeMutex>(true);
                Require(mutexes.Length > 0 && mutexes.All(m => m.RelationManager != null),
                    "Bed skin " + prefabName + " injects HomeRelationManager into HomeMutex");
            }
            yield return Capture(frameName);
        }

        home.Close();
        home = null;
        yield return WaitFrames(3);
    }
}
