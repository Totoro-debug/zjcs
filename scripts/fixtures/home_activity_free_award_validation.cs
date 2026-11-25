using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeActivityTipDots;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

[InitializeOnLoad]
public static class HomeActivityFreeAwardValidation
{
    private const string Active = "Zjcs.HomeActivityFreeAwardValidation";
    static HomeActivityFreeAwardValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("Original activity free award validation").AddComponent<Runner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("An owned independent Unity project is required.");
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_ACTIVITY_FREE_AWARD_PASSED"); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    private sealed class Manager : ITipDotNodeManager
    {
        public int Dirty;
        public Action OnAddScopeDataCollector { get; set; }
        public Action OnRemoveScopeDataCollector { get; set; }
        public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
        public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
        public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty++;
        public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) { }
    }
    private sealed class Services : IHomeActivityFreeAwardServices
    {
        public bool HasPlayer => true;
        public bool HasActivityManager => true;
        public bool GiftExists = true, ActivityExists = true, Reward;
        public DateTime End;
        public event Action<int> OnFreeAwardDataAdd;
        public event Action<int> OnGetFreeReward;
        public event Action<long> OnDebugTimeOffset;
        public event Action<HomeTipActivity> OnActivityOpen;
        public event Action<HomeTipActivity> OnActivityClose;
        public event Action<ActivityType> OnActivitiesUpdate;
        public int Subscribers => (OnFreeAwardDataAdd?.GetInvocationList().Length ?? 0) + (OnGetFreeReward?.GetInvocationList().Length ?? 0)
            + (OnDebugTimeOffset?.GetInvocationList().Length ?? 0) + (OnActivityOpen?.GetInvocationList().Length ?? 0)
            + (OnActivityClose?.GetInvocationList().Length ?? 0) + (OnActivitiesUpdate?.GetInvocationList().Length ?? 0);
        public bool TryResolveGiftId(ActivityType type, int id, out int gift) { gift = GiftExists ? 42 : 0; return GiftExists; }
        public bool TryGetFreeAwardEndTime(int gift, out DateTime end) { end = End; return End != default; }
        public bool HasActivity(ActivityType type, int id) => ActivityExists;
        public bool CanGetFreeReward(int gift, DateTime now) => Reward && gift == 42;
        public DateTime GetDateTime() => DateTime.UtcNow;
        public void Add(int id) => OnFreeAwardDataAdd?.Invoke(id);
        public void Get(int id) => OnGetFreeReward?.Invoke(id);
        public void DebugTime() => OnDebugTimeOffset?.Invoke(0);
        public void Open(ActivityType type, int id) => OnActivityOpen?.Invoke(new HomeTipActivity(type, id));
        public void Close(ActivityType type, int id) => OnActivityClose?.Invoke(new HomeTipActivity(type, id));
        public void Update(ActivityType type) => OnActivitiesUpdate?.Invoke(type);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            Exception error = null;
            var steps = Check();
            while (true)
            {
                bool more;
                try { more = steps.MoveNext(); } catch (Exception ex) { error = ex; break; }
                if (!more) break;
                yield return steps.Current;
            }
            Finish(error);
        }
        private IEnumerator Check()
        {
            var services = new Services(); var manager = new Manager();
            var node = new TipDotFreeAwardConditionNode { Services = services, NodeManager = manager };
            Require(!node.CheckShowTip(), "No instance info must hide the tip.");
            node.NodeKey = new TipDotNodeKey("FortuneTree", "Activity", new ActivityNodeInstanceInfo(9));
            services.GiftExists = false; Require(!node.CheckShowTip(), "Missing gift must hide the tip.");
            services.GiftExists = true; services.ActivityExists = false; Require(!node.CheckShowTip(), "Closed activity must hide the tip.");
            services.ActivityExists = true; Require(!node.CheckShowTip(), "Unavailable reward must hide the tip.");
            services.Reward = true; Require(node.CheckShowTip(), "The original active activity and reward gate must show the tip.");
            var clone = (TipDotFreeAwardConditionNode)node.Clone(); Require(clone.ActivityType == ActivityType.PrePay, "Clone must retain the original activity type.");
            services.End = DateTime.UtcNow.AddMilliseconds(350);
            node.Init(); node.IsActive = true; manager.Dirty = 0;
            Require(services.Subscribers == 6, "All original event listeners must be installed.");
            services.Add(99); services.Get(99); services.Open(ActivityType.PrePay, 8); services.Close(ActivityType.BattlePass, 9); services.Update(ActivityType.BattlePass);
            Require(manager.Dirty == 0, "Unrelated gifts and activities must not dirty this node.");
            services.Add(42); services.Get(42); services.Open(ActivityType.PrePay, 9); services.Close(ActivityType.PrePay, 9); services.Update(ActivityType.PrePay);
            Require(manager.Dirty == 5, "Matching gift and activity events must dirty the node.");
            manager.Dirty = 0;
            yield return new WaitForSecondsRealtime(0.75f);
            Require(manager.Dirty > 0, "The original timer must dirty the node when the reward expires.");
            manager.Dirty = 0; services.End = DateTime.UtcNow.AddSeconds(-1); services.DebugTime();
            Require(manager.Dirty == 2, "A debug clock change rechecks the expired timer and dirties the node.");
            node.Release(); Require(services.Subscribers == 0, "Release must remove every original event listener.");
        }
    }
}
