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
using Zjcs.Official.HomeTaskUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;
using UnityEngine.Events;
using TaskListItem=Zjcs.Official.HomeTaskUI.TaskListItem;
using ShowRemainTimeInfo=Zjcs.Official.HomeTaskUI.ShowRemainTimeInfo;
using IItemIconManager=Zjcs.Official.HomeTaskUI.IItemIconManager;
using ItemIcon=Zjcs.Official.HomeTaskUI.ItemIcon;
using ItemInfo=Zjcs.Official.HomeTaskUI.ItemInfo;
using GlobalColors=Zjcs.Official.HomeTaskUI.GlobalColors;

[InitializeOnLoad]
public static class HomeTaskUiNativeValidation
{
    const string Key="Zjcs.HomeTaskUiNativeValidation";
    static HomeTaskUiNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                HomeTaskUiNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("@TASK_PREFAB@");
                HomeTaskUiNativeRunner.Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("@SOUND_GUID@"));
                HomeTaskUiNativeRunner.Texts=AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@");
                HomeTaskUiNativeRunner.AdditionalFont=AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("@FONT_GUID@"));
                new GameObject("OriginalHomeTaskFixture").AddComponent<HomeTaskUiNativeRunner>();
            }
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Home result fixture timed out."));};
    }
    public static void Run()
    {
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_TASK_UI_NATIVE_PASSED: original pinned panel and 250-object task template, actual pointers, filtering, sorting, progress, claim, navigation, trace and lifecycle. Task operations are controlled fixtures; production integration remains separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeTaskUiNativeRunner:MonoBehaviour
{
    public static GameObject Prefab;public static AudioClip Clip;public static TextAsset Texts;public static Font AdditionalFont;
    Camera camera;RenderTexture target;EventSystem events;int errors;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeTaskUiNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeTaskUiNativeValidation.Finish(null);
    }
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
        Require(Prefab&&Texts&&Clip,"native task prefab, language and original PCM loaded");
        camera=new GameObject("MutedUiCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();camera.targetTexture=target;
        var metrics=ScreenMetrics.Bind(new Metrics());var screen=new ScreenAdaptService();screen.Initialize();
        var inactive=new GameObject("InactiveOriginalFrame");inactive.SetActive(false);
        var frame=Object.Instantiate(OfficialHomeScreenAssetCatalog.LoadDefault().framePrefab,inactive.transform,false);frame.SetActive(true);
        var rootCanvas=frame.GetComponent<Canvas>();rootCanvas.renderMode=RenderMode.ScreenSpaceCamera;rootCanvas.worldCamera=camera;rootCanvas.planeDistance=1;
        foreach(var canvas in frame.GetComponentsInChildren<Canvas>(true))canvas.worldCamera=camera;
        frame.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService=screen;
        foreach(var safe in frame.GetComponentsInChildren<SafeArea>(true))safe.ScreenAdaptService=screen;
        var slot=new GameObject("DetachedOriginalLeftBotGo",typeof(RectTransform));slot.transform.SetParent(frame.transform.Find("Content"),false);
        var rect=(RectTransform)slot.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.zero;
        var layout=slot.AddComponent<VerticalLayoutGroup>();layout.childAlignment=TextAnchor.LowerLeft;layout.spacing=30;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;layout.childScaleWidth=layout.childScaleHeight=true;
        var fitter=slot.AddComponent<ContentSizeFitter>();fitter.horizontalFit=fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var root=Object.Instantiate(Prefab,slot.transform,false);Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original panel scripts resolve");
        var baseFonts=OfficialHomePlayerInfoAssetCatalog.LoadDefault().CreateFontService();
        var fonts=new FontAssetFontService(new FontLoader(Field<IProfessionImageResourceLoader>(baseFonts,"mResourceLoader")),Field<Func<string,FontAssetFontConfig>>(baseFonts,"mDeserialize"));fonts.Initialize();
        var dictionary=JsonUtility.FromJson<LanguageData>(Texts.text).texts.ToDictionary(x=>x.key,x=>x.text);
        dictionary["Fixture.Title.1"]="完成任务测试";dictionary["Fixture.Title.2"]="前往目标测试";dictionary["Fixture.Condition"]="测试目标";
        LanguageManager.Initialize(new Language(dictionary));
        var host=new Tasks(dictionary,fonts,Clip);var panel=root.GetComponent<PinnedTaskPanel>();panel.Services=host;
        var first=new TaskRow(host,1){Completed=true,Remain=TimeSpan.FromSeconds(600)};var second=new TaskRow(host,2){Jump=true,Trace=true};
        first.Condition.Progress=3;first.Condition.Met=true;
        host.Rows.Add(second);host.Rows.Add(new TaskRow(host,3){Visible=false});host.Rows.Add(first);host.Rows.Add(new TaskRow(host,4){PinnedValue=false});
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();var previousSound=OfficialWwiseEvent.Host;var sound=new Sound();OfficialWwiseEvent.Host=sound;host.Sound=sound;
        inactive.SetActive(true);yield return null;yield return null;yield return new WaitForSeconds(.7f);LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();
        var items=Field<Dictionary<long,TaskListItem>>(panel,"mCurrentTaskListItem");
        Require(items.Count==2&&items.ContainsKey(1)&&items.ContainsKey(2),"original pin and display-condition filters");
        Require(items[1].transform.GetSiblingIndex()<items[2].transform.GetSiblingIndex(),"original duration priority sorts timed task first");
        Require(host.RefreshCalls==1&&!host.LastInvoke,"daily coroutine immediately requests all task refresh with invokeByPlayer false");
        Require(Field<WorldMultiLanguageText>(items[1],"myTaskTitle").Text==dictionary["Fixture.Title.1"],"original task title binding");
        Require(Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[1],"mStateSwitcher").State==TaskListItem.TaskStatus.Completed,"finishable task selects original completed state");
        Require(Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[2],"mStateSwitcher").State==TaskListItem.TaskStatus.Jump,"navigable task selects original jump state");
        Capture("home-task-claim.png");Click(Field<Button>(items[1],"mClaimBtn"));Require(host.Claims==1&&!items.ContainsKey(1),"actual claim pointer submits one and removes received task");yield return null;
        Click(Field<Button>(items[2],"mJumpBtn"));Require(host.Navigations==1,"actual jump pointer reaches task navigation host");
        second.Jump=false;host.Update(second);yield return null;
        Require(Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[2],"mStateSwitcher").State==TaskListItem.TaskStatus.NotTracing,"traceable task selects original untraced state");
        Click(Field<Button>(items[2],"mTraceBtn"));Require(host.StartCalls==1&&Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[2],"mStateSwitcher").State==TaskListItem.TaskStatus.Tracing,"actual trace pointer and original trace observers update state");
        yield return null;
        Capture("home-task-tracing.png");Click(Field<Button>(items[2],"mCanclTraceBtn"));Require(host.CancelCalls==1&&Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[2],"mStateSwitcher").State==TaskListItem.TaskStatus.NotTracing,"actual cancel pointer updates original trace state");
        second.Condition.Progress=2;host.EmitConditions();Require(items[2].TaskGoal.ProgressText.Text.Contains("2")&&items[2].TaskGoal.ProgressText.Text.Contains("3"),"condition observer refreshes native colored progress");
        second.Completed=true;host.Update(second);Require(Field<WorldStateSwitcher<TaskListItem.TaskStatus>>(items[2],"mStateSwitcher").State==TaskListItem.TaskStatus.Completed,"task update switches traceable item to completed");
        host.DebugOffset();Require(host.RefreshCalls==3,"debug offset refreshes once then starts original immediately refreshing coroutine");
        second.PinnedValue=false;host.Update(second);yield return null;Require(items.Count==0,"unpin update destroys existing native item");
        root.SetActive(false);Require(host.PinObservers==0&&host.ConditionObservers==0,"disable releases panel and item observers");root.SetActive(true);yield return null;Require(host.PinObservers==4&&host.RefreshCalls==4,"reenable installs one observer set and refreshes immediately");
        root.SetActive(false);Object.Destroy(inactive);yield return null;Require(host.PinObservers==0&&host.ConditionObservers==0&&host.TipObservers==0,"destroy leaves no task or global-tip observers");
        Require(sound.Posts>=4,"native task pointer plays original PCM while muted");OfficialWwiseEvent.Host=previousSound;sound.Dispose();fonts.Dispose();screen.Dispose();metrics.Dispose();Require(errors==0,"no Unity errors in exercised task flows");
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var rect=(RectTransform)button.transform;var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native task button is first unobstructed target: "+button.name+" hits="+string.Join(",",hits.Take(8).Select(x=>x.gameObject.name)));
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static FieldInfo FindField(object owner,string name){for(var type=owner.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(field!=null)return field;}throw new MissingFieldException(name);}
    static T Field<T>(object owner,string name)=>(T)FindField(owner,name).GetValue(owner);
    static void SetField(object owner,string name,object value)=>FindField(owner,name).SetValue(owner,value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    [Serializable] sealed class LanguageData{public LanguageRow[] texts;}[Serializable] sealed class LanguageRow{public string key,text;}
    sealed class Language:ILocalizationTextService
    {
        readonly Dictionary<string,string> texts;public Language(Dictionary<string,string> values){texts=values;}
        string Text(string key)=>texts.TryGetValue(key,out var value)?value:key;
        public LanguageType CurrentLanguage=>LanguageType.zh_CN;public void ReloadData(){}public bool ContainsKey(string key)=>texts.ContainsKey(key);
        public bool TryGetOriginalText(string key,LanguageType language,out string result)=>texts.TryGetValue(key,out result);
        public string GetText(string key,LanguageType language,params object[] args)=>args.Length==0?Text(key):string.Format(Text(key),args);
        public string GetTextFormated(string key,LanguageType language,params object[] args)=>GetText(key,language,args);
        public void SetReplacer(ITextReplacer replacer){}public bool TryGetText(string key,LanguageType language,out string result)=>TryGetOriginalText(key,language,out result);
        public bool TryGetText(string key,out string result)=>texts.TryGetValue(key,out result);public void AddDataProvider(ILanguageTextDataProvider provider){}
    }
    sealed class Condition:Zjcs.Official.HomeTaskUI.ICondition
    {
        public int Progress;public bool Met,Display=true;public override ConditionType Type=>ConditionType.Other;
        public override bool IsMet(DataHub data)=>Met;public override bool IsMet(IHomeTaskPlayer player)=>Display;
        public override (long,long) GetProgress(DataHub data)=>(Progress,3);
        public override ConditionDescComponent GetDescComponent(DataHub data,object context,string metColor,string notMetColor)=>new ConditionDescComponent{Key="Fixture.Condition",Args=Array.Empty<object>()};
    }
    sealed class Info:TaskInfo,ITaskTraceTarget
    {
        readonly TaskRow row;public Info(TaskRow row){this.row=row;}public override Zjcs.Official.HomeTaskUI.ICondition DisplayCondition=>new Condition{Display=row.Visible};
        public override Zjcs.Official.HomeTaskUI.ICondition FinishCondition=>row.Condition;public override bool IsAllowTrace=>row.Trace;public override bool ShowProgressBar=>true;public override int Order=>(int)row.TaskId;public override bool AutoTracing=>false;
        public override ITaskTraceTarget GetTaskTraceTarget()=>row.Trace?this:null;public bool CanTrace()=>true;
    }
    sealed class TaskRow:PlayerTaskDataWrap
    {
        readonly Tasks host;readonly long id;public bool Completed,Jump,Trace,Visible=true,PinnedValue=true;public TimeSpan? Remain;public readonly Condition Condition=new Condition();readonly Info info;
        public TaskRow(Tasks host,long id){this.host=host;this.id=id;info=new Info(this);}public override long TaskId=>id;public override int TaskClassId=>(int)id;public override PlayerTaskDataWrap DataWrap=>this;public override bool Pinned=>PinnedValue;public override TaskInfo Info()=>info;
        public override bool IsCompletedState()=>Completed;public override bool IsHandInItemsTask()=>false;public override Dictionary<int,long> GetNeedHandInItemsDict()=>throw new NotSupportedException();public override bool CanGetReward(IHomeTaskPlayer player)=>false;
        public override void InitRewardIconWithProbability(IItemIconManager icons,Transform slot,List<ItemIcon> results){}public override bool CanFinish(IHomeTaskPlayer player)=>Completed;public override bool CanNavigate()=>Jump;
        public override void Navigate(IMapInputSupportHandlerProxy input,INavigateManager navigation,object context){host.Navigations++;}
        public override void Submit(IHomeTaskPlayer player,long count){Require(count==1,"original single submit count");host.Claims++;host.Remove(id);}
        public override bool ShouldShowCountdown()=>Remain.HasValue;public override TimeSpan? GetRemainTime()=>Remain;
    }
    sealed class Tasks:IHomeTaskServices,IHomeTaskPlayer,IHomeTaskConditions,ITaskHandler,IMyMapPositionHandlerProxy,IHomeTaskGlobalColors,IProfessionTipDotSystem
    {
        readonly Dictionary<string,string> texts;readonly FontAssetFontService fonts;readonly AudioClip clip;public Sound Sound;
        public readonly List<TaskRow> Rows=new List<TaskRow>();public int Claims,Navigations,RefreshCalls,StartCalls,CancelCalls;public bool LastInvoke;
        public Tasks(Dictionary<string,string> texts,FontAssetFontService fonts,AudioClip clip){this.texts=texts;this.fonts=fonts;this.clip=clip;}
        public IHomeTaskPlayer Player=>this;public IHomeTaskConditions Conditions=>this;public IMapInputSupportHandlerProxy mMapInput=>null;public INavigateManager NavigateManager=>null;public ITaskHandler TaskHandler=>this;public IItemIconManager ItemIconManager=>null;public IMyMapPositionHandlerProxy MyMapPositionHandler=>this;
        public IHomeTaskGlobalColors GlobalColors=>this;public IHomeTaskCombatColors CombatRatingConfig=>throw new NotSupportedException();public object PlanesRank=>null;
        public GlobalColors Value=>new GlobalColors{EnoughColor=new Color(.4627451f,.4745098f,.09411765f,1),LackColor=new Color(.9254902f,.3372549f,.3372549f,1)};
        public DateTime GetDateTime()=>new DateTime(2026,1,1,12,0,0,DateTimeKind.Utc);public double CalcRemainSecond(DateTime endTime)=>(endTime-GetDateTime()).TotalSeconds;public DateTime GetDailyResetTimeFromDate(DateTime dateTime)=>dateTime.Date;
        public MapRegionInfo GetMapRegion(int mapClassId,int region)=>throw new NotSupportedException();public int CalcRankSuppression(SubRank player,SubRank region)=>throw new NotSupportedException();public int GetCombatRatingLevel(long combatRating,IReadOnlyList<long> values)=>throw new NotSupportedException();public bool TryCheckTaskLimit(int id,IHomeTaskPlayer player,out TaskLimitResult result){result=null;return false;}
        public string GetTaskTitleKey(int id)=>"Fixture.Title."+id;public string GetFullName(GameObject go)=>go.name;
        public TaskListItem InstantiateTaskItem(GameObject template,Transform parent)
        {
            var staging=new GameObject("InactiveTaskInjection");staging.SetActive(false);var go=Object.Instantiate(template,staging.transform,false);
            Require(go.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all 250-object native task template scripts resolve");
            foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true)){var service=component.GetType().GetProperty("Services");if(service?.PropertyType==typeof(IHomeTaskServices))service.SetValue(component,this);}
            foreach(var tip in go.GetComponentsInChildren<Zjcs.Official.HomeMainUiControls.TipDotObject>(true))tip.TipDotSystem=this;
            foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))if(text.font)fonts.SetupFont(text.font);
            foreach(var text in go.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>texts.TryGetValue(key,out var value)?value:key;
            foreach(var sound in go.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=(audio,emitter)=>{Require(audio==clip,"original task click PCM");Sound.PostEvent(@SOUND_ID@u,emitter);};
            go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;Object.Destroy(staging);return go.GetComponent<TaskListItem>();
        }
        public void OpenTaskDetail(PlayerTask task){}public void DebugCompleteTask(List<long> ids)=>throw new NotSupportedException();public void RefreshAllTasks(bool invokeByPlayer){RefreshCalls++;LastInvoke=invokeByPlayer;}public void RefreshLimitTimeTask(long id,bool invokeByPlayer)=>throw new NotSupportedException();public MapRegionLimitStatusRefreshForRankUnlockTime CreateTimedRegionLimitProvider()=>throw new NotSupportedException();
        Action<PlayerTaskDataWrap> added,updated;Action<long> removed,offset;
        public event Action<PlayerTaskDataWrap> OnTaskAdd{add{added+=value;}remove{added-=value;}}public event Action<PlayerTaskDataWrap> OnTaskUpdate{add{updated+=value;}remove{updated-=value;}}public event Action<long> OnTaskRemove{add{removed+=value;}remove{removed-=value;}}public event Action<long> OnDebugTimeOffset{add{offset+=value;}remove{offset-=value;}}
        public event Action<PlayerItemDataWrap,long,AwardSourceData> OnItemCountUpdate;public event Action<long,long> OnCombatRatingChanged;public event Action<SubRank> OnSubRankUpdate;public event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;public event Action<int> OnLevelUpdate;public event Action<int,int> OnMeRegionUpdate;
        public int PinObservers=>(added?.GetInvocationList().Length??0)+(updated?.GetInvocationList().Length??0)+(removed?.GetInvocationList().Length??0)+(offset?.GetInvocationList().Length??0);
        public IEnumerable<PlayerTask> PinTasks()=>Rows.Where(x=>x.Pinned);public bool IsPinnedTask(long id)=>Rows.Any(x=>x.TaskId==id&&x.Pinned);public PlayerTask GetTaskData(long id)=>Rows.FirstOrDefault(x=>x.TaskId==id);public bool Gm=>false;public int Level=>1;public long CombatRating=>10;public SubRank SubRank=>SubRank.Norank;public long GetItemAmount(int id)=>0;public bool HasItems(Dictionary<int,long> items)=>items==null||items.Count==0;
        public void Update(TaskRow row)=>updated?.Invoke(row);public void Remove(long id){Rows.RemoveAll(x=>x.TaskId==id);removed?.Invoke(id);}public void DebugOffset()=>offset?.Invoke(0);
        readonly Dictionary<Zjcs.Official.HomeTaskUI.ICondition,List<Action>> conditions=new Dictionary<Zjcs.Official.HomeTaskUI.ICondition,List<Action>>();
        public void Register(Zjcs.Official.HomeTaskUI.ICondition condition,Action refresh){if(!conditions.TryGetValue(condition,out var list))conditions[condition]=list=new List<Action>();list.Add(refresh);}
        public void Deregister(Zjcs.Official.HomeTaskUI.ICondition condition,Action refresh){if(conditions.TryGetValue(condition,out var list))list.Remove(refresh);}public int ConditionObservers=>conditions.Values.Sum(x=>x.Count);public void EmitConditions(){foreach(var callback in conditions.Values.SelectMany(x=>x).ToArray())callback();}
        public UnityEvent OnTeamUpdate{get;}=new UnityEvent();public UnityEvent OnTraceStart{get;}=new UnityEvent();public UnityEvent OnTraceEnd{get;}=new UnityEvent();public UnityEvent<long> OnTraceCancel{get;}=new UnityEvent<long>();public PlayerTask CurrentTraceData{get;private set;}
        public void StartTrace(long id,bool showTip){StartCalls++;CurrentTraceData=GetTaskData(id);OnTraceStart.Invoke();}public void CancelTrace(bool showTip){CancelCalls++;var id=CurrentTraceData?.TaskId??0;CurrentTraceData=null;OnTraceCancel.Invoke(id);}public void StopTrace(long id){if(CurrentTraceData?.TaskId==id)CurrentTraceData=null;}
        public bool IsShowTipDot=>true;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}public int TipObservers=>OnTipDotShowChanged?.GetInvocationList().Length??0;
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;
    }
    sealed class FontLoader:IProfessionImageResourceLoader
    {
        readonly IProfessionImageResourceLoader original;public FontLoader(IProfessionImageResourceLoader original){this.original=original;}
        public IProfessionAssetOperation<T> LoadAsset<T>(string path)where T:Object=>path=="Assets/TextMesh Pro/Fonts/MPLUSRounded1c-Medium.ttf"?new FontHandle<T>(AdditionalFont as T):original.LoadAsset<T>(path);
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path)where T:Object=>LoadAsset<T>(path);
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid)where T:Object=>throw new NotSupportedException();public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid)where T:Object=>throw new NotSupportedException();
    }
    sealed class FontHandle<T>:IProfessionAssetOperation<T>where T:Object
    {
        T value;public FontHandle(T value){this.value=value;}public bool IsValid=>value;public int Status=>1;public T Result=>value;
        public event Action<IProfessionAssetOperation<T>> Completed{add{value?.Invoke(this);}remove{}}public void Dispose(){value=null;}
    }
    sealed class Sound:IProfessionSoundHost,IDisposable
    {
        readonly GameObject root=new GameObject("MutedOriginalClick");public int Posts;
        public uint PostEvent(uint id,GameObject emitter){Require(id==@SOUND_ID@u,"original click event ID");Posts++;var source=root.GetComponent<AudioSource>();if(!source)source=root.AddComponent<AudioSource>();source.clip=Clip;source.volume=0;source.Play();Require(source.isPlaying,"original PCM playback starts while muted");return (uint)Posts;}
        public void Dispose()=>Object.Destroy(root);
    }
}
