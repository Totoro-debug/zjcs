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
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
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

public sealed class HomeFurnitureControllerChecks : MonoBehaviour
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
        int clickCallbacks = 0; actor.OnClickAction.AddListener(() => clickCallbacks++);
        root.SetActive(true);
        yield return null;
        Require(actor.GetState() == FurnitureBase.EFurnitureState.Locked && states[0].activeSelf, "Initial original locked state");
        Require(services.ConditionSubscriptions.Count == 2 && services.HomeChanges.Count == 1 && services.Awards.Count == 1, "Original start subscriptions");
        yield return Click(target);
        Require(clickCallbacks == 1, "Native pointer dispatcher reached the actor callback");
        Require(services.Calls.Count == 0, "Locked click has no functional action");

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

        Destroy(root); yield return null;
        Require(services.ConditionSubscriptions.Count == 0 && services.HomeChanges.Count == 0 && services.Awards.Count == 0, "Destroy disposes original subscriptions and award handler");
        Require(services.PlayerValue.RepairSubscribers == 0 && services.PlayerValue.TimeSubscribers == 0 && services.ContextDisposed == 1, "Destroy releases player callbacks and reused story context");
        Debug.Log("Furniture controller assertions completed.");
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
        public bool OwnHome = true, WindowsClosed = true, Disabled;
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
        public void DoStoryAction(Action action) { Calls.Add("story-action"); action(); }
        sealed class Story : IHomeFurnitureStoryContext { }
        public IHomeFurnitureStoryContext CreateStoryContext(string owner) { Calls.Add("context:" + owner); return new Story(); }
        public void PlayStory(IHomeFurnitureStoryContext context, string storyName) => Calls.Add("play:" + storyName);
        public void DisposeStory(IHomeFurnitureStoryContext context) => ContextDisposed++;
        public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition) => ConditionNotice;
        public void SystemMessage(string message) => Calls.Add("message:" + message);
    }
}
