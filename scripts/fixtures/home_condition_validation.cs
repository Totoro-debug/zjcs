using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

public static class HomeConditionValidation
{
    private sealed class Player : IHomeConditionPlayer
    {
        public int BaseLevel { get; set; }
        public int SubRank { get; set; }
        public int ProfessionRank, StoryCount, StageCount;
        public bool Goddess, ThrowMap, ThrowInventory;
        public readonly Dictionary<int,long> Items = new Dictionary<int,long>();
        public readonly List<HomeConditionItem> Inventory = new List<HomeConditionItem>();
        public readonly HashSet<int> Npcs = new HashSet<int>(), Maps = new HashSet<int>();
        public long GetItemAmount(int id)=>Items.TryGetValue(id,out var amount) ? amount : 0;
        public bool IsMapAccessible(int id)=>ThrowMap ? throw new Exception("Unexpected map evaluation") : Maps.Contains(id);
        public bool RegionIsActived(int id,int region)=>false;
        public bool HasNpc(int id)=>Npcs.Contains(id);
        public int GetHighestUnlockedProfessionRank()=>ProfessionRank;
        public IEnumerable<HomeConditionItem> GetInventoryItems()=>ThrowInventory ? throw new Exception("Unexpected inventory evaluation") : Inventory;
        public int GetStoryCount(string key)=>StoryCount;
        public bool IsAnyGoddessActived()=>Goddess;
        public int GetStageCount(int id)=>StageCount;
    }

    private static object Value(JToken token)
    {
        if (token is JObject obj) return obj.Properties().ToDictionary(p=>p.Name,p=>Value(p.Value));
        if (token is JArray list) return list.Select(Value).ToList();
        return ((JValue)token).Value;
    }
    private static Dictionary<string,object> Node(string type,params object[] fields)
    {
        var node=new Dictionary<string,object> { ["_type"]=type };
        for (int i=0;i<fields.Length;i+=2) node.Add((string)fields[i],fields[i+1]);
        return node;
    }
    private static Dictionary<string,List<Dictionary<string,object>>> Tables(string file)
    {
        var data=(Dictionary<string,object>)Value(JObject.Parse(File.ReadAllText(file)));
        return ((Dictionary<string,object>)data["tables"]).Where(pair=>pair.Value is List<object>).ToDictionary(pair=>pair.Key,
            pair=>((List<object>)pair.Value).Cast<Dictionary<string,object>>().ToList());
    }
    private static void Require(bool result,string message) { if (!result) throw new Exception(message); }

