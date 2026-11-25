using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeSceneInput;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeUI;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryMasks;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.World;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeDiningTableRuntimeValidation
{
    private const string Active = "Zjcs.HomeDiningTableRuntimeValidation.Active";

    private static int errors;
    private static HomeFeatureRuntime home;

    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunChecks());
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                bool hasNext;
                try
                {
                    hasNext = current.MoveNext();
                }
                catch (Exception error)
                {
                    Finish(error);
                    yield break;
                }
                if (!hasNext)
                {
                    stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
                else
                {
                    yield return current.Current;
                }
            }
            Finish(errors == 0 ? null : new Exception("Unexpected Unity errors during DiningTable validation: " + errors));
        }
    }

    static HomeDiningTableRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
            {
                AudioListener.volume = 0f;
                Application.runInBackground = true;
                Application.logMessageReceived += OnLog;
                var runner = new GameObject("HomeDiningTableValidationRunner");
                Object.DontDestroyOnLoad(runner);
                runner.AddComponent<Runner>();
            }
        };

        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false)
                && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            {
                Finish(new TimeoutException("DiningTable runtime E2E validation timed out."));
            }
        };
    }

    public static void Import()
    {
        ValidateOwnedProject();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("HOME_DININGTABLE_IMPORT_PASSED");
    }

    public static void Run()
    {
        ValidateOwnedProject();
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        ValidationGameView.Portrait(675, 1463);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 420f);
        EditorApplication.EnterPlaymode();
    }

    private static void ValidateOwnedProject()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_DINING_PROJECT");
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
            Debug.Log("HOME_DININGTABLE_RUNTIME_PASSED: states, notice, UnlockCommunication story, mask, repair effect, persistence, NPC bubble tip, dialogue acknowledge, TipDots, FunctionLimit 57, and all 6 skins verified.");
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

    private static void FocusDiningTable(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.DiningTable);
        Require(obj != null, "DiningTable native instance must exist");
        Vector3 pos = runtime.Scene.Position;
        pos.x = obj.transform.position.x;
        runtime.Scene.Camera.transform.position = runtime.Scene.Clamp(pos);
        Physics.SyncTransforms();
    }

    private static HomeNpcChatEntry DiningTableEntry(HomeFeatureRuntime runtime)
    {
        GameObject obj = runtime.NativeFurnitureInstance(EFurniture.DiningTable);
        return obj ? obj.GetComponentInChildren<HomeNpcChatEntry>(true) : null;
    }

    private static TipDotNodeLinker FindTipLinker(GameObject root, string graphName, string nodeName)
        => root.GetComponentsInChildren<TipDotNodeLinker>(true)
            .Single(value => value.ConfiguredNodeKey.GraphName == graphName && value.ConfiguredNodeKey.NodeName == nodeName);

    private static Collider FindTouchCollider(ClickableHomeObj owner)
        => Field<List<GameObject>>(owner, "mTouchList")
            .Select(go => go ? go.GetComponent<Collider>() : null)
            .First(c => c != null && c.enabled && c.gameObject.activeInHierarchy);

    private static Collider FindFurnitureCollider(HomeNpcChatEntry entry)
        => FindTouchCollider(entry);

    private static void ClickUiButton(Button button)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable, "Target UI button must be clickable: " + (button ? button.name : "null"));
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
    }

    private static IEnumerator AdvanceCurrentStoryDialog(string screenshotName)
    {
        OptionTalkDialogPanel panel = null;
        WorldPopupWindow popup = null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            panel = Object.FindObjectOfType<OptionTalkDialogPanel>();
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                popup = panel.GetComponent<WorldPopupWindow>();
                if (popup != null && popup.State == WorldPopupWindow.EState.Show) break;
            }
            yield return null;
        }
        Require(panel != null && popup != null && popup.State == WorldPopupWindow.EState.Show,
            "OptionTalkDialogPanel should open and complete Show animation before " + screenshotName);
        Require(panel.GetComponentsInChildren<Component>(true).All(c => c != null),
            "OptionTalkDialogPanel has no missing scripts");
        yield return WaitFrames(2);

        Button nextBtn = Field<Button>(panel, "mNextBtn");
        FieldInfo typingField = typeof(TalkDialog).GetField("mInTypeWriting", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo readyField = typeof(TalkDialog).GetField("mWaitClickForNext", BindingFlags.Instance | BindingFlags.NonPublic);

        bool captured = false;
        int guard = 0;
        while (panel != null && panel.gameObject.activeInHierarchy && guard < 20)
        {
            guard++;
            if ((bool)typingField.GetValue(panel))
            {
                ClickUiButton(nextBtn);
                yield return null;
            }
            float readyDeadline = Time.realtimeSinceStartup + 5f;
            while (panel != null && panel.gameObject.activeInHierarchy && !(bool)readyField.GetValue(panel) && Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
            }
            if (panel == null || !panel.gameObject.activeInHierarchy) break;
            if (!captured)
            {
                captured = true;
                var talkTextField = typeof(TalkDialog).GetField("mTalkContentTxt", BindingFlags.Instance | BindingFlags.NonPublic);
                var talkLang = talkTextField?.GetValue(panel) as WorldMultiLanguageText;
                var talkTmp = talkLang ? talkLang.GetComponent<TMP_Text>() : null;
                Transform talkContent = panel.transform.Find("Content/CharacterAndTalkContent/TalkContent");
                Image bgImg = talkContent ? talkContent.Find("BgImg")?.GetComponent<Image>() : null;
                var corners = new Vector3[4];
                if (talkTmp) talkTmp.rectTransform.GetWorldCorners(corners);
                var bgCorners = new Vector3[4];
                if (bgImg) bgImg.rectTransform.GetWorldCorners(bgCorners);
                Debug.Log("DIALOG_DIAGNOSTIC " + screenshotName
                    + ": talkContent=" + (talkContent ? (talkContent.gameObject.activeInHierarchy + " pos=" + ((RectTransform)talkContent).anchoredPosition + " size=" + ((RectTransform)talkContent).rect + " alpha=" + (talkContent.GetComponent<CanvasGroup>() ? talkContent.GetComponent<CanvasGroup>().alpha.ToString() : "none")) : "null")
                    + " bg=" + (bgImg ? (bgImg.enabled + " sprite=" + (bgImg.sprite ? bgImg.sprite.name : "null") + " color=" + bgImg.color + " inhAlpha=" + bgImg.canvasRenderer.GetInheritedAlpha() + " cull=" + bgImg.canvasRenderer.cull + " corners=" + bgCorners[0] + "/" + bgCorners[2]) : "null")
                    + " tmp=" + (talkTmp ? ("'" + talkTmp.text + "' active=" + talkTmp.isActiveAndEnabled + " max=" + talkTmp.maxVisibleCharacters + " chars=" + talkTmp.textInfo.characterCount + " verts=" + talkTmp.textInfo.meshInfo[0].vertexCount + " inhAlpha=" + talkTmp.canvasRenderer.GetInheritedAlpha() + " font=" + (talkTmp.font ? talkTmp.font.name : "null") + " color=" + talkTmp.color + " corners=" + corners[0] + "/" + corners[2]) : "null"));
                yield return Capture(screenshotName);
            }
            ClickUiButton(nextBtn);
            yield return WaitFrames(2);
        }

        float closeDeadline = Time.realtimeSinceStartup + 8f;
        while (panel != null && panel.gameObject.activeInHierarchy && Time.realtimeSinceStartup < closeDeadline)
        {
            yield return null;
        }
        Require(panel == null || !panel.gameObject.activeInHierarchy, "Story dialog panel should auto-close after final line: " + screenshotName);
    }

    private static IEnumerator RunChecks()
    {
        checksCount = 0;
        string saveRoot = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot);
        draft.name = "电话桌测试";
        Require(creation.TryCreateCharacter(slot, draft, out string createError), "Create character: " + createError);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out createError),
            "Set initial sword: " + createError);

        // Verify HasManualMeetNpc CompositeOr unlock branch in an isolated secondary slot first.
        string manualSlot = creation.CreateSlot();
        var manualDraft = creation.OpenDraft(manualSlot);
        manualDraft.name = "遇人分支";
        Require(creation.TryCreateCharacter(manualSlot, manualDraft, out createError), "Create manual slot: " + createError);
        Require(creation.TrySetInitialWeapon(manualSlot, CharacterCreationOfflineService.SwordInitialWeaponType, out createError),
            "Set manual initial weapon: " + createError);
        var manualProfile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot), manualSlot);
        Require(manualProfile.ApplyCharacterMutation(p =>
        {
            p.Level = 1;
            p.HasManualMeetNpc = true;
            p.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 1 });
        }, "diningtable-test:manual-meet").IsApplied, "Set manual NPC friendship on secondary slot");

        var pointer = new NativePointer();
        home = OpenHome(manualSlot, saveRoot, pointer);
        yield return WaitFrames(5);
        HomeNpcChatEntry manualEntry = DiningTableEntry(home);
        Require(manualEntry != null && manualEntry.GetState() == FurnitureBase.EFurnitureState.CanRepair,
            "HasManualMeetNpc CompositeOr branch unlocks CanRepair state even at Level 1");
        home.Close();
        home = null;
        yield return WaitFrames(3);

        // Case 1: Level 1 -> Locked -> Level 31 RepairNotice -> Level 38 CanRepair
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot), slot);
        var expCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 1;
            p.SubRank = 1;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[1];
        }, "diningtable-test:level1").Status != ProgressionResultStatus.InvalidState, "Set Level 1");
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusDiningTable(home);
        yield return WaitFrames(2);

        GameObject dtObj = home.NativeFurnitureInstance(EFurniture.DiningTable);
        HomeNpcChatEntry entry = DiningTableEntry(home);
        HomeNotice notice = dtObj.GetComponentInChildren<HomeNotice>(true);
        Require(entry != null && dtObj.transform.parent.name == "DiningTableSlot", "DiningTable mounts in DiningTableSlot");
        Require(entry.GetState() == FurnitureBase.EFurnitureState.Locked, "DiningTable is Locked at Level 1");
        Require(notice != null && !notice.ShowNotice, "HomeNotice is hidden when Locked");
        TipDotNodeLinker tip1 = FindTipLinker(dtObj, "DiningTable", "1");
        TipDotNodeLinker tip13 = FindTipLinker(dtObj, "DiningTable", "13");
        Require(!tip1.IsShowTip && !tip13.IsShowTip, "TipDots [1] and [13] are hidden when Locked");
        Require(home.Rules.IsFunctionLimited(57), "Function 57 is limited at Level 1");
        yield return Capture("01-diningtable-locked.png");

        home.Close();
        home = null;
        yield return WaitFrames(3);

        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 31;
            p.SubRank = 4;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[31];
        }, "diningtable-test:level31").IsApplied, "Set Level 31");
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusDiningTable(home);
        yield return WaitFrames(2);

        dtObj = home.NativeFurnitureInstance(EFurniture.DiningTable);
        entry = DiningTableEntry(home);
        notice = dtObj.GetComponentInChildren<HomeNotice>(true);
        var furnitureSvc = entry.Services.PlayerData;
        var dtInfo = entry.Services.GetFurnitureInfo(EFurniture.DiningTable);
        Debug.Log("LV31_DIAGNOSTIC state=" + entry.GetState()
            + " owns=" + furnitureSvc.HasFurniture(EFurniture.DiningTable)
            + " unlock=" + dtInfo.UnlockCondition.IsMet()
            + " noticeCond=" + dtInfo.RepairNoticeCondition.IsMet()
            + " level=" + home.Profile.Save.Player.Level
            + " subRank=" + home.Profile.Save.Player.SubRank
            + " npcs=" + string.Join(",", home.Profile.Save.Player.CharacterProperties.NpcFriendships.Select(f => f.NpcClassId + ":" + f.Level))
            + " dtPos=" + dtObj.transform.position
            + " camPos=" + home.Scene.Camera.transform.position);
        Require(entry.GetState() == FurnitureBase.EFurnitureState.RepairNotice,
            "DiningTable enters RepairNotice at Level 31 (actual=" + entry.GetState()
            + ", unlock=" + dtInfo.UnlockCondition.IsMet()
            + ", noticeCond=" + dtInfo.RepairNoticeCondition.IsMet() + ")");
        Require(notice != null && notice.ShowNotice, "HomeNotice shows RepairNotice banner at Level 31");
        TMP_Text noticeText = notice.GetComponentInChildren<TMP_Text>(true);
        Require(noticeText != null && noticeText.text.Contains("38") && noticeText.text.Contains("解锁"),
            "RepairNotice banner shows Level 38 unlock condition text: " + (noticeText ? noticeText.text : "null"));
        yield return Capture("02-diningtable-repair-notice.png");
        pointer.ClickWorldPoint(home, FindFurnitureCollider(entry).bounds.center);
        yield return WaitFrames(2);
        Require(!home.IsFurnitureStoryRunning && Prop<FurnitureRepairDialog>(home.Hud, "ActiveRepairDialog") == null,
            "Clicking DiningTable in RepairNotice shows condition tip instead of opening repair/story");

        home.Close();
        home = null;
        yield return WaitFrames(3);

        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 38;
            p.SubRank = 4;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[38];
        }, "diningtable-test:level38").IsApplied, "Set Level 38");
        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusDiningTable(home);
        yield return WaitFrames(2);

        dtObj = home.NativeFurnitureInstance(EFurniture.DiningTable);
        entry = DiningTableEntry(home);
        notice = dtObj.GetComponentInChildren<HomeNotice>(true);
        tip1 = FindTipLinker(dtObj, "DiningTable", "1");
        tip13 = FindTipLinker(dtObj, "DiningTable", "13");
        var tipManager = Field<ITipDotNodeManager>(Field<OfflinePartnerTipDotRuntime>(home, "partnerTipDots"), "manager");
        var key11 = new TipDotNodeKey { GraphName = "DiningTable", NodeName = "11" };
        var key13 = new TipDotNodeKey { GraphName = "DiningTable", NodeName = "13" };
        var key3 = new TipDotNodeKey { GraphName = "DiningTable", NodeName = "3" };
        Require(entry.GetState() == FurnitureBase.EFurnitureState.CanRepair, "DiningTable enters CanRepair at Level 38");
        Require(notice == null || !notice.ShowNotice, "HomeNotice banner hides once unlock condition is met");
        Require(tipManager.FindNode(key11).IsShowTip, "TipDot [11] CanRepair node is active at Level 38");
        Require(tip1.IsShowTip && !tip13.IsShowTip, "TipDot [1] DiningTableHasTip is active while [13] unlocked tip is off");
        Require(!home.Rules.IsFunctionLimited(57), "Function 57 is unlocked at Level 38");
        var openTipTexts = dtObj.GetComponentsInChildren<WorldMultiLanguageText>(true)
            .Where(t => t.myKey == "home_repair_open" && t.gameObject.activeInHierarchy).ToArray();
        Require(openTipTexts.Length > 0 && openTipTexts.All(t => t.Text == "打开"),
            "CanRepair floating label localizes home_repair_open to 打开: "
            + string.Join(",", openTipTexts.Select(t => t.Text)));
        yield return Capture("03-diningtable-can-repair.png");

        // Case 2: Full UnlockCommunication story -> Dialog(0) -> Mask DiningTable -> RepairDialog + RepairBtn Mask -> Effect + Notify -> Dialog(1)
        pointer.ClickWorldPoint(home, FindFurnitureCollider(entry).bounds.center);
        yield return WaitFrames(4);
        Require(home.IsFurnitureStoryRunning && home.StoryStatus("UnlockCommunication") == 1,
            "Clicking CanRepair DiningTable starts UnlockCommunication story and persists StoryStatus=1");
        Require(Mathf.Abs(home.Scene.Camera.transform.position.x - 11.02439f) < 0.05f,
            "SetFreeCameraForcePosXNode moves camera near 11.02439, actual=" + home.Scene.Camera.transform.position.x);
        object inputCtrl = Field<object>(home, "input");
        Require(!(bool)Prop<bool>(inputCtrl, "CameraDragEnabled"), "CameraDragNode disables scene camera drag during story");

        yield return AdvanceCurrentStoryDialog("04-story-dialog-0.png");

        // Wait for NativeStoryMaskHost highlighting DiningTable
        NativeStoryMaskHost maskHost = null;
        float maskDeadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < maskDeadline)
        {
            maskHost = Object.FindObjectOfType<NativeStoryMaskHost>();
            if (maskHost != null && maskHost.Manager != null && maskHost.Manager.EnableRectMask) break;
            yield return null;
        }
        Require(maskHost != null && maskHost.Manager.EnableRectMask, "RepairFurnitureNode activates NativeStoryMaskHost on DiningTable");
        yield return WaitFrames(8);
        yield return Capture("05-story-furniture-mask.png");

        Vector2 outsidePoint = new Vector2(8f, Screen.height * 0.5f);
        List<RaycastResult> outsideHits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = outsidePoint }, outsideHits);
        StoryMaskGraphic maskGraphic = maskHost.GetComponentInChildren<StoryMaskGraphic>(true);
        Require(outsideHits.Count > 0 && outsideHits[0].gameObject == maskGraphic.gameObject,
            "Story mask blocks UI raycasts outside DiningTable hole");

        pointer.ClickWorldPoint(home, FindFurnitureCollider(entry).bounds.center);
        FurnitureRepairDialog repairDialog = null;
        float repairDeadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < repairDeadline)
        {
            repairDialog = Prop<FurnitureRepairDialog>(home.Hud, "ActiveRepairDialog");
            if (repairDialog != null && repairDialog.gameObject.activeInHierarchy)
            {
                var wp = repairDialog.GetComponent<WorldPopupWindow>();
                if (wp == null || wp.State == WorldPopupWindow.EState.Show) break;
            }
            yield return null;
        }
        Require(repairDialog != null && repairDialog.RepairBtn != null && repairDialog.RepairBtn.gameObject.activeInHierarchy,
            "Clicking masked DiningTable opens FurnitureRepairDialog");
        yield return WaitFrames(8);
        yield return Capture("06-story-repair-btn-mask.png");

        ClickUiButton(repairDialog.RepairBtn);
        FurnitureRepairedNotifyDialog notifyDialog = null;
        float notifyDeadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < notifyDeadline)
        {
            notifyDialog = Object.FindObjectOfType<FurnitureRepairedNotifyDialog>();
            if (notifyDialog != null && notifyDialog.gameObject.activeInHierarchy)
            {
                var wp = notifyDialog.GetComponent<WorldPopupWindow>();
                if (wp == null || wp.State == WorldPopupWindow.EState.Show) break;
            }
            yield return null;
        }
        Require(home.Session.OwnsFurniture(2), "RepairBtn commits OwnsFurniture(2)");
        Require(entry.GetState() == FurnitureBase.EFurnitureState.Functional, "DiningTable becomes Functional after repair");
        yield return WaitFrames(2);
        Require(!tipManager.FindNode(key11).IsShowTip && !tip1.gameObject.activeInHierarchy && !tipManager.FindNode(key3).IsShowTip,
            "Repair clears TipDot [11] CanUnlock and deactivates unrepaired TipDot [1]");
        Require(notifyDialog != null, "FurnitureRepairedNotifyDialog appears with repair effect");
        GameObject repairFx = Field<GameObject>(entry, "mRepairEffect");
        Require(repairFx == null || repairFx.scene.IsValid(), "Native mRepairEffect reference is valid");
        yield return WaitFrames(4);
        yield return Capture("07-repair-notify-and-effect.png");

        // Close FurnitureRepairedNotifyDialog and complete DialogNode(1)
        CloseWindowBtn closeNotifyBtn = notifyDialog.GetComponentsInChildren<CloseWindowBtn>(true)
            .FirstOrDefault(b => b.GetComponent<Button>() != null && b.gameObject.activeInHierarchy);
        if (closeNotifyBtn != null)
        {
            ClickUiButton(closeNotifyBtn.GetComponent<Button>());
        }
        else
        {
            home.Hud.CloseDialog();
        }
        yield return AdvanceCurrentStoryDialog("08-story-dialog-1.png");

        float endDeadline = Time.realtimeSinceStartup + 8f;
        while (home.IsFurnitureStoryRunning && Time.realtimeSinceStartup < endDeadline) yield return null;
        Require(!home.IsFurnitureStoryRunning && home.StoryStatus("UnlockCommunication") == 2,
            "UnlockCommunication completes cleanly with StoryStatus=2");
        Require((bool)Prop<bool>(inputCtrl, "CameraDragEnabled"), "Scene camera drag is restored after story finishes");
        yield return WaitFrames(4);
        yield return Capture("09-post-repair-idle.png");

        // Case 3: Cold save persistence & Functional touch opens PartnerPanel Tab 0 (NpcList)
        home.Close();
        home = null;
        yield return WaitFrames(3);

        home = OpenHome(slot, saveRoot, pointer);
        yield return WaitFrames(6);
        FocusDiningTable(home);
        yield return WaitFrames(2);
        dtObj = home.NativeFurnitureInstance(EFurniture.DiningTable);
        entry = DiningTableEntry(home);
        HomeNpcBubbleTip bubble = dtObj.GetComponentInChildren<HomeNpcBubbleTip>(true);
        tip1 = FindTipLinker(dtObj, "DiningTable", "1");
        tip13 = FindTipLinker(dtObj, "DiningTable", "13");
        tipManager = Field<ITipDotNodeManager>(Field<OfflinePartnerTipDotRuntime>(home, "partnerTipDots"), "manager");
        Require(home.Session.OwnsFurniture(2) && home.StoryStatus("UnlockCommunication") == 2,
            "Cold reopen persists OwnsFurniture(2) and UnlockCommunication StoryStatus=2");
        Require(entry.GetState() == FurnitureBase.EFurnitureState.Functional, "Cold reopen keeps Functional state");
        Require(bubble != null && !bubble.IsShow, "Bubble tip stays hidden when no active partner dialogue exists");
        Require(!tipManager.FindNode(key11).IsShowTip && !tipManager.FindNode(key3).IsShowTip
            && tip13.IsShowTip == tipManager.FindNode(key13).IsShowTip,
            "Cold reopen keeps [11] CanUnlock and [3] CallTab inactive while [13] matches graph");

        pointer.ClickWorldPoint(home, FindFurnitureCollider(entry).bounds.center);
        yield return WaitFrames(6);
        NpcPanel npcPanel = Object.FindObjectOfType<NpcPanel>();
        Require(npcPanel != null && npcPanel.gameObject.activeInHierarchy, "Clicking Functional DiningTable opens NpcPanel");
        Require(Field<GameObject>(npcPanel, "mNpcListGo").activeSelf, "DiningTable click opens NpcList tab by default");
        home.Hud.CloseDialog();
        yield return WaitFrames(4);

        // Case 4: NPC Dialogue Bubble Tip + TipDot [13] + DiningTableNpcPopClick -> Dialogue Tab -> Playback Acknowledge
        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.CharacterProperties.NpcFriendships.RemoveAll(f => f.NpcClassId == 8402);
            p.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 40 });
        }, "diningtable-test:meet-8402").IsApplied, "Add friendship for NPC 8402");
        Require(profile.EnsureLocalActorIdentities().Status != ProgressionResultStatus.InvalidState, "Ensure local actor identities");
        long playerId = profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        long npcId = profile.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8402);
        long seededDialogueId = 0;
        DateTime now = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
        Require(profile.ApplyCharacterMutation(p =>
        {
            seededDialogueId = OfflineLocalActorIdentities.Allocate(p);
            var record = PartnerConversationRecord.Create(playerId, npcId, 2021, 1, seededDialogueId, now);
            p.CharacterProperties.NpcConversations.Records.Clear();
            p.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, record));
        }, "diningtable-test:seed-dialogue").IsApplied, "Seed active unread conversation for NPC 8402");

        home.Profile.Reload();
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(6);

        SpriteRenderer bubbleIcon = Field<SpriteRenderer>(bubble, "mIcon");
        SkeletonAnimation bubbleSkel = Field<SkeletonAnimation>(bubble, "mSkeletonAnimation");
        string showAnim = Field<string>(bubble, "mShowAnim");
        string notShowAnim = Field<string>(bubble, "mNotShowAnim");
        Require(bubble.IsShow, "HomeNpcBubbleTip activates on unread NPC dialogue");
        Require(bubbleIcon != null && bubbleIcon.sprite != null, "HomeNpcBubbleTip loads official NPC icon sprite");
        Require(bubbleSkel != null && bubbleSkel.AnimationName == showAnim,
            "HomeNpcBubbleTip plays show animation (" + showAnim + "), actual=" + (bubbleSkel ? bubbleSkel.AnimationName : "null"));
        Require(tipManager.FindNode(key3).IsShowTip && tipManager.FindNode(key13).IsShowTip && tip13.IsShowTip,
            "TipDot [3] CallTab and [13] UnlockedDiningTableHasTip activate with unread dialogue");
        yield return Capture("10-diningtable-bubble-tip-active.png");

        DiningTableNpcPopClick popClick = dtObj.GetComponentInChildren<DiningTableNpcPopClick>(true);
        Require(popClick != null, "DiningTableNpcPopClick component exists");
        Collider popCol = FindTouchCollider(popClick);
        Require(popCol != null && popCol.enabled && popCol.gameObject.activeInHierarchy, "DiningTableNpcPopClick collider is active");
        pointer.ClickWorldPoint(home, popCol.bounds.center);
        yield return WaitFrames(6);

        npcPanel = Object.FindObjectOfType<NpcPanel>();
        Require(npcPanel != null && Field<GameObject>(npcPanel, "mNpcDialogueListGo").activeSelf,
            "Clicking DiningTableNpcPopClick opens NpcPanel directly on Dialogue tab");
        WorldPopupWindow panelWindow = npcPanel.GetComponentInParent<WorldPopupWindow>();
        float panelShowDeadline = Time.realtimeSinceStartup + 6f;
        NpcPanelDialogueListNpcItem npcGroup = null;
        while (Time.realtimeSinceStartup < panelShowDeadline)
        {
            bool windowShown = panelWindow == null || panelWindow.State == WorldPopupWindow.EState.Show;
            npcGroup = Object.FindObjectsOfType<NpcPanelDialogueListNpcItem>(false)
                .FirstOrDefault(item =>
                {
                    var btn = Field<Button>(item, "mBtn");
                    return btn != null && btn.gameObject.activeInHierarchy && btn.interactable;
                });
            if (windowShown && npcGroup != null) break;
            yield return null;
        }
        Require(npcGroup != null, "NpcPanelDialogueListNpcItem renders in Dialogue tab");
        yield return WaitFrames(2);
        var npcGroupIcon = npcGroup.GetComponentInChildren<Zjcs.Official.ProfessionUI.PlayerInfoUI>(true);
        Require(npcGroupIcon != null && npcGroupIcon.PlayerIcon != null && npcGroupIcon.PlayerIcon.sprite != null,
            "NpcPanelDialogueListNpcItem loads official NPC avatar sprite");
        yield return Capture("11-partner-panel-dialogue-tab.png");

        ClickUiButton(Field<Button>(npcGroup, "mBtn"));
        NpcPanelDialogueListDialogueItem diagItem = null;
        float itemDeadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < itemDeadline)
        {
            diagItem = Object.FindObjectsOfType<NpcPanelDialogueListDialogueItem>(false)
                .FirstOrDefault(item =>
                {
                    var contentRoot = Field<GameObject>(item, "mContentRoot");
                    var btn = Field<Button>(item, "mButton");
                    return (contentRoot == null || contentRoot.activeInHierarchy)
                        && btn != null && btn.gameObject.activeInHierarchy && btn.interactable;
                });
            if (diagItem != null) break;
            yield return null;
        }
        Require(diagItem != null, "Unread dialogue item renders in Dialogue list");
        yield return WaitFrames(2);
        ClickUiButton(Field<Button>(diagItem, "mButton"));
        yield return WaitFrames(6);

        PartnerDialoguePlaybackView playbackView = Object.FindObjectOfType<PartnerDialoguePlaybackView>();
        Require(playbackView != null && playbackView.Content != null && playbackView.Content.activeInHierarchy,
            "Clicking dialogue item opens PartnerDialoguePlaybackView");
        yield return Capture("12-partner-dialogue-playback.png");

        // Advance and acknowledge all nodes in seeded conversation via real UI clicks / auto-advance
        NpcDialoguePanel diagPanel = playbackView.GetComponentInChildren<NpcDialoguePanel>(true);
        Button fastEndBtn = Field<Button>(diagPanel, "mFastEndTypeWriterBtn");
        float ackDeadline = Time.realtimeSinceStartup + 45f;
        var conversations = Field<OfflinePartnerConversationUiSession>(home, "partnerConversations");
        while (Time.realtimeSinceStartup < ackDeadline)
        {
            if (conversations.Playback.Dialogues.TryGetValue(seededDialogueId, out var d)
                && !PartnerDialogueListRules.HasRedDot(d, conversations.Playback))
            {
                break;
            }
            if (fastEndBtn != null && fastEndBtn.gameObject.activeInHierarchy && fastEndBtn.interactable)
            {
                ClickUiButton(fastEndBtn);
            }
            var optItem = playbackView.GetComponentsInChildren<NpcDialoguePanelOptionItem>(false)
                .FirstOrDefault(o =>
                {
                    var btn = o.GetComponentInChildren<Button>(false);
                    return btn != null && btn.gameObject.activeInHierarchy && btn.interactable;
                });
            if (optItem != null)
            {
                ClickUiButton(optItem.GetComponentInChildren<Button>(false));
            }
            yield return WaitFrames(3);
        }
        conversations.Playback.Dialogues.TryGetValue(seededDialogueId, out var finishedDialogue);
        Require(finishedDialogue != null
            && !PartnerDialogueListRules.HasRedDot(finishedDialogue, conversations.Playback),
            "Partner dialogue acknowledgement completes and clears unread red dot (currentNode="
            + (finishedDialogue != null ? finishedDialogue.CurrentNode.ToString() : "null")
            + ", status=" + (finishedDialogue != null ? finishedDialogue.DialogueStatus.ToString() : "null")
            + ", hasRedDot=" + (finishedDialogue != null && PartnerDialogueListRules.HasRedDot(finishedDialogue, conversations.Playback)) + ")");
        NpcDialoguePanelRewardItemContent rewardContent = null;
        ItemIcon rewardIcon = null;
        float rewardDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < rewardDeadline)
        {
            rewardContent = playbackView.GetComponentsInChildren<NpcDialoguePanelRewardItemContent>(false).FirstOrDefault();
            rewardIcon = rewardContent != null ? rewardContent.GetComponentInChildren<ItemIcon>(false) : null;
            if (rewardIcon != null
                && rewardIcon.GetComponentsInChildren<Image>(false).Any(img => img != null && img.sprite != null
                    && img.sprite.name.IndexOf("item_110", StringComparison.OrdinalIgnoreCase) >= 0)
                && rewardIcon.GetComponentsInChildren<ItemIconAttachmentCell>(false).Any()) break;
            yield return null;
        }
        Require(rewardIcon != null, "Final dialogue node renders RewardItem ItemIcon");
        var rewardImages = rewardIcon.GetComponentsInChildren<Image>(false);
        Require(rewardImages.Any(img => img != null && img.sprite != null && img.sprite.name.IndexOf("item_110", StringComparison.OrdinalIgnoreCase) >= 0),
            "Final dialogue reward ItemIcon displays real official item_110 sprite");
        Require(rewardIcon.GetComponentsInChildren<ItemIconAttachmentCell>(false).Any(),
            "Final dialogue reward ItemIcon mounts official GotGrayObj attachment");
        yield return Capture("12b-partner-dialogue-completed.png");

        home.Hud.CloseDialog();
        yield return WaitFrames(5);
        Require(!bubble.IsShow, "HomeNpcBubbleTip hides after dialogue is read");
        Require(bubbleSkel == null || bubbleSkel.AnimationName == notShowAnim,
            "HomeNpcBubbleTip switches back to idle animation (" + notShowAnim + "), actual=" + (bubbleSkel ? bubbleSkel.AnimationName : "null"));
        Require(!tipManager.FindNode(key3).IsShowTip && tip13.IsShowTip == tipManager.FindNode(key13).IsShowTip,
            "TipDot [3] CallTab clears after dialogue acknowledgement");

        // Test FunctionLimit 57 blocking behavior (Drop Level back to 1 with !HasManualMeetNpc temporarily)
        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 1;
            p.SubRank = 1;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[1];
            p.HasManualMeetNpc = false;
            var unreadRecord = PartnerConversationRecord.Create(playerId, npcId, 2021, 1, seededDialogueId, now);
            p.CharacterProperties.NpcConversations.Records.Clear();
            p.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, unreadRecord));
        }, "diningtable-test:limit57").IsApplied, "Simulate FunctionLimit 57 condition");
        home.Profile.Reload();
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(4);
        Require(home.Rules.IsFunctionLimited(57) && !bubble.IsShow, "FunctionLimit 57 hides NPC bubble tip");
        pointer.ClickWorldPoint(home, FindFurnitureCollider(entry).bounds.center);
        yield return WaitFrames(3);
        Require(Object.FindObjectOfType<NpcPanel>() == null, "FunctionLimit 57 blocks opening PartnerPanel from DiningTable");

        // Restore Level 38 & verify all 6 skins
        profile.Reload();
        Require(profile.ApplyCharacterMutation(p =>
        {
            p.Level = 38;
            p.SubRank = 4;
            p.Rank = 1;
            p.Experience = expCatalog.CumulativeLevelExperience[38];
        }, "diningtable-test:restore38").IsApplied, "Restore Level 38");
        home.Profile.Reload();
        Invoke(home, "RefreshPartnerDerivedState");
        yield return WaitFrames(3);

        var skins = new (int id, string prefabName, string frameName)[]
        {
            (4217, "DiningTableChristmas", "13-skin-4217-christmas.png"),
            (4224, "DiningTableChineseStyle", "14-skin-4224-chinesestyle.png"),
            (4236, "DiningTable1stAnniversary", "15-skin-4236-anniversary.png"),
            (4246, "DiningTableChineseOdyssey", "16-skin-4246-odyssey.png"),
            (4259, "DiningTableHalloween", "17-skin-4259-halloween.png"),
            (4205, "DiningTable", null),
        };
        foreach (var (skinId, prefabName, frameName) in skins)
        {
            Require(home.HomeProgression.EquipSkin(home.Session, 0, (int)EFurniture.DiningTable, skinId, _ => 1, "diningtable-test:skin:" + skinId),
                "Equip DiningTable skin " + skinId);
            home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
            home.RefreshNativeFurnitureSkins();
            yield return WaitFrames(4);
            GameObject live = home.NativeFurnitureInstance(EFurniture.DiningTable);
            Require(live != null && live.activeInHierarchy && live.name == prefabName + "(Clone)"
                && live.transform.parent.name == "DiningTableSlot",
                "Switched DiningTable skin " + skinId + " mounts " + prefabName + "(Clone)");
            Require(live.GetComponentsInChildren<Component>(true).All(c => c != null),
                "DiningTable skin " + prefabName + " has zero missing scripts");
            Require(live.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m != null)),
                "DiningTable skin " + prefabName + " binds valid materials");
            if (frameName != null)
            {
                yield return Capture(frameName);
            }
        }

        home.Close();
        home = null;
        yield return WaitFrames(3);
    }
}
