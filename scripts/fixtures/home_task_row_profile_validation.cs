using TaskUI=Zjcs.Official.HomeTaskUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Offline.HomeTaskUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

public sealed partial class HomeCurrencyWindowsNativeRunner
{
    Func<OfflineHomeMainlineTaskData,IOfflineHomeTaskNavigation> taskRowNavigationFactory;
    Func<TaskUI.TaskListItem,IEnumerator> taskRowNavigationVerifier;
    Action<TaskUI.IHomeTaskPlayer,TaskRowProfileHost> taskRowTraceSetup;
    Func<TaskUI.TaskListItem,IEnumerator> taskRowTraceVerifier;
    Action taskRowTraceFinalCheck;
    IEnumerator ExerciseTaskRowsWithProfile()
    {
        string root=System.IO.Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"task-row-profile-"+Guid.NewGuid().ToString("N"));
        OfflineHomeTaskPlayer player=null;OfflineHomeTaskConditions conditions=null;OfflineHomeTaskRewardIcons icons=null;
        GameObject slot=null;TaskRowProfileHost host=null;int beforeSubscribers=subscribers;
        var failures=new List<Exception>();
        try {
            var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(root);
            string id=creation.CreateSlot();var draft=creation.OpenDraft(id);draft.name="任务条验证";
            Require(creation.TryCreateCharacter(id,draft,out string error),error);
            var slots=new Zjcs.Offline.State.ProfileSlotCatalog(root);var catalog=OfficialMainlineTaskCatalog.LoadDefault();
            var profile=OfflineMainlineProgressSession.Open(slots,id,catalog);
            var data=OfflineHomeMainlineTaskData.LoadDefault();taskIconData=data;
            var settings=(Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.Linq.JObject.Parse(assets.configuration.text)["tables"]["game_settings"];
            var reader=new OfflineHomeTaskConditionReader(profile,data,_=>0,()=>(int)settings["GachaId"]);
            conditions=new OfflineHomeTaskConditions(reader,new Zjcs.Offline.HomeUI.OfflineHomeConditionPlayer(
                Zjcs.Offline.HomeUI.OfficialCurrentHomeRuleAssetCatalog.LoadDefault(),()=>profile.Save,_=>false),failures.Add);
            var results=new List<ProgressionResult>();
            player=new OfflineHomeTaskPlayer(profile,data,reader,conditions,new OfflineHomeTaskRewardPresenter(data,settings),
                taskRowNavigationFactory?.Invoke(data)??new TaskPlayerNavigationProbe(),()=>0,()=>TaskUI.SubRank.Norank,results.Add,failures.Add);
            icons=new OfflineHomeTaskRewardIcons(canvas.transform,this,fonts,Post,data,assets);
            host=new TaskRowProfileHost(this,player,conditions,icons,data);
            taskRowTraceSetup?.Invoke(player,host);
            slot=new GameObject("DetachedTaskProfileFixtureHost",typeof(RectTransform));slot.SetActive(false);slot.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)slot.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
            var layout=slot.AddComponent<VerticalLayoutGroup>();layout.childAlignment=TextAnchor.LowerLeft;layout.spacing=30;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;layout.childScaleWidth=layout.childScaleHeight=true;
            var fitter=slot.AddComponent<ContentSizeFitter>();fitter.horizontalFit=fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/UI/PinnedTasks/HomePinnedTasks.prefab");
            Require(prefab,"original packaged pinned-task panel");
            var panelRoot=Object.Instantiate(prefab,slot.transform,false);host.Bind(panelRoot);
            var panel=panelRoot.GetComponent<TaskUI.PinnedTaskPanel>();
            slot.SetActive(true);yield return null;yield return new WaitForSeconds(.7f);Canvas.ForceUpdateCanvases();
            var rows=Field<Dictionary<long,TaskUI.TaskListItem>>(panel,"mCurrentTaskListItem");
            Require(rows.Count==1&&rows.ContainsKey(1000)&&host.RefreshCalls==1,"original pinned panel acquires the real new-character task and invokes its first daily refresh");
            var first=rows[1000];
            Require(Field<WorldMultiLanguageText>(first,"myTaskTitle").Text==data.Text("task_1000_title"),"native title uses the selected original task text");
            Func<string> battleText=()=>first.TaskGoal.ConditionItems[0].GetComponentInChildren<TMP_Text>(true).text;
            Require(!first.TaskGoal.ProgressText.gameObject.activeSelf&&battleText().Contains("0")&&battleText().Contains("2"),"native new-character task starts at zero of two in the original condition label");
            RequireTaskTextOnScreen(first);
            Capture("home-task-profile-processing-native.png");
            Require(profile.InitializeMap(10,new[]{0,1,2,3,4}).IsApplied&&profile.RecordMapBattle(10,0,1,"native-row-battle-1").IsApplied,"commit first actual map battle receipt");
            conditions.NotifyUpdateBlackboardData();
            Require(battleText().Contains("1"),"original task goal observer reads the committed one-battle progress without replacing its row");
            Require(profile.RecordMapBattle(10,0,1,"native-row-battle-2").IsApplied,"commit second actual map battle receipt");
            player.RefreshTasks(false);conditions.NotifyUpdateBlackboardData();yield return null;
            Require(rows.Count==1&&Field<WorldStateSwitcher<TaskUI.TaskListItem.TaskStatus>>(first,"mStateSwitcher").State==TaskUI.TaskListItem.TaskStatus.Completed,
                "original completion switch displays the claim state and hides the unsubmitted successor");
            long beforeMoney=profile.InventoryCount(2);int beforeSounds=sounds;
            RequireTaskTextOnScreen(first);Capture("home-task-profile-claim-native.png");Click(Field<Button>(first,"mClaimBtn"));yield return null;yield return null;
            Require(results.Count==1&&results[0].IsApplied&&profile.InventoryCount(2)==beforeMoney+30&&sounds>beforeSounds
                &&rows.Count==1&&!rows.ContainsKey(1000)&&rows.ContainsKey(1005),
                "unobstructed original claim pointer plays its PCM, commits thirty currency and replaces the received row with the displayed successor");
            var world=rows[1005];
            Require(profile.SetMapRegionProgress(10,0,2500).IsApplied,"commit quarter-region progress");
            conditions.NotifyWorldMapProgressUpdate();
            Require(world.TaskGoal.ProgressText.Text.Contains("25"),"original native world task formats the actual integer percentage");
            RequireTaskTextOnScreen(world);
            Capture("home-task-profile-world-progress-native.png");
            if(taskRowNavigationVerifier!=null)yield return taskRowNavigationVerifier(world);
            if(taskRowTraceVerifier!=null)yield return taskRowTraceVerifier(world);
            Require(profile.SetMapRegionProgress(10,0,10000).IsApplied,"commit completed region progress");
            player.RefreshTasks(false);conditions.NotifyWorldMapProgressUpdate();yield return null;
            Require(Field<WorldStateSwitcher<TaskUI.TaskListItem.TaskStatus>>(world,"mStateSwitcher").State==TaskUI.TaskListItem.TaskStatus.Completed,"actual world completion updates the original row state");
            Capture("home-task-profile-world-claim-native.png");Click(Field<Button>(world,"mClaimBtn"));yield return null;yield return null;
            Require(results.Count==2&&results.All(result=>result.IsApplied)&&profile.InventoryCount(2)==beforeMoney+60
                &&rows.Count==1&&rows.ContainsKey(1010),"second original claim pointer advances to the next original region task and awards once");
            if(host.TraceHandlerOverride!=null)Require(host.TraceHandlerOverride.CurrentTraceData==null,"original claimed-row destruction stops its live trace");
            var restored=OfflineMainlineProgressSession.Open(slots,id,catalog);
            Require(restored.InventoryCount(2)==beforeMoney+60&&restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1000)
                &&restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1005),"two native claim transactions survive reopening the actual profile");
            slot.SetActive(false);yield return null;
            Require(subscribers==beforeSubscribers&&host.TipObservers==0&&host.PositionObservers==0,"native row disable returns icons and releases tip/position subscriptions");
            slot.SetActive(true);yield return null;
            Require(rows.Count==1&&rows.ContainsKey(1010)&&host.RefreshCalls==2,"original panel reenable restores the actual current task with one immediate refresh");
            slot.SetActive(false);Object.Destroy(slot);slot=null;yield return null;
            Require(subscribers==beforeSubscribers&&host.TipObservers==0&&host.PositionObservers==0&&failures.Count==0,"original row destruction releases subscriptions without isolated task errors");
            taskRowTraceFinalCheck?.Invoke();
            Debug.Log("HOME_TASK_PROFILE_ROWS_PASSED: actual new-character profile, original PinnedTaskPanel/TaskListItem/TaskGoal, live battle and world percentages, original state switchers, two unobstructed claim pointers with original PCM, real reward/receipt restoration, hidden successor activation and native disable/reenable/destruction. Actual selected map limits/recommendation metadata is bound. Test host controls combat/rank, navigation/trace, calendar and tip visibility; production HUD and full route remain separate.");
        }
        finally {
            if(slot){slot.SetActive(false);Object.Destroy(slot);}
            player?.Dispose();conditions?.Dispose();icons?.Dispose();taskIconData=null;
            if(System.IO.Directory.Exists(root))System.IO.Directory.Delete(root,true);
        }
        yield return null;
    }

    void RequireTaskTextOnScreen(TaskUI.TaskListItem row)
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in row.GetComponentsInChildren<TMP_Text>(false)){
            if(string.IsNullOrWhiteSpace(text.text))continue;
            var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
            foreach(var corner in corners){var point=RectTransformUtility.WorldToScreenPoint(cameraUi,corner);
                Require(point.x>=-.5f&&point.x<=target.width+.5f&&point.y>=-.5f&&point.y<=target.height+.5f,
                    "original task text rect remains inside the actual render target: "+text.name+" "+point);}
        }
    }

    sealed class TaskRowProfileHost : TaskUI.IHomeTaskServices,TaskUI.ITaskHandler,TaskUI.IMyMapPositionHandlerProxy,TaskUI.IHomeTaskGlobalColors,IProfessionTipDotSystem
    {
        readonly HomeCurrencyWindowsNativeRunner owner;readonly OfflineHomeTaskPlayer player;readonly OfflineHomeTaskConditions conditions;
        readonly OfflineHomeTaskRewardIcons icons;readonly OfflineHomeMainlineTaskData data;
        readonly Dictionary<string,string> texts;
        readonly OfflineHomeTaskMapData maps=OfflineHomeTaskMapData.LoadDefault();
        public int RefreshCalls;
        public TaskRowProfileHost(HomeCurrencyWindowsNativeRunner owner,OfflineHomeTaskPlayer player,OfflineHomeTaskConditions conditions,OfflineHomeTaskRewardIcons icons,OfflineHomeMainlineTaskData data)
        {
            this.owner=owner;this.player=player;this.conditions=conditions;this.icons=icons;this.data=data;
            var original=UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Home/UI/PinnedTasks/home-task-ui-texts.json");
            Require(original,"original native task UI language payload");
            texts=((Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(original.text)["texts"])
                .ToDictionary(row=>(string)row["key"],row=>(string)row["text"]);
        }
        public TaskUI.IHomeTaskPlayer Player=>player;public TaskUI.IHomeTaskConditions Conditions=>conditions;
        public TaskUI.IMapInputSupportHandlerProxy mMapInput=>null;public TaskUI.INavigateManager NavigateManager=>null;
        public TaskUI.ITaskHandler TraceHandlerOverride;
        public TaskUI.ITaskHandler TaskHandler=>TraceHandlerOverride??this;public TaskUI.IItemIconManager ItemIconManager=>icons;public TaskUI.IMyMapPositionHandlerProxy MyMapPositionHandler=>this;
        public TaskUI.IHomeTaskGlobalColors GlobalColors=>this;public TaskUI.IHomeTaskCombatColors CombatRatingConfig=>maps;
        public object PlanesRank=>null;
        public TaskUI.GlobalColors Value=>new TaskUI.GlobalColors{EnoughColor=new Color(.4627451f,.4745098f,.09411765f,1),LackColor=new Color(.9254902f,.3372549f,.3372549f,1)};
        public DateTime GetDateTime()=>new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);
        public double CalcRemainSecond(DateTime endTime)=>(endTime-GetDateTime()).TotalSeconds;
        public DateTime GetDailyResetTimeFromDate(DateTime dateTime)=>dateTime.Date.AddHours(6);
        public TaskUI.MapRegionInfo GetMapRegion(int map,int region)=>maps.Get(map,region);
        public int CalcRankSuppression(TaskUI.SubRank playerRank,TaskUI.SubRank regionRank)=>maps.CalcRankSuppression(playerRank,regionRank);
        public int GetCombatRatingLevel(long rating,IReadOnlyList<long> levels)=>TaskUI.HomeTaskMapFormula.GetCombatRatingLevel(rating,levels);
        public bool TryCheckTaskLimit(int id,TaskUI.IHomeTaskPlayer player,out TaskUI.TaskLimitResult result){result=null;return false;}
        public string GetTaskTitleKey(int id)=>"task_"+id+"_title";public string GetFullName(GameObject go)=>go.name;
        public TaskUI.TaskListItem InstantiateTaskItem(GameObject template,Transform parent)
        {
            var staging=new GameObject("InactiveOriginalTaskInjection");staging.SetActive(false);
            try {
                var go=Object.Instantiate(template,staging.transform,false);Bind(go);
                go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                return go.GetComponent<TaskUI.TaskListItem>();
            }finally{Object.Destroy(staging);}
        }
        public void Bind(GameObject go)
        {
            Require(go.GetComponentsInChildren<MonoBehaviour>(true).All(component=>component),"all original task-prefab scripts resolve before activation");
            foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true)){
                var service=component.GetType().GetProperty("Services");if(service?.PropertyType==typeof(TaskUI.IHomeTaskServices))service.SetValue(component,this);
            }
            foreach(var dot in go.GetComponentsInChildren<Zjcs.Official.HomeMainUiControls.TipDotObject>(true))dot.TipDotSystem=this;
            foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))if(text.font)owner.fonts.SetupFont(text.font);
            foreach(var text in go.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>data.Texts.ContainsKey(key)?data.Text(key):texts.TryGetValue(key,out string value)?value:owner.assets.Text(key);
            foreach(var sound in go.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=owner.Post;
        }
        public void OpenTaskDetail(TaskUI.PlayerTask task)=>throw new NotSupportedException("Actual detail-window boundary");
        public void DebugCompleteTask(List<long> ids)=>throw new NotSupportedException();
        public void RefreshAllTasks(bool invokeByPlayer){RefreshCalls++;player.RefreshTasks();}
        public void RefreshLimitTimeTask(long id,bool invokeByPlayer)=>throw new NotSupportedException();
        public TaskUI.MapRegionLimitStatusRefreshForRankUnlockTime CreateTimedRegionLimitProvider()=>throw new NotSupportedException();
        public UnityEvent OnTeamUpdate{get;}=new UnityEvent();public TaskUI.HomeTaskTraceEvent OnTraceStart{get;}=new TaskUI.HomeTaskTraceEvent(Debug.LogException);public TaskUI.HomeTaskTraceEvent OnTraceEnd{get;}=new TaskUI.HomeTaskTraceEvent(Debug.LogException);public TaskUI.HomeTaskPlayerEvent<long> OnTraceCancel{get;}=new TaskUI.HomeTaskPlayerEvent<long>(Debug.LogException);
        public TaskUI.ITaskTraceMapEntity CurrentTraceData=>null;
        public long? WaitTraceTaskId=>null;
        public void TraceWhenEnterMap(long id)=>throw new NotSupportedException();public void CleanWaitTraceTask()=>throw new NotSupportedException();
        public void StartTrace(long id,bool showTip)=>throw new NotSupportedException("Actual navigation/trace boundary");public void CancelTrace(bool showTip)=>throw new NotSupportedException();public void StopTrace(long id){}
        Action<int,int> position;public event Action<int,int> OnMeRegionUpdate{add=>position+=value;remove=>position-=value;}
        public void NotifyPosition(int map,int region)=>position?.Invoke(map,region);
        public int PositionObservers=>position?.GetInvocationList().Length??0;
        public bool IsShowTipDot=>true;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}
        public int TipObservers=>OnTipDotShowChanged?.GetInvocationList().Length??0;
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;

    }
}
