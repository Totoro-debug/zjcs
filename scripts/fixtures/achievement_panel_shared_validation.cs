using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

// Component regression fixture, installed only in an independent source project.
// Native prefab presentation and each production adapter require their own flow.
[InitializeOnLoad]
public static class AchievementPanelSharedValidation
{
    const string Pending = "Zjcs.SharedAchievement.Pending";
    static readonly List<string> checks = new List<string>();
    static AchievementPanelSharedValidation() { EditorApplication.playModeStateChanged += StateChanged; }
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        bool success = false;
        GameObject root = null;
        try
        {
            root = new GameObject("Achievement regression"); root.SetActive(false);
            var panel = root.AddComponent<AchievementPanelBase>();
            panel.EmptyStateSwitcher = Switch<bool>(root);
            var items = new List<AchievementPanelItem>();
            for (int i = 0; i < 5; i++) items.Add(Item(root.transform));
            Set(panel, "mPanelItemList", items);
            Set(panel, "mIsShowProcessingItem", true);
            root.SetActive(true);
            var source = new Source();
            panel.InitDataKey(new AchievementPanelKey(66, 16), source);
            Check(items.Select(item => item.StateSwitcher.State).SequenceEqual(new[] {
                OfficialAchievementState.Completed, OfficialAchievementState.Completed,
                OfficialAchievementState.Processing, OfficialAchievementState.Processing,
                OfficialAchievementState.Processing }), "regular initial filters and state ordering");
            Check(items[0].TitleText.myKey == "row-16-1" && items[1].TitleText.myKey == "row-16-2",
                "received row excluded before completion tests");
            var entry = root.AddComponent<AchievementPanel>();
            entry.AchievementPanelBase = panel;
            int beforeConfigure = source.Created;
            entry.Configure(source);
            Check(source.Created == beforeConfigure, "None type waits for a runtime data key");
            entry.SetDataKey(new AchievementPanelKey(66, 16));
            Check(source.Created == beforeConfigure + 5 && source.Subscribers == 5, "runtime data key refreshes once and replaces row subscriptions");
            int icons = source.Created;
            source.Current = 4; source.Raise(99, "ALL");
            Check(items[0].ProgressText.Text == "2/2" && source.Created == icons, "unrelated blackboard ignored");
            source.Raise(45, "OTHER");
            Check(items[0].ProgressText.Text == "2/2", "blackboard parameter identity");
            source.Raise(45, "ALL");
            Check(items[0].ProgressText.Text == "4/2" && source.Created == icons
                && items[2].StateSwitcher.State == OfficialAchievementState.Processing,
                "matching blackboard refreshes progress without rebuilding states or icons");
            items[0].GetRewardBtn.onClick.Invoke();
            Check(source.LastClaim == (16, -1, false) && items[0].TitleText.myKey == "row-16-3"
                && items[0].StateSwitcher.State == OfficialAchievementState.Processing,
                "group minus-one claim callback removes captured completed rows");
            root.SetActive(false);
            Check(source.Subscribers == 0 && source.Created == source.Recycled, "disable releases progress subscriptions and icons");
            root.SetActive(true);
            Check(source.Subscribers == items.Count(item => item.gameObject.activeSelf), "reenable restores one subscription per row");
            Set(panel, "mIsShowReceivedItem", true);
            panel.InitDataKey(new AchievementPanelKey(66, 16), source);
            Check(items[0].StateSwitcher.State == OfficialAchievementState.Processing, "processing sorted ahead of received rows");
            var range = AchievementPanelBase.GetAchievementStateSearchRange(new AchievementPanelGroup(10000000, 1000, 2000, _ => null), 5);
            Check(range == (995, 2005), "cyclic search window uses original recorded and available bounds");
            source.Groups = true; source.Current = 2; source.ResetClaims();
            Set(panel, "mIsShowReceivedItem", false);
            panel.InitDataKey(new AchievementPanelKey(66, 16), source);
            Check(items[0].TitleText.myKey == "row-15-1" && items[1].TitleText.myKey == "row-15-2"
                && items[2].TitleText.myKey == "row-16-1", "same-type groups sort by group then index");
            root.SetActive(false);
            var profession = new Profession();
            source.Groups = false;
            entry.AchievementType = 56;
            entry.Init(profession);
            Check(profession.Subscribers == 0, "inactive profession entry waits for enable");
            root.SetActive(true);
            CompareProfession(items, profession);
            profession.Level = 7; profession.Notify(); CompareProfession(items, profession);
            items[0].GetRewardBtn.onClick.Invoke();
            CompareProfession(items, profession);
            Check(profession.Claims == 1 && items[0].StateSwitcher.State == OfficialAchievementState.Processing,
                "profession synchronous save event and callback rebuild preserve next milestones");
            root.SetActive(false);
            Check(profession.Subscribers == 0 && profession.Created == profession.Recycled, "profession lifecycle balances resources");
            success = true;
        }
        catch (Exception ex) { Debug.LogException(ex); checks.Add(ex.ToString()); }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            string output = Environment.GetEnvironmentVariable("ZJCS_SHARED_ACHIEVEMENT_OUTPUT");
            File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(new { success, checks }, Formatting.Indented));
            SessionState.SetBool(Pending, false);
            SessionState.SetInt(Pending + ".exit", success ? 0 : 1);
            EditorApplication.playModeStateChanged += Exit;
            EditorApplication.isPlaying = false;
        }
    }
    static void Exit(PlayModeStateChange state)
    { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(SessionState.GetInt(Pending + ".exit", 1)); }
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); checks.Add("PASS " + name); }
    static WorldStateSwitcher<T> Switch<T>(GameObject owner) where T : struct, IConvertible
        => WorldStateSwitcher<T>.FromBindings(owner, Array.Empty<IWorldStateConfig>());
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static WorldMultiLanguageText Text(Transform parent, string key)
    {
        var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
        go.AddComponent<TextMeshProUGUI>(); var text = go.AddComponent<WorldMultiLanguageText>();
        text.myKey = key; text.Localizer = value => value; return text;
    }
    static AchievementPanelItem Item(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform)); go.SetActive(false); go.transform.SetParent(parent, false);
        var item = go.AddComponent<AchievementPanelItem>();
        item.TitleText = Text(go.transform, "title"); item.ProgressText = Text(go.transform, "{0}/{1}");
        item.RewardSlot = go.transform; item.GetRewardBtn = go.AddComponent<Button>();
        item.StateSwitcher = Switch<OfficialAchievementState>(go);
        Set(item, "mTipDotStateSwitcher", Switch<bool>(go)); return item;
    }
    sealed class Icon : IProfessionRewardIcon { public void SetCount(long count) { } public void SetClickPopupInfoDialog() { } }
    sealed class Source : IAchievementPanelBlackboardSource
    {
        public int Current = 2, Created, Recycled, Subscribers;
        public bool Groups;
        readonly HashSet<int> claimed = new HashSet<int> { 0 };
        public void ResetClaims() { claimed.Clear(); claimed.Add(0); }
        Action<AchievementPanelBlackboardKey> changed;
        public event Action<AchievementPanelBlackboardKey> BlackboardChanged
        { add { changed += value; Subscribers++; } remove { changed -= value; Subscribers--; } }
        public void Raise(int type, string parameter) => changed?.Invoke(new AchievementPanelBlackboardKey(type, parameter));
        public int AchievementPanelShowCount => 5;
        public bool AchievementGroupLimited => false;
        public (int, int, bool) LastClaim;
        public IReadOnlyList<AchievementPanelGroup> GetGroups(AchievementPanelKey key, bool same)
            => (Groups ? new[] { 16, 15 } : new[] { 16 }).Select(group => new AchievementPanelGroup(12,
                claimed.Max(), Current, index => new AchievementPanelEntry { Group = group, Index = index,
                    AlreadyGotReward = claimed.Contains(index), CanGetReward = index <= Current && !claimed.Contains(index),
                    Content = ("row-" + group + "-" + index, null), Title = ("group-" + group, null),
                    Progress = (index + 1, Current), ProgressKey = new AchievementPanelBlackboardKey(45, "ALL"),
                    Reward = new Dictionary<int, long> { [1] = 1 } })).ToArray();
        public object Compact(long value) => value.ToString();
        public IProfessionRewardIcon GetIcon(int item, Transform slot) { Created++; return new Icon(); }
        public void RecycleIcon(IProfessionRewardIcon icon) { Recycled++; }
        public void ShowSystemMessage(string key) { throw new Exception(key); }
        public void Claim(int group, int count, bool same, Action success)
        { LastClaim = (group, count, same); for (int i = 0; i <= Current; i++) claimed.Add(i); success(); }
    }
    sealed class Profession : IProfessionAchievementData
    {
        public OfficialProfessionAchievementRules Rules { get; }
        public int Level = 5, Created, Recycled, Claims, Subscribers;
        public int TotalLevel => Level;
        public ISet<string> Recorded { get; } = new HashSet<string>();
        public int AchievementPanelShowCount => 5;
        public bool AchievementGroupLimited => false;
        Action changed;
        public event Action Changed { add { changed += value; Subscribers++; } remove { changed -= value; Subscribers--; } }
        public void Notify() => changed?.Invoke();
        public Profession()
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SHARED_ACHIEVEMENT_RULES");
            Rules = new OfficialProfessionAchievementRules(((List<object>)Convert(JObject.Parse(File.ReadAllText(path))["achievement"]))
                .Cast<Dictionary<string, object>>());
        }
        static object Convert(JToken token) => token is JObject obj ? obj.Properties().ToDictionary(p => p.Name, p => Convert(p.Value))
            : token is JArray array ? array.Select(Convert).ToList() : (token as JValue)?.Value;
        public bool CanGetReward(OfficialProfessionAchievement info) => Level >= info.RequiredLevel && !AlreadyGotReward(info);
        public bool AlreadyGotReward(OfficialProfessionAchievement info) => Recorded.Contains(info.EventId);
        public bool ClaimGroup(int group, int count, bool same)
        {
            if (group != 10 || count != -1 || same) throw new Exception("profession request changed");
            Claims++; foreach (var row in Rules.CanClaim(Level, Recorded)) Recorded.Add(row.EventId);
            Notify(); return true;
        }
        public void ShowSystemMessage(string key) { throw new Exception(key); }
        public IProfessionRewardIcon GetIcon(int item, Transform slot) { Created++; return new Icon(); }
        public void RecycleIcon(IProfessionRewardIcon icon) { Recycled++; }
    }
    static void CompareProfession(List<AchievementPanelItem> items, Profession data)
    {
        var expected = data.Rules.VisibleWithStates(data.Level, data.Recorded, 5);
        Check(items.Count(row => row.gameObject.activeSelf) == expected.Count, "profession visible row count at " + data.Level);
        for (int i = 0; i < expected.Count; i++)
            Check(items[i].StateSwitcher.State == expected[i].State && items[i].TitleText.myKey == "ui_achievement_content_TotalProfessionLevel"
                && items[i].ProgressText.Text == OfficialPropFormat.Compact(data.Level) + "/" + OfficialPropFormat.Compact(expected[i].Info.RequiredLevel),
                "profession milestone state and progress " + data.Level + ":" + i);
    }
}
