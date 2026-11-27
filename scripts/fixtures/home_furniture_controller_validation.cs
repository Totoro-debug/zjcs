using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Offline.HomeSceneInput;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeFurnitureControllerValidation
{
    const string Active = "Zjcs.HomeFurnitureControllerValidation.Active";
    static HomeFurnitureControllerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeFurnitureControllerChecks").AddComponent<HomeFurnitureControllerChecks>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Furniture controller validation timed out."));
        };
    }
    public static void Run()
    {
        string expected = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_PROJECT"));
        if (Path.GetFullPath(Application.dataPath) != Path.Combine(expected, "Assets"))
            throw new InvalidOperationException("The explicitly selected independent project is required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup +
            (Environment.GetEnvironmentVariable("ZJCS_FURNITURE_ALL_WEAPONS") == "1" ? 900 : 180));
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_FURNITURE_CONTROLLERS_PASSED: four states, native click dispatch, notices, repair timing, stories and teardown.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed partial class HomeFurnitureControllerChecks : MonoBehaviour
{
    readonly List<GameObject> roots = new List<GameObject>();
    Camera viewCamera;
    EventSystem events;
    OfflineHomeSceneInput input;
    readonly Pointer pointer = new Pointer();
    readonly Services services = new Services();
    int errors;

    IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        var steps = new Stack<IEnumerator>(); steps.Push(Checks());
        Exception failure = null;
        while (steps.Count > 0)
        {
            object current;
            try
            {
                if (!steps.Peek().MoveNext()) { steps.Pop(); continue; }
                current = steps.Peek().Current;
                if (current is IEnumerator nested) { steps.Push(nested); continue; }
            }
            catch (Exception error) { failure = error; break; }
            yield return current;
        }
        foreach (GameObject root in roots) if (root) Destroy(root);
        yield return null;
        input?.Dispose();
        Application.logMessageReceived -= OnLog;
        HomeFurnitureControllerValidation.Finish(failure ?? (errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)));
    }

    IEnumerator Checks()
    {
        viewCamera = New("Camera").AddComponent<Camera>();
        viewCamera.orthographic = true; viewCamera.orthographicSize = 4; viewCamera.transform.position = new Vector3(0, 0, -10);
        events = EventSystem.current ? EventSystem.current : New("Events").AddComponent<EventSystem>();
        input = new OfflineHomeSceneInput(() => viewCamera, () => events, services, pointer);
        var root = New("StorageController"); root.SetActive(false);
        var target = new GameObject("OriginalColliderTarget", typeof(BoxCollider)); target.transform.SetParent(root.transform, false);
        GameObject[] states = Enumerable.Range(0, 4).Select(index => Child(root, "State" + index)).ToArray();
        var effect = Child(root, "RepairEffect"); effect.SetActive(false);
        var noticeRoot = Child(root, "HomeNotice");
        var noticeShown = Child(noticeRoot, "ShowNotice");
        var text = Child(noticeShown, "NoticeText").AddComponent<TextMeshPro>();
        var font = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
        Require(font, "Installed official font is available");
        var serializedText = new SerializedObject(text);
        serializedText.FindProperty("m_fontAsset").objectReferenceValue = font;
        serializedText.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
        serializedText.ApplyModifiedPropertiesWithoutUndo();
        var label = text.gameObject.AddComponent<WorldMultiLanguageText>(); label.Localizer = key => key == "notice" ? "Reach level {0}" : "Custom {0}";
        var notice = noticeRoot.AddComponent<HomeNotice>();
        notice.Bind(label, Switch<bool>(noticeRoot, new[] { new[] { noticeShown } }, new[] { 1 }), "", services);
        var actor = root.AddComponent<StoreBoxFurniture>();
        actor.Bind(EFurniture.StorageBox, Switch<FurnitureBase.EFurnitureState>(root, states.Select(value => new[] { value }).ToArray(), new[] { 0, 1, 2, 3 }), notice, effect, .6f, 1.5f, services);
        input.Bind(actor);
        var lateTarget = Child(root, "InactiveOriginalCollider");
        lateTarget.transform.localPosition = new Vector3(2, 0, 0);
        lateTarget.AddComponent<BoxCollider>(); lateTarget.SetActive(false);
        var clickSound = root.AddComponent<EntityClickSound>();
        clickSound.InputOperationModule = input.InputModule;
        var clip = AudioClip.Create("ProcessLocalClickFixture", 48, 1, 48000, false);
        clickSound.Sound = clip;
        int soundPosts = 0;
        clickSound.Post = (value, emitter) => {
            Require(value == clip && emitter == root, "Click audio preserves its clip and original emitter");
            soundPosts++;
        };
        int clickCallbacks = 0; actor.OnClickAction.AddListener(() => clickCallbacks++);
        root.SetActive(true);
        yield return null;
        Require(actor.GetState() == FurnitureBase.EFurnitureState.Locked && states[0].activeSelf, "Initial original locked state");
        Require(services.ConditionSubscriptions.Count == 2 && services.HomeChanges.Count == 1 && services.Awards.Count == 1, "Original start subscriptions");
        yield return Click(target);
        Require(clickCallbacks == 1, "Native pointer dispatcher reached the actor callback");
        Require(services.Calls.Count == 0, "Locked click has no functional action");
        Require(soundPosts == 1, "Original click sound uses native collider dispatch independently of furniture state");
        clickSound.enabled = false; yield return Click(target);
        Require(soundPosts == 1, "Disabling click sound removes its native input subscription");
        clickSound.enabled = true; yield return Click(target);
        Require(soundPosts == 2, "Re-enabling click sound subscribes exactly once");
        lateTarget.SetActive(true); yield return Click(lateTarget);
        Require(soundPosts == 3, "Original click sound collects initially inactive child colliders");
        lateTarget.SetActive(false);
        var outside = New("OtherBuildingCollider"); outside.AddComponent<BoxCollider>();
        outside.transform.position = new Vector3(-2, 0, 0); yield return Click(outside);
        Require(soundPosts == 3, "A collider belonging to another building does not post this sound");
        Destroy(outside);

        services.Notice.Met = true; services.TriggerCondition(services.Notice);
        Require(actor.GetState() == FurnitureBase.EFurnitureState.RepairNotice && states[1].activeSelf, "Notice transition callback updates native state switch");
        Require(notice.ShowNotice && text.text == "Reach level 9", "Original key and argument notice rendering");
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "message:notice-tip" }), "RepairNotice click shows original notice message; calls=" + string.Join(",", services.Calls));
        services.Calls.Clear();

        services.Unlock.Met = true; services.TriggerCondition(services.Unlock);
        Require(actor.GetState() == FurnitureBase.EFurnitureState.CanRepair && states[2].activeSelf, "Unlock subscription transitions to CanRepair");
        services.PlayerValue.TimeOffset();
        Require(!notice.ShowNotice, "Time-offset handler refreshes notice");
        services.Info.RepairStoryName = "OfficialStory";
        yield return Click(target);
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "story-action", "context:FurnitureBase", "play:OfficialStory", "story-action", "play:OfficialStory" }), "Story first, exact owner and context reuse");
        services.Calls.Clear(); services.PlayerValue.StoryStarted = true;
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "repair:StorageBox:False" }), "Previously started story opens non-build repair dialog");
        services.Calls.Clear(); services.Info.RepairStoryName = "";
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "repair:StorageBox:False" }), "Empty story directly opens repair dialog");

        services.Calls.Clear(); services.PlayerValue.Owned = true;
        services.PlayerValue.Repaired(EFurniture.Cart);
        Require(!effect.activeSelf && states[2].activeSelf, "Other furniture repair does not start this effect");
        services.PlayerValue.Repaired(EFurniture.StorageBox);
        Require(effect.activeSelf && states[2].activeSelf, "Repair starts effect without switching early");
        yield return new WaitForSeconds(.25f);
        Require(effect.activeSelf && states[2].activeSelf, "Repair state remains before show time");
        yield return new WaitForSeconds(.45f);
        Require(effect.activeSelf && states[3].activeSelf, "Repair switches after original 0.6 seconds");
        yield return new WaitForSeconds(.95f);
        Require(!effect.activeSelf && states[3].activeSelf, "Repair hides effect after original 1.5 seconds");
        services.Unlock.Met = false; services.Notice.Met = false;
        Require(actor.GetState() == FurnitureBase.EFurnitureState.Functional, "Ownership has priority over both conditions");
        Require(!services.Awards.Single()(new HomeFurnitureAward(false)) && services.Awards.Single()(new HomeFurnitureAward(true)), "Only repair-source awards are consumed");

        services.Disabled = true;
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "disabled:StorageBox", "error:53851" }), "Original functional disable gate and error");
        services.Calls.Clear(); services.Disabled = false;
        yield return Click(target);
        Require(services.Calls.SequenceEqual(new[] { "disabled:StorageBox", "bag" }), "Storage original OnClick opens shared bag");
        services.Calls.Clear(); services.WindowsClosed = false;
        yield return Click(target);
        Require(services.Calls.Count == 0, "Modal window blocks native furniture click");
        services.WindowsClosed = true; services.OwnHome = false;
        yield return Click(target);
        Require(services.Calls.Count == 0, "Other player's home blocks native furniture click");
        services.PlayerValue.Owned = false;
        services.DisplayedPlayer = new Player { Owned = true };
        Require(actor.GetState() == FurnitureBase.EFurnitureState.Functional, "Furniture state reads displayed home owner separately from main player");
        services.DisplayedPlayer = null;
        services.PlayerValue.Owned = true;
        services.OwnHome = true;

        // A second notice validates InitWrapper disable/re-enable cleanup and
        // original callback order without rewriting the controller's state.
        var callbacks = new List<string>();
        services.Notice.Met = false; services.Unlock.Met = false;
        notice.Init(services.Notice, services.Unlock, () => callbacks.Add("notice"), () => callbacks.Add("unlock"));
        int subscriptions = services.ConditionSubscriptions.Count;
        notice.Init(services.Notice, services.Unlock, () => callbacks.Add("notice"), () => callbacks.Add("unlock"));
        Require(services.ConditionSubscriptions.Count == subscriptions, "Repeated Init does not accumulate notice subscriptions");
        services.Notice.Met = true; services.PlayerValue.TimeOffset();
        services.Unlock.Met = true; services.PlayerValue.TimeOffset();
        services.PlayerValue.TimeOffset();
        Require(callbacks.SequenceEqual(new[] { "notice", "unlock" }), "Notice and unlock callbacks occur only on false-to-true transitions");
        services.OwnHome = false; services.PlayerValue.TimeOffset();
        Require(!notice.ShowNotice, "Notice is hidden outside owned home");
        services.OwnHome = true; services.Unlock.Met = false;
        notice.Bind(label, Switch<bool>(noticeRoot, new[] { new[] { noticeShown } }, new[] { 1 }), "custom", services);
        notice.Init(services.Notice, services.Unlock, null, null);
        Require(text.text == "Custom 9", "Original custom notice key retains format arguments");
        services.ConditionNotice.Changed = true;
        services.Unlock.Met = true;
        yield return null; yield return null;
        Require(!notice.ShowNotice, "Condition notice OnUpdate can refresh unlock");

        services.Notice.Met = false; services.Unlock.Met = false; services.PlayerValue.Owned = false;
        foreach (Action action in services.HomeChanges.ToArray()) action();
        Require(states[0].activeSelf, "Post-home change reapplies state and notice initialization");
        actor.enabled = false; services.PlayerValue.Owned = true;
        services.PlayerValue.Repaired(EFurniture.StorageBox);
        Require(states[3].activeSelf && !effect.activeSelf, "Inactive controller updates immediately without coroutine");
        actor.enabled = true;

        int postsBeforeDestroy = soundPosts;
        Destroy(root); yield return null;
        var afterDestroy = New("AfterClickSoundDestroy"); afterDestroy.AddComponent<BoxCollider>();
        yield return Click(afterDestroy);
        Require(soundPosts == postsBeforeDestroy, "Destroyed click sound leaves no native input callback");
        Destroy(afterDestroy); Destroy(clip);
        Require(services.ConditionSubscriptions.Count == 0 && services.HomeChanges.Count == 0 && services.Awards.Count == 0, "Destroy disposes original subscriptions and award handler");
        Require(services.PlayerValue.RepairSubscribers == 0 && services.PlayerValue.TimeSubscribers == 0 && services.ContextDisposed == 1, "Destroy releases player callbacks and reused story context");
        yield return CheckWeaponRack();
        yield return CheckNativeWeapon();
        yield return CheckDiningTableControllers();
        yield return CheckBedHangUpControllers();
        Debug.Log("Furniture controller assertions completed.");
    }

    IEnumerator CheckDiningTableControllers()
    {
        viewCamera.transform.position = new Vector3(0, 0, -10);
        viewCamera.orthographicSize = 4;
        services.OwnHome = true;
        services.WindowsClosed = true;
        services.PlayerValue.Owned = true;
        services.Disabled = false;
        services.FunctionLimited = false;
        services.NpcPanels.Clear();
        services.DailyRefreshes = 0;

        var popHost = New("DiningTablePopHost");
        popHost.SetActive(false);
        var popCollider = Child(popHost, "PopBubbleCollider");
        popCollider.transform.position = Vector3.zero;
        popCollider.AddComponent<BoxCollider>();
        var popClick = popHost.AddComponent<DiningTableNpcPopClick>();
        popClick.Services = services;
        var soPop = new SerializedObject(popClick);
        var touchListProp = soPop.FindProperty("mTouchList");
        touchListProp.arraySize = 1;
        touchListProp.GetArrayElementAtIndex(0).objectReferenceValue = popCollider;
        soPop.ApplyModifiedPropertiesWithoutUndo();
        input.Bind(popClick);
        popHost.SetActive(true);
        yield return null;
        yield return Click(popCollider);
        Require(services.NpcPanels.Count == 1 && services.NpcPanels[0] == 1, "DiningTableNpcPopClick opens NpcPanel Dialogue tab (1)");
        Destroy(popHost);
        yield return null;

        services.NpcPanels.Clear();
        services.Now = new DateTime(2026, 11, 24, 5, 59, 59, 950);
        services.ResetDate = new DateTime(2026, 11, 23, 6, 0, 0);
        var chatHost = New("HomeNpcChatHost");
        chatHost.SetActive(false);
        var chatCollider = Child(chatHost, "TableCollider");
        chatCollider.transform.position = Vector3.zero;
        chatCollider.AddComponent<BoxCollider>();
        var chatStates = Enumerable.Range(0, 4).Select(index => Child(chatHost, "ChatState" + index)).ToArray();
        var chatNoticeRoot = Child(chatHost, "ChatNotice");
        var chatNoticeText = Child(chatNoticeRoot, "ChatNoticeTxt").AddComponent<TextMeshPro>().gameObject.AddComponent<WorldMultiLanguageText>();
        var chatNotice = chatNoticeRoot.AddComponent<HomeNotice>();
        chatNotice.Bind(chatNoticeText, Switch<bool>(chatNoticeRoot, new[] { new[] { Child(chatNoticeRoot, "Shown") } }, new[] { 1 }), "", services);
        var chat = chatHost.AddComponent<HomeNpcChatEntry>();
        chat.Bind(EFurniture.DiningTable, Switch<FurnitureBase.EFurnitureState>(chatHost, chatStates.Select(value => new[] { value }).ToArray(), new[] { 0, 1, 2, 3 }), chatNotice, null, 0.2f, 1.0f, services);
        var soChat = new SerializedObject(chat);
        var chatTouch = soChat.FindProperty("mTouchList");
        chatTouch.arraySize = 1;
        chatTouch.GetArrayElementAtIndex(0).objectReferenceValue = chatCollider;
        soChat.ApplyModifiedPropertiesWithoutUndo();
        input.Bind(chat);
        chatHost.SetActive(true);
        yield return null;

        services.FunctionLimited = true;
        yield return Click(chatCollider);
        Require(services.NpcPanels.Count == 0, "HomeNpcChatEntry blocks click when FunctionType 57 is limited");
        services.PlayerValue.TimeOffset();
        Require(services.DailyRefreshes == 0, "HomeNpcChatEntry ignores debug time offset when FunctionType 57 is limited");

        services.FunctionLimited = false;
        yield return Click(chatCollider);
        Require(services.NpcPanels.Count == 1 && services.NpcPanels[0] == 0, "HomeNpcChatEntry opens NpcPanel default tab when unlocked");
        services.PlayerValue.TimeOffset();
        Require(services.DailyRefreshes == 1, "HomeNpcChatEntry triggers RefreshDailyDialogues on debug time offset");
        yield return new WaitForSeconds(0.15f);
        Require(services.DailyRefreshes >= 2, "HomeNpcChatEntry coroutine triggers RefreshDailyDialogues after daily reset boundary");
        Destroy(chatHost);
        yield return null;
        Require(services.PlayerValue.TimeSubscribers == 0, "Destroyed HomeNpcChatEntry unsubscribes debug time offset");

        var playerUi = new BubblePlayerUi();
        var bubbleSvc = new BubbleServicesImpl();
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        bubbleSvc.Sprites["UI/NPC/Npc_Icon/Map_11/npc_icon_8401"] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        var bubbleHost = New("HomeNpcBubbleHost");
        bubbleHost.SetActive(false);
        var iconRenderer = Child(bubbleHost, "Icon").AddComponent<SpriteRenderer>();
        var bubbleVisual = Child(bubbleHost, "BubbleVisual");
        var bubble = bubbleHost.AddComponent<HomeNpcBubbleTip>();
        bubble.PlayerServices = playerUi;
        bubble.FurnitureServices = services;
        bubble.BubbleServices = bubbleSvc;
        var soBubble = new SerializedObject(bubble);
        soBubble.FindProperty("mIcon").objectReferenceValue = iconRenderer;
        soBubble.ApplyModifiedPropertiesWithoutUndo();
        var switcherField = typeof(HomeNpcBubbleTip).GetField("mShowStateSwitcher", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        switcherField.SetValue(bubble, Switch<bool>(bubbleHost, new[] { new[] { bubbleVisual } }, new[] { 1 }));
        bubbleHost.SetActive(true);
        yield return null;
        yield return null;
        Require(!bubble.IsShow && !bubbleVisual.activeSelf && iconRenderer.sprite == null, "HomeNpcBubbleTip hides bubble when no dialogue has red dot");

        bubbleSvc.ActiveNpcClassId = 8401;
        bubbleSvc.RaiseDialogueChanged();
        yield return null;
        yield return null;
        Require(bubble.IsShow && bubbleVisual.activeSelf && iconRenderer.sprite == bubbleSvc.Sprites["UI/NPC/Npc_Icon/Map_11/npc_icon_8401"], "HomeNpcBubbleTip shows NPC icon when dialogue red dot is active");

        services.FunctionLimited = true;
        bubbleSvc.RaiseHomeChanged();
        yield return null;
        yield return null;
        Require(!bubble.IsShow && !bubbleVisual.activeSelf && iconRenderer.sprite == null && bubbleSvc.UnloadedCount >= 1, "HomeNpcBubbleTip hides and unloads sprite when NPCDialogue is locked");

        Destroy(bubbleHost);
        Destroy(bubbleSvc.Sprites["UI/NPC/Npc_Icon/Map_11/npc_icon_8401"]);
        Destroy(tex);
        yield return null;
        Require(bubbleSvc.DialogueSubscribers == 0 && bubbleSvc.HomeSubscribers == 0, "Destroyed HomeNpcBubbleTip unsubscribes dialogue and home changes");
    }

    IEnumerator CheckBedHangUpControllers()
    {
        viewCamera.transform.position = new Vector3(0, 0, -10);
        viewCamera.orthographicSize = 4;
        services.OwnHome = true;
        services.WindowsClosed = true;
        services.PlayerValue.Owned = true;
        services.Disabled = false;
        services.NpcPanels.Clear();
        services.Calls.Clear();

        var tipSvc = new HangUpTipTestServices();
        var hangUpSvc = new HangUpFurnitureTestServices(tipSvc);
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        hangUpSvc.AwardSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

        var host = New("BedHangUpHost");
        host.SetActive(false);
        var bedCollider = Child(host, "BedCollider");
        bedCollider.transform.position = Vector3.zero;
        bedCollider.AddComponent<BoxCollider>();
        var states = Enumerable.Range(0, 4).Select(index => Child(host, "BedState" + index)).ToArray();
        var tip0 = Child(host, "Tip0");
        var tip1 = Child(host, "Tip1");
        var noticeRoot = Child(host, "BedNotice");
        var noticeText = Child(noticeRoot, "BedNoticeTxt").AddComponent<TextMeshPro>().gameObject.AddComponent<WorldMultiLanguageText>();
        var notice = noticeRoot.AddComponent<HomeNotice>();
        notice.Bind(noticeText, Switch<bool>(noticeRoot, new[] { new[] { Child(noticeRoot, "Shown") } }, new[] { 1 }), "", services);
        var iconRenderer = Child(host, "icon").AddComponent<SpriteRenderer>();
        var bed = host.AddComponent<BedFurniture>();
        var soBed = new SerializedObject(bed);
        var touchList = soBed.FindProperty("mTouchList");
        touchList.arraySize = 1;
        touchList.GetArrayElementAtIndex(0).objectReferenceValue = bedCollider;
        soBed.ApplyModifiedPropertiesWithoutUndo();
        bed.Bind(EFurniture.Bed,
            Switch<FurnitureBase.EFurnitureState>(host, states.Select(value => new[] { value }).ToArray(), new[] { 0, 1, 2, 3 }),
            notice, null, 0.2f, 1.0f, services, iconRenderer,
            Switch<int>(host, new[] { new[] { tip0 }, new[] { tip0, tip1 } }, new[] { 0, 1 }),
            hangUpSvc);
        input.Bind(bed);
        host.SetActive(true);
        yield return null;

        Require(iconRenderer.sprite == hangUpSvc.AwardSprite, "BedFurniture binds AwardIcon sprite from HangUpServices");
        Require(bed.TipLevel == -1 && !tip0.activeSelf && !tip1.activeSelf, "BedFurniture hides mRewardTipSwitcher at TipLevel -1 (actual=" + bed.TipLevel + ")");
        Require(!bed.CanReceiveAward(), "BedFurniture CanReceiveAward is false at TipLevel -1");
        yield return Click(bedCollider);
        Require(hangUpSvc.Popups.SequenceEqual(new[] { EFurniture.Bed }), "Clicking BedFurniture at TipLevel -1 opens Bed window");

        tipSvc.TestPlayer.AwardCount = 10;
        tipSvc.TestPlayer.RaiseHangUpDataChanged(4100);
        yield return null;
        Require(bed.TipLevel == 0 && tip0.activeSelf && !tip1.activeSelf, "BedFurniture switches mRewardTipSwitcher to level 0");
        Require(hangUpSvc.TipLevelUpdates.Last() == (EFurniture.Bed, 0), "BedFurniture reports TipLevel 0 update");
        Require(!bed.CanReceiveAward(), "CanReceiveAward stays false when quick-receive is disabled");
        yield return Click(bedCollider);
        Require(hangUpSvc.Popups.Count == 2, "Clicking BedFurniture without quick-receive opens Bed window instead of collecting");

        tipSvc.TestPlayer.AwardCount = 120;
        tipSvc.TestPlayer.QuickReceive = true;
        tipSvc.TestPlayer.RaiseHangUpDataChanged(4100);
        yield return null;
        Require(bed.TipLevel == 1 && tip0.activeSelf && tip1.activeSelf, "BedFurniture switches mRewardTipSwitcher to level 1");
        Require(bed.CanReceiveAward(), "CanReceiveAward is true when quick-receive is enabled and TipLevel >= 0");
        yield return Click(bedCollider);
        Require(hangUpSvc.Collections == 1 && hangUpSvc.AwardCollectedCalls == 1 && hangUpSvc.MergedAwards == 1 && hangUpSvc.PublishedCollections == 1,
            "Clicking BedFurniture with quick-receive collects, awards, merges/popups and publishes reward");
        tipSvc.TestPlayer.AwardCount = 0;
        tipSvc.TestPlayer.RaiseHangUpDataChanged(4100);
        yield return null;
        Require(bed.TipLevel == -1 && !tip0.activeSelf && !tip1.activeSelf, "BedFurniture resets mRewardTipSwitcher to -1 after collection");

        Destroy(host);
        Destroy(hangUpSvc.AwardSprite);
        Destroy(tex);
        yield return null;
        Require(tipSvc.TestPlayer.UnlockSubscribers == 0 && tipSvc.TestPlayer.DataChangedSubscribers == 0
            && tipSvc.TestPlayer.RepairSubscribers == 0 && tipSvc.TestPlayer.TimeSubscribers == 0
            && tipSvc.TestPlayer.LevelSubscribers == 0,
            "Destroyed BedFurniture disposes HomeHangUpTipController and releases all player subscriptions");
    }

    sealed class HangUpTipPlayerStub : Zjcs.Official.HomeHangUpTips.IHomeHangUpTipPlayer
    {
        public long AwardCount;
        public bool QuickReceive;
        public bool HasFurniture(EFurniture furniture) => true;
        public IReadOnlyDictionary<int, Zjcs.Official.HomeHangUpTips.HomeHangUpTipRecord> HangUpDataDict { get; } =
            new Dictionary<int, Zjcs.Official.HomeHangUpTips.HomeHangUpTipRecord> {
                { 4100, new Zjcs.Official.HomeHangUpTips.HomeHangUpTipRecord { IsUnlock = true } }
            };
        public long GetHangUpAwardCount(int id) => AwardCount;
        public long GetHangUpMaxCount(int id) => 1000;
        public double GetHangUpHourOutput(int id) => 100.0;
        public bool GetHangUpQuickReceiveState(EFurniture furniture) => QuickReceive;
        public int Level => 1;
        public bool CanGetExp() => false;
        public event Action<int> OnHangUpUnlock;
        public event Action<int> OnHangUpDataChanged;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<long> OnDebugTimeOffset;
        public event Action<int> OnLevelUpdate;
        public int UnlockSubscribers => OnHangUpUnlock?.GetInvocationList().Length ?? 0;
        public int DataChangedSubscribers => OnHangUpDataChanged?.GetInvocationList().Length ?? 0;
        public int RepairSubscribers => OnRepairFurniture?.GetInvocationList().Length ?? 0;
        public int TimeSubscribers => OnDebugTimeOffset?.GetInvocationList().Length ?? 0;
        public int LevelSubscribers => OnLevelUpdate?.GetInvocationList().Length ?? 0;
        public void RaiseHangUpDataChanged(int id) => OnHangUpDataChanged?.Invoke(id);
    }

    sealed class HangUpTipTestServices : Zjcs.Official.HomeHangUpTips.IHomeHangUpTipServices
    {
        public readonly HangUpTipPlayerStub TestPlayer = new HangUpTipPlayerStub();
        public HangUpTipTestServices()
        {
            Settings = new Zjcs.Official.HomeHangUpTips.HomeHangUpTipSettings(
                new List<Zjcs.Official.HomeHangUpTips.HomeHangUpTipSettings.HangUpTipSetting>
                {
                    new Zjcs.Official.HomeHangUpTips.HomeHangUpTipSettings.HangUpTipSetting
                    {
                        FurnitureList = new List<EFurniture> { EFurniture.Bed },
                        TipLevelList = new List<float> { 0.005f, 0.05f }
                    }
                },
                0.1f);
            RecordHangUpIdDict = new Dictionary<EFurniture, List<int>> {
                { EFurniture.Bed, new List<int> { 4100 } }
            };
        }
        public Zjcs.Official.HomeHangUpTips.IHomeHangUpTipPlayer MainPlayer => TestPlayer;
        public Zjcs.Official.HomeHangUpTips.IHomeHangUpTipPlayer PlayerData => TestPlayer;
        public bool InMyHome => true;
        public IReadOnlyDictionary<EFurniture, List<int>> RecordHangUpIdDict { get; }
        public Zjcs.Official.HomeHangUpTips.HomeHangUpTipSettings Settings { get; }
        public long GetLevelUpExp(int level) => 10000;
        public void SetTipState(EFurniture furniture, int level) { }
        public void Publish(Zjcs.Official.HomeSceneUiControls.HangUpTipLevelChangeData data) { }
        public IDisposable Wait(float seconds, Action completed) => new Subscription(null);
    }

    sealed class HangUpAwardMessageStub : IHomeHangUpAwardMessage
    {
        public IList DataList { get; } = new List<object> { "reward" };
        public object SourceData { get; } = "bed-collect";
    }

    sealed class HangUpFurnitureTestServices : IHomeHangUpFurnitureServices
    {
        public Sprite AwardSprite;
        public readonly List<EFurniture> Popups = new List<EFurniture>();
        public readonly List<(EFurniture furniture, int tipLevel)> TipLevelUpdates = new List<(EFurniture, int)>();
        public int Collections, AwardCollectedCalls, MergedAwards, PublishedCollections;
        public HangUpFurnitureTestServices(Zjcs.Official.HomeHangUpTips.IHomeHangUpTipServices tips) { Tips = tips; }
        public Zjcs.Official.HomeHangUpTips.IHomeHangUpTipServices Tips { get; }
        public Sprite LoadAwardSprite(EFurniture furniture) => AwardSprite;
        public void CollectHangUpRewards(EFurniture furniture, IReadOnlyList<int> hangUpIds, Action<IHomeHangUpAwardMessage, int> completed)
        {
            Collections++;
            completed?.Invoke(new HangUpAwardMessageStub(), 0);
        }
        public void AwardCollected(IList dataList, object sourceData) => AwardCollectedCalls++;
        public void MergeAndPopupAward(EFurniture furniture, IHomeHangUpAwardMessage response) => MergedAwards++;
        public void PublishHangUpCollect(EFurniture furniture, IReadOnlyList<int> hangUpIds) => PublishedCollections++;
        public void PopupFurnitureWindow(EFurniture furniture) => Popups.Add(furniture);
        public void OnTipLevelUpdated(EFurniture furniture, int tipLevel) => TipLevelUpdates.Add((furniture, tipLevel));
    }

    sealed class BubbleTipDotSystem : Zjcs.Official.ProfessionUI.IProfessionTipDotSystem
    {
        public bool IsShowTipDot => true;
        public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; }
        public Zjcs.Official.ProfessionUI.SafeAction OnTipDotSystemInited { get; set; } = new Zjcs.Official.ProfessionUI.SafeAction();
        public Zjcs.Official.ProfessionUI.SafeAction OnTipDotSystemPreClear { get; set; } = new Zjcs.Official.ProfessionUI.SafeAction();
        public Zjcs.Official.ProfessionUI.TipDotGraphType? GetGraphType(string graphName) => null;
    }

    sealed class BubblePlayerUi : Zjcs.Official.HomeHudUI.IHomePlayerUiServices
    {
        public bool HasPlayer => true;
        public bool ManagerAvailable => true;
        public UnityEngine.Events.UnityEvent OnPlayerUpdate { get; } = new UnityEngine.Events.UnityEvent();
        public Zjcs.Official.ProfessionUI.IProfessionTipDotSystem TipDotSystem { get; } = new BubbleTipDotSystem();
    }

    sealed class BubbleServicesImpl : IHomeNpcBubbleTipServices
    {
        public int ActiveNpcClassId;
        public int UnloadedCount;
        public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        public readonly List<Action> DialogueListeners = new List<Action>();
        public readonly List<Action> HomeListeners = new List<Action>();
        public int DialogueSubscribers => DialogueListeners.Count;
        public int HomeSubscribers => HomeListeners.Count;
        public void RaiseDialogueChanged() { foreach (var action in DialogueListeners.ToArray()) action(); }
        public void RaiseHomeChanged() { foreach (var action in HomeListeners.ToArray()) action(); }
        public IDisposable SubscribeDialogueChanged(Action action) { DialogueListeners.Add(action); return new Subscription(() => DialogueListeners.Remove(action)); }
        public IDisposable SubscribePostHomeChange(Action action) { HomeListeners.Add(action); return new Subscription(() => HomeListeners.Remove(action)); }
        public bool TryFindActiveDialogueNpcClassId(out int npcClassId) { npcClassId = ActiveNpcClassId; return npcClassId > 0; }
        public string GetNpcIconPath(int npcClassId) => npcClassId > 0 ? "UI/NPC/Npc_Icon/Map_11/npc_icon_" + npcClassId : null;
        public Sprite LoadNpcIcon(string iconPath) => Sprites.TryGetValue(iconPath, out var value) ? value : null;
        public void UnloadNpcIcon(string iconPath) { if (!string.IsNullOrEmpty(iconPath)) UnloadedCount++; }
    }

    IEnumerator Click(GameObject target)
    {
        Physics.SyncTransforms();
        pointer.Position = viewCamera.WorldToScreenPoint(target.transform.position);
        var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = pointer.Position }, hits);
        Require(hits.Count == 0, "Fixture pointer is not covered by UI: " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        Require(Physics.Raycast(viewCamera.ScreenPointToRay(pointer.Position), out var hitInfo, 1000) && hitInfo.collider.gameObject == target, "Fixture pointer hits the actual collider");
        pointer.LeftDown = true; pointer.LeftUp = false;
        input.Tick(); input.LateTick(); yield return null;
        pointer.LeftDown = false; pointer.LeftUp = true;
        input.Tick(); input.LateTick(); yield return null;
        pointer.LeftUp = false;
    }
    GameObject New(string name) { var value = new GameObject(name); roots.Add(value); return value; }
    static GameObject Child(GameObject parent, string name) { var value = new GameObject(name); value.transform.SetParent(parent.transform, false); return value; }
    static WorldStateSwitcher<T> Switch<T>(GameObject owner, GameObject[][] groups, int[] states) where T : struct, IConvertible
        => WorldStateSwitcher<T>.FromBindings(owner, new IWorldStateConfig[] { WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData {
            GoConfig = groups.Select((group, index) => new WorldStateGameObjectData.StateGameObjectKV { State = states[index], GameObjects = group.ToList() }).ToList() }) });
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("CHECK: " + message); }
    void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    sealed class Pointer : IHomeSceneClickPointer
    {
        public bool LeftDown { get; set; }
        public bool LeftUp { get; set; }
        public Vector2 Position { get; set; }
        public int Width => Screen.width;
        public int Height => Screen.height;
    }
    sealed class Condition : IHomeFurnitureCondition { public bool Met; public bool IsMet() => Met; }
    sealed class Notice : IHomeFurnitureConditionNotice
    {
        public bool Changed;
        public Action<string, object[]> OnNoticeTxtUpdate { get; set; }
        public void Refresh() => OnNoticeTxtUpdate?.Invoke("notice", new object[] { 9 });
        public bool OnUpdate(float deltaTime) { bool result = Changed; Changed = false; return result; }
        public string MessageTip() => "notice-tip";
    }
    sealed class Player : IHomeFurniturePlayer
    {
        public bool Owned, StoryStarted;
        public bool HasFurniture(EFurniture furniture) => Owned;
        public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => furniture.UnlockCondition.IsMet();
        public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => furniture.RepairNoticeCondition.IsMet();
        public bool HasStartedStory(string storyName) => StoryStarted;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<long> OnDebugTimeOffset;
        public int RepairSubscribers => OnRepairFurniture?.GetInvocationList().Length ?? 0;
        public int TimeSubscribers => OnDebugTimeOffset?.GetInvocationList().Length ?? 0;
        public void Repaired(EFurniture furniture) => OnRepairFurniture?.Invoke(furniture);
        public void TimeOffset() => OnDebugTimeOffset?.Invoke(1);
    }
    sealed class Subscription : IDisposable
    {
        Action dispose;
        public Subscription(Action dispose) => this.dispose = dispose;
        public void Dispose() { var action = dispose; dispose = null; action?.Invoke(); }
    }
    sealed class Services : IHomeFurnitureServices, IHomeSceneClickContext
    {
        public bool OwnHome = true, WindowsClosed = true, Disabled, FunctionLimited;
        public DateTime Now = new DateTime(2026, 11, 24, 12, 0, 0);
        public DateTime ResetDate = new DateTime(2026, 11, 24, 6, 0, 0);
        public int DailyRefreshes;
        public readonly List<int> NpcPanels = new List<int>();
        public readonly Condition Notice = new Condition(), Unlock = new Condition();
        public readonly List<(IHomeFurnitureCondition condition, Action action)> ConditionSubscriptions = new List<(IHomeFurnitureCondition, Action)>();
        public readonly List<Action> HomeChanges = new List<Action>();
        public readonly List<Func<HomeFurnitureAward, bool>> Awards = new List<Func<HomeFurnitureAward, bool>>();
        public readonly List<string> Calls = new List<string>();
        public readonly FurnitureInfo Info;
        public readonly Player PlayerValue;
        public Player DisplayedPlayer;
        public readonly Notice ConditionNotice = new Notice();
        public int ContextDisposed;
        public Services() { Info = new FurnitureInfo { RepairNoticeCondition = Notice, UnlockCondition = Unlock }; PlayerValue = new Player(); }
        public bool InMyHome => OwnHome;
        public bool AllNoTipWindowsClosed => WindowsClosed;
        public IHomeFurniturePlayer Player => PlayerValue;
        public IHomeFurniturePlayerData PlayerData => DisplayedPlayer ?? PlayerValue;
        public FurnitureInfo GetFurnitureInfo(EFurniture furniture) => Info;
        public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action)
        { var entry = (condition, action); ConditionSubscriptions.Add(entry); return new Subscription(() => ConditionSubscriptions.Remove(entry)); }
        public void TriggerCondition(IHomeFurnitureCondition condition) { foreach (var entry in ConditionSubscriptions.ToArray()) if (entry.condition == condition) entry.action(); }
        public IDisposable SubscribePostHomeChange(Action action) { HomeChanges.Add(action); return new Subscription(() => HomeChanges.Remove(action)); }
        public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) => Awards.Add(handler);
        public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) => Awards.Remove(handler);
        public bool CheckFunctionDisabled(EFurniture furniture) { Calls.Add("disabled:" + furniture); return Disabled; }
        public void ShowError(int textId) => Calls.Add("error:" + textId);
        public void PopupRepair(EFurniture furniture, bool isBuild) => Calls.Add("repair:" + furniture + ":" + isBuild);
        public void PopupPlayerBag() => Calls.Add("bag");
        public void PopupTreasurePanel() => Calls.Add("treasure");
        public void DoStoryAction(Action action) { Calls.Add("story-action"); action(); }
        sealed class Story : IHomeFurnitureStoryContext { }
        public IHomeFurnitureStoryContext CreateStoryContext(string owner) { Calls.Add("context:" + owner); return new Story(); }
        public void PlayStory(IHomeFurnitureStoryContext context, string storyName) => Calls.Add("play:" + storyName);
        public void DisposeStory(IHomeFurnitureStoryContext context) => ContextDisposed++;
        public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => ConditionNotice;
        public void SystemMessage(string message) => Calls.Add("message:" + message);
        public bool IsFunctionLimit(int functionType) => FunctionLimited;
        public void PopupNpcPanel(int defaultTab = 0) => NpcPanels.Add(defaultTab);
        public DateTime GetDateTime() => Now;
        public DateTime GetDailyResetTimeFromDate(DateTime dateTime) => ResetDate;
        public void RefreshDailyDialogues(bool invokeByPlayer) => DailyRefreshes++;
    }
}