    public static int Main(string[] args)
    {
        try { Check(args[0],args[1]); Console.WriteLine("HOME_CURRENT_CONDITIONS_PASSED"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

#if UNITY_EDITOR
    private const string PlayRequest="Zjcs.HomeConditions.ValidationPlayRequest";
    [UnityEditor.InitializeOnLoadMethod]
    private static void Hook()
    {
        UnityEditor.EditorApplication.playModeStateChanged-=PlayState;
        UnityEditor.EditorApplication.playModeStateChanged+=PlayState;
    }
    public static void RunPlay()
    {
        GuardOwnedProject();
        UnityEngine.AudioListener.volume=0;
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        UnityEditor.SessionState.SetBool(PlayRequest,true);
        UnityEditor.EditorApplication.isPlaying=true;
    }
    private static void PlayState(UnityEditor.PlayModeStateChange state)
    {
        if (state!=UnityEditor.PlayModeStateChange.EnteredPlayMode || !UnityEditor.SessionState.GetBool(PlayRequest,false)) return;
        UnityEditor.SessionState.SetBool(PlayRequest,false);
        UnityEditor.EditorApplication.delayCall+=Run;
    }
    private static void GuardOwnedProject()
    {
        string work=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Require(!string.IsNullOrEmpty(work) && Path.GetFullPath(UnityEngine.Application.dataPath)
            .StartsWith(Path.GetFullPath(work)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Owned validation project");
    }
    public static void Run()
    {
        try
        {
            UnityEngine.AudioListener.volume=0;
            string repo=Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),work=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            GuardOwnedProject();
            var binding=Zjcs.Offline.HomeUI.OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            Require(binding.configuration!=null && binding.Rows("conditions").Count==4496
                && binding.Rows("furniture").Count==13 && binding.Rows("hang_up").Count==12
                && binding.Record("home_decoration").Count==3,"Explicit current configuration resource binding");
            var partners=Tables(Path.Combine(repo,"unity/Assets/Official/Partners/partner-rules.json"));
            var rules=binding.CreateConditions(new PartnerConditionRules(name=>partners[name]));
            Require(rules.IsMet(Node("NoneCondition"),new Player()),"Resource-loaded original predicate");
            int result=Main(new[] { repo,work });
            if (result==0)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Debug.Log("HOME_CURRENT_CONDITIONS_PLAY_PASSED");
                else { Zjcs.Editor.ProjectTools.Validate(); UnityEngine.Debug.Log("HOME_CURRENT_CONDITIONS_UNITY_PASSED"); }
            }
            UnityEditor.EditorApplication.Exit(result);
        }
        catch (Exception error) { UnityEngine.Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
    }
#endif

    private static void Check(string repo,string work)
    {
        var home=Tables(Path.Combine(work,"home-rules-current.json"));
        var partner=Tables(Path.Combine(repo,"unity/Assets/Official/Partners/partner-rules.json"));
        var partnerRules=new PartnerConditionRules(name=>partner[name]);
        var rules=new HomeConditionRules(home["conditions"],partnerRules);
        var player=new Player();
        player.ThrowInventory=true;
        Require(rules.IsMet(Node("NoneCondition"),player) && !rules.IsMet(Node("AlwaysCondition"),player),"Original None/Always");
        player.ThrowInventory=false;
        Require(rules.IsMet(Node("HasItemTypeCondition","ItemType",10,"Count",0),player),"Zero quantity is already satisfied");
        var item=Node("HasItemTypeCondition","ItemType",10,"Count",7);
        player.Inventory.Add(new HomeConditionItem(10,3));
        player.Inventory.Add(new HomeConditionItem(10,4));
        player.Inventory.Add(new HomeConditionItem(11,99));
        Require(rules.IsMet(item,player),"Sum every stack of exactly the requested item type");
        player.Inventory.RemoveAt(1);
        Require(!rules.IsMet(item,player),"Player changes do not leave cached inventory totals");
        player.Inventory.Clear(); player.Inventory.Add(new HomeConditionItem(10,3000000000L));
        Require(rules.IsMet(Node("HasItemTypeCondition","ItemType",10,"Count",int.MaxValue),player),"Inventory sum retains 64-bit quantities");
        player.Inventory.Clear();

        player.BaseLevel=38;
        Require(rules.IsMet(Node("HasManualMeetNpcCondition"),player),"Meetable manual partners qualify before ownership");
        player.BaseLevel=0;
        Require(!rules.IsMet(Node("HasManualMeetNpcCondition"),player),"No met or meetable manual partner");
        player.Npcs.Add(8404);
        Require(rules.IsMet(Node("HasManualMeetNpcCondition"),player),"Met trader qualifies");
        player.Npcs.Clear(); player.Npcs.Add(3601);
        Require(!rules.IsMet(Node("HasManualMeetNpcCondition"),player),"Exploration bosses are excluded");

        player.SubRank=(int)HomeConditionRules.SubRank.Bronze1;
        Require(rules.IsMet(Node("SubRankCondition","SubRank",player.SubRank,"Op","="),player),"Personal subrank identity");
        Require(rules.IsMet(Node("RankCondition","Rank",(int)HomeConditionRules.Rank.Bronze,"Op","="),player),"Original subrank-to-rank mapping");
        Require(!rules.IsMet(Node("RankCondition","Rank",(int)HomeConditionRules.Rank.Blackiron,"Op","="),player),"Exact rank comparison");
        player.SubRank=(int)HomeConditionRules.SubRank.Bronze3;
        Require(rules.IsMet(Node("RankCondition","Rank",(int)HomeConditionRules.Rank.Bronze,"Op","="),player),"All three subranks share one rank");
        player.SubRank=999;
        Require(rules.IsMet(Node("RankCondition","Rank",0,"Op","="),player),"Original unrecognized subrank fallback");
        player.ProfessionRank=3;
        Require(rules.IsMet(Node("MaxProfessionRankCondition","Rank",3,"Op",">="),player),"Highest unlocked profession");
        player.ProfessionRank=2;
        Require(!rules.IsMet(Node("MaxProfessionRankCondition","Rank",3,"Op",">="),player),"Profession threshold");
        player.StoryCount=2;
        Require(rules.IsMet(Node("StoryCountCondition","StoryKey","FirstMeetWeiLan","Count",1,"Op","=="),player),"Original unknown == uses >= fallback");
        Require(!rules.IsMet(Node("StoryCountCondition","StoryKey","FirstMeetWeiLan","Count",1,"Op","="),player),"Original equals operator is a single =");
        Require(!rules.IsMet(Node("AnyMapGoddessActivedCondition"),player),"No activated goddess");
        player.Goddess=true;
        Require(rules.IsMet(Node("AnyMapGoddessActivedCondition"),player),"Actual goddess activation");
        var stage=Node("StagePassCondition","StageId",1094,"Op",">=");
        Require(!rules.IsMet(stage,player),"Uncleared stage"); player.StageCount=1;
        Require(rules.IsMet(stage,player),"Cleared stage");

        var rows=new List<Dictionary<string,object>>();
        Action<int,Dictionary<string,object>> add=(id,node)=>rows.Add(new Dictionary<string,object> { ["ClassId"]=id,["Condition"]=node });
        add(1,Node("AlwaysCondition")); add(2,Node("MapUnlockCondition","MapClassId",11)); add(3,Node("NoneCondition"));
        var composite=new HomeConditionRules(rows,partnerRules);
        player.ThrowMap=true; player.ThrowInventory=true;
        Require(!composite.IsMet(Node("CompositeAndCondition","Ids",new List<object> { 1,2 }),player),"AND short circuit/order");
        Require(composite.IsMet(Node("CompositeOrCondition","Ids",new List<object> { 3,2 }),player),"OR short circuit/order");
        Require(composite.IsMet(Node("CompositeAndCondition","Ids",null),player)
            && !composite.IsMet(Node("CompositeOrCondition","Ids",null),player),"Original empty composites");
        bool missing=false;
        try { composite.IsMet(Node("CompositeAndCondition","Ids",new List<object> { 999 }),player); }
        catch (NullReferenceException) { missing=true; }
        Require(missing,"Missing original reference remains a failure");
        player.ThrowMap=false; player.ThrowInventory=false;

        int evaluated=0;
        foreach (string name in new[] { "furniture","hang_up","hang_up_reward","hang_up_upgrade_material","home_level","fast_hang_up" })
            foreach (var row in home[name])
                foreach (var node in row.Values.OfType<Dictionary<string,object>>().Where(value=>value.ContainsKey("_type")))
                { rules.IsMet(node,player); evaluated++; }
        Require(evaluated==57,"Full selected facility/income condition rows");
        Console.WriteLine("Evaluated selected home conditions: "+evaluated);
    }
}
