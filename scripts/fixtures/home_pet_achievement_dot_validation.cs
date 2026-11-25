using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePetGachaUI;
using Zjcs.Official.ProfessionUI;

[InitializeOnLoad]
public static class HomePetAchievementDotValidation
{
    private const string Active = "Zjcs.HomePetAchievementDotValidation";
    static HomePetAchievementDotValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("Original pet achievement indicator validation").AddComponent<Runner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("An owned independent Unity project is required.");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_PET_ACHIEVEMENT_DOT_PASSED"); else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    private sealed class Runner : MonoBehaviour
    {
        private sealed class Tips : IProfessionTipDotSystem
        {
            public bool IsShowTipDot => true;
            public bool TempLimitShowTip => false;
            public Action OnTipDotShowChanged { get; set; }
            public SafeAction OnTipDotSystemInited { get; set; }
            public SafeAction OnTipDotSystemPreClear { get; set; }
            public TipDotGraphType? GetGraphType(string name) => null;
        }
        private sealed class PlayerUi : IHomePlayerUiServices
        {
            public bool HasPlayer => true;
            public bool ManagerAvailable => true;
            public UnityEvent OnPlayerUpdate { get; } = new UnityEvent();
            public IProfessionTipDotSystem TipDotSystem { get; } = new Tips();
        }
        private sealed class Key
        {
            public int Type, Group;
            public string Id;
        }
        private sealed class Subscription : IDisposable
        {
            private readonly Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() => dispose();
        }
        private sealed class Services : IHomeAchievementRedDotServices
        {
            public bool HasPlayer => true;
            public bool GroupLimited;
            public readonly HashSet<string> Rewards = new HashSet<string>();
            public Action Changed;
            public event Action<int> OnRewardRecorded;
            public event Action<object> OnAchievementUpdated;
            public event Action OnRewardsRecorded;
            public int UpdateSubscribers => OnAchievementUpdated?.GetInvocationList().Length ?? 0;
            public object CreateKey(int type, string json) => new Key { Type = type, Group = type == 65 ? 42 : 99, Id = json };
            public bool ShowAllForType(object key) => ((Key)key).Type == 66;
            public IEnumerable<object> GetKeysOfSameType(object key)
                => new[] { key, CreateKey(66, "third") };
            public bool KeysEqual(object left, object right) => ((Key)left).Type == ((Key)right).Type && ((Key)left).Id == ((Key)right).Id;
            public int GroupId(object key) => ((Key)key).Group;
            public List<int> GetAllAchievementGroupIds() => new List<int> { 42 };
            public bool IsAchievementGroupLimited() => GroupLimited;
            public bool HasAvailableReward(object key) => Rewards.Contains(((Key)key).Id);
            public bool HasAvailableReward(List<object> keys) => keys.Exists(HasAvailableReward);
            public IDisposable SubscribeAchievementGroupLimit(Action changed) { Changed = changed; return new Subscription(() => Changed = null); }
            public void Update(object key) => OnAchievementUpdated?.Invoke(key);
            public void Record(int group) => OnRewardRecorded?.Invoke(group);
            public void RecordBatch() => OnRewardsRecorded?.Invoke();
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private IEnumerator Start()
        {
            Exception error = null;
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception ex) { error = ex; break; }
                yield return current;
            }
            Finish(error);
        }
        private IEnumerator Check()
        {
            var owner = new GameObject("Original achievement indicator"); owner.SetActive(false);
            var dot = new GameObject("Original red dot");
            var service = new Services();
            var component = owner.AddComponent<AchievementRedDotItem>();
            component.PlayerServices = new PlayerUi(); component.AchievementServices = service; component.myRedDotObj = dot;
            component.AchievementTypeAndParamList = new List<AchievementTypeAndParamItem> {
                new AchievementTypeAndParamItem { AchievementType = 65, JsonParam = "first" },
                new AchievementTypeAndParamItem { AchievementType = 66, JsonParam = "second" }
            };
            service.Rewards.Add("first"); owner.SetActive(true); yield return null; yield return null;
            Require(!component.ShowRedDot() && !dot.activeSelf, "An achievement owned by a configured group is excluded when group limits are open.");
            service.Rewards.Add("second"); service.Update(service.CreateKey(66, "different"));
            Require(!dot.activeSelf, "A different achievement key must not refresh this indicator.");
            service.Update(service.CreateKey(66, "second"));
            Require(dot.activeSelf, "A matching achievement update refreshes the original indicator immediately.");
            service.Rewards.Remove("second"); service.RecordBatch();
            Require(!dot.activeSelf, "Batch reward claims refresh the indicator.");
            service.Rewards.Add("third"); service.Update(service.CreateKey(66, "third"));
            Require(dot.activeSelf, "The official same-type expansion retains every returned key.");
            service.Rewards.Remove("third"); service.RecordBatch();
            service.GroupLimited = true; service.Changed(); yield return null;
            Require(dot.activeSelf, "A limited achievement-group function uses the original all-keys branch.");
            service.Rewards.Clear(); service.Record(42);
            Require(!dot.activeSelf, "Recording a reward refreshes even for a different group.");
            owner.SetActive(false); yield return null;
            Require(service.UpdateSubscribers == 0 && service.Changed == null, "Disable releases achievement and condition subscriptions.");
            Destroy(owner); Destroy(dot);
        }
    }
}
