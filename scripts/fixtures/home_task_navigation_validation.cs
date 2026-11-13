using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeTaskUI;
using Zjcs.Official.WorldUI;
using TaskUI=Zjcs.Official.HomeTaskUI;

public sealed partial class HomeCurrencyWindowsNativeRunner
{
    TaskJumpHost rowNavigationHost;
    TaskUI.IHomeTaskTraceFactory taskRowTraceFactory;
    void ConfigureTaskRowNavigation()
    {
        taskRowNavigationFactory=data=>{
            rowNavigationHost=new TaskJumpHost(this,data);
            return new OfflineHomeTaskNavigation(data,rowNavigationHost,_=>{},taskRowTraceFactory);
        };
        taskRowNavigationVerifier=VerifyNativeTaskJumpPointer;
    }
    IEnumerator VerifyNativeTaskJumpPointer(TaskUI.TaskListItem row)
    {
        var jump=Field<UnityEngine.UI.Button>(row,"mJumpBtn");
        Require(jump.gameObject.activeInHierarchy,"actual original task row exposes original jump action");
        Click(jump);yield return WaitConfirmation();
        Require(rowNavigationHost.Calls.Count==0,"actual task row pointer opens native confirmation before map request");
        Require(confirmation.TitleText.Text!="confirmation"&&confirmation.ConfirmText.Text!="confirm"&&confirmation.CancelText.Text!="cancel","native dialog common title and action text resolve selected language");
        Capture("home-task-row-jump-confirm-native.png");Click(confirmation.CancelBtn);yield return WaitAllClosed();
        Require(rowNavigationHost.Calls.Count==0,"actual row native cancel preserves map");
        Click(jump);yield return WaitConfirmation();Click(confirmation.ConfirmBtn);yield return WaitAllClosed();
        Require(rowNavigationHost.Calls.SequenceEqual(new[]{"goto:10:0"}),"actual row and native confirmation pointers reach the original exact target request once");
        taskRowNavigationFactory=null;taskRowNavigationVerifier=null;
    }
    IEnumerator ExerciseTaskNavigation()
    {
        var data=OfflineHomeMainlineTaskData.LoadDefault();var host=new TaskJumpHost(this,data);
        var completed=new List<string>();var navigation=new OfflineHomeTaskNavigation(data,host,_=>completed.Add("default"));
        Require(navigation.CanNavigate(1000)&&navigation.CanNavigate(1005)&&!navigation.CanNavigate(1030),"selected condition jumps from home and original profession non-jumpable");
        Require(navigation.TraceTarget(1000)==null&&navigation.TraceTarget(1030)==null&&!navigation.TraceTarget(1005).CanTrace(),"only original trace-capable conditions expose targets");
        host.Map=10;Require(!navigation.CanNavigate(1005)&&navigation.TraceTarget(1005).CanTrace(),"same-map world task traces instead of jumping");
        host.Map=11;Require(!navigation.CanNavigate(1005),"ordinary current map prevents world jump");
        host.Groups[11]=12;Require(navigation.CanNavigate(1005),"group current-map mismatch permits world jump");
        host.HasContainer=false;Require(!navigation.CanNavigate(1005),"missing current container refuses world jump");
        host.HasContainer=true;host.TraceJump=true;Require(navigation.TraceTarget(1005).CanTrace(),"explicit original jump-entity query enables cross-map trace");host.TraceJump=false;
        host.Map=0;host.Groups.Clear();
        navigation.Navigate(1005,null,null,null);yield return WaitConfirmation();
        Require(host.Calls.Count==0&&host.LastText.Key=="jump_to_map_tip1","world jump waits for native confirmation with original text key");
        Require(!confirmation.ContentText.GetComponent<TMP_Text>().text.Contains("jump_to_map_tip1"),"original native confirmation resolves actual selected language text");
        Capture("home-task-navigation-confirm-native.png");Click(confirmation.CancelBtn);yield return WaitAllClosed();Require(host.Calls.Count==0,"native cancellation never enters map");
        navigation.Navigate(1005,null,null,null);yield return WaitConfirmation();Click(confirmation.ConfirmBtn);yield return WaitAllClosed();
        Require(host.Calls.SequenceEqual(new[]{"goto:10:0"}),"original confirmation pointer enters the exact requested map/region once");host.Calls.Clear();
        host.FastClear=true;navigation.Navigate(1005,null,null,null);Require(windows.WindowCount==0&&host.Calls.Count==0,"fast-clear handler consumes jump before popup");host.FastClear=false;
        host.Accessible=false;host.Unlock=host.Now.AddDays(1);navigation.Navigate(1005,null,null,null);
        Require(host.Calls.SequenceEqual(new[]{"tip:MapUnLockTip"}),"unavailable future region preserves original time-tip branch");host.Calls.Clear();
        host.Accessible=true;host.Unlock=DateTime.MinValue;host.RegionUnlocked=false;host.Previous=7;
        navigation.Navigate(1005,null,null,null);yield return WaitConfirmation();Require(host.LastText.Key=="jump_to_map_tip2"&&host.LastRegionName=="region_10_7","locked target uses original previous-region confirmation");Click(confirmation.CancelBtn);yield return WaitAllClosed();host.RegionUnlocked=true;
        host.Groups[10]=11;host.Previous=3;
        navigation.Navigate(1005,null,null,null);yield return WaitConfirmation();
        Require(host.LastText.Key=="jump_to_map_tip2"&&host.LastRegionName=="region_11_3","group redirect confirmation names the actual last-entry region");
        Click(confirmation.ConfirmBtn);yield return WaitAllClosed();
        Require(host.Calls.SequenceEqual(new[]{"goto:10:0"}),"group redirect label preserves original requested target in map entry call");host.Calls.Clear();host.Groups.Clear();
        navigation.Navigate(1025,null,null,null);yield return WaitConfirmation();Require(host.LastText.Key=="jump_to_map_tip0","entity-tag jump uses original confirmation");Click(confirmation.ConfirmBtn);yield return WaitAllClosed();
        Require(host.Calls.SequenceEqual(new[]{"trace:1025","entity:10:77"}),"entity confirmation registers pending trace before exact tag entry request");host.Calls.Clear();
        host.Map=0;Require(!navigation.CanNavigate(1040),"chest completion has no home-side jump");host.Map=10;Require(navigation.CanNavigate(1040),"chest completion can return home from map");
        navigation.Navigate(1040,null,null,null);yield return WaitConfirmation();Require(host.Calls.Count==0,"leave-map request waits for native confirmation");Click(confirmation.ConfirmBtn);yield return WaitAllClosed();Require(host.Calls.SequenceEqual(new[]{"leave"}),"chest native confirmation leaves map once");host.Calls.Clear();
        navigation.Navigate(1070,null,null,(Action<object>)(_=>completed.Add("caller")));
        Require(host.Calls.SequenceEqual(new[]{"ladder"})&&completed.SequenceEqual(new[]{"caller","default"}),"original fantasy ladder and wrapped callback order");host.Calls.Clear();
        var allMap=new TaskUI.ClientMapGameTotalCountCondition{Host=host,MapClassId=-1,IsJumpable=true};
        Require(!allMap.CanJump(),"all-map battle target requires visited region");host.RegionCount=1;Require(allMap.CanJump(),"all-map battle target reads actual supplied visited count");
        host.Map=0;host.Accessible=false;navigation.Navigate(1050,null,null,null);Require(host.Calls.SequenceEqual(new[]{"tip:MainlandPanel.MapUnLockLimit"}),"goddess jump preserves original map-access rejection");host.Calls.Clear();
        foreach(var condition in new TaskUI.HomeTaskJumpCondition[]{new TaskUI.ClientWorldMapProgressCondition(),new TaskUI.ClientMapGameTotalCountCondition(),new TaskUI.ClientMapLogicEntityEndByTagCondition(),new TaskUI.ClientMapRegionGoddessCondition(),new TaskUI.ClientTotalOpenChestCondition(),new TaskUI.ClientDemonBattleLayerPassCondition()}){
            condition.Host=host;condition.IsJumpable=false;Require(!condition.CanJump(),"original false/null jumpability is rejected before navigation state");
        }
        Require(windows.WindowCount==0,"all original navigation dialogs closed");
        Debug.Log("HOME_TASK_NAVIGATION_PASSED: selected original condition decisions, same/group/missing-map branches, trace visibility, native original confirmation text/cancel/confirm pointers, exact requested region, entity pending-trace order, chest return-home, ladder callback order, all-map battle and original rejection branches. Map/entry/trace operations are explicit controlled ports; production routing remains pending.");
    }
    sealed class TaskJumpHost:TaskUI.IHomeTaskJumpHost
    {
        readonly HomeCurrencyWindowsNativeRunner owner;readonly OfflineHomeMainlineTaskData data;readonly Dictionary<string,string> texts;
        public readonly List<string> Calls=new List<string>();public readonly Dictionary<int,int> Groups=new Dictionary<int,int>();
        public int Map,RegionCount,Previous;public bool HasContainer=true,Accessible=true,FastClear,Galaxy,RegionUnlocked=true,TraceJump;
        public readonly DateTime Now=new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);public DateTime Unlock=DateTime.MinValue;
        public TaskUI.HomeTaskJumpText LastText;public string LastRegionName;
        public TaskJumpHost(HomeCurrencyWindowsNativeRunner owner,OfflineHomeMainlineTaskData data){this.owner=owner;this.data=data;texts=JObject.Parse(Resources.Load<TextAsset>("Home/home-task-navigation-texts").text)["texts"].ToObject<Dictionary<string,string>>();}
        public bool IsOnMap()=>Map!=0;public bool IsOnMap(int map)=>Map==map;public bool IsInTheMap(int map)=>Map==map;
        public bool HasMapContainer=>HasContainer;public int CurrentMapClassId=>Map;public int WorldMapRegionProgressCount=>RegionCount;
        public int GetGroupCurrentMap(int map)=>Groups.TryGetValue(map,out int value)?value:map;
        public bool IsMapAccessible(int map)=>Accessible;public bool TryOpenMapEnterByFastClear(int map)=>FastClear;public bool TryOpenMapGalaxyPanel(int map)=>Galaxy;
        public DateTime GetDateTime()=>Now;public DateTime GetRegionUnlockTime(int map,int region)=>Unlock;
        public bool IsRegionUnlocked(int map,int region,DateTime now)=>RegionUnlocked;
        public TaskUI.HomeTaskJumpRegion GetPreviousRegion(int map,int region)=>new TaskUI.HomeTaskJumpRegion{Region=Previous};
        public bool TryGetLastMapEntry(int map,out int region){region=Previous;return true;}
        public string GetRegionNameKey(int map,int region)=>LastRegionName="region_"+map+"_"+region;
        public object FormatDetailedTimeSpan(TimeSpan span)=>span;
        public void Broadcast(string key,params object[] args)=>Calls.Add("tip:"+key);
        public void Popup(Action<TaskUI.IHomeTaskJumpDialog> initialize)
        {
            owner.windows.PopupConfirm(dialog=>{
                owner.confirmation=dialog;
                foreach(var text in dialog.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>texts.TryGetValue(key,out string value)?value:data.Texts.ContainsKey(key)?data.Text(key):owner.assets.Text(key);
                initialize(new Dialog(this,dialog));
            });
        }
        public Action<int,int> EntryRoute;
        public void GotoMapRegion(int map,int region){Calls.Add("goto:"+map+":"+region);EntryRoute?.Invoke(map,region);}
        public void NavigateRegion(int map,int region,Action<object> completed){Calls.Add("navigate:"+map+":"+region);completed?.Invoke(this);}
        public void NavigateFantasyLadder(Action<object> completed){Calls.Add("ladder");completed?.Invoke(this);}
        public void TraceTaskWhenEnterMap(long id)=>Calls.Add("trace:"+id);
        public TaskUI.HomeTaskJumpTag GetTag(string tag)=>new TaskUI.HomeTaskJumpTag{MapClass=10,LogicId=77};
        public TaskUI.HomeTaskJumpRegion GetEntityRegion(int map,int logic)=>new TaskUI.HomeTaskJumpRegion{Region=0};
        public void EnterNearEntity(int map,int logic)=>Calls.Add("entity:"+map+":"+logic);public void LeaveMap()=>Calls.Add("leave");
        public bool CanTraceEntityToJump(int map,int region)=>TraceJump;
        sealed class Dialog:TaskUI.IHomeTaskJumpDialog
        {
            readonly TaskJumpHost host;readonly WorldConfirmDialog dialog;
            public Dialog(TaskJumpHost host,WorldConfirmDialog dialog){this.host=host;this.dialog=dialog;}
            public void Init(TaskUI.HomeTaskJumpText text,Action confirm){host.LastText=text;dialog.Init(text.Key,text.Args,confirm);}
            public void Init(string key,Action confirm)=>Init(new TaskUI.HomeTaskJumpText(key),confirm);
        }
    }
}
