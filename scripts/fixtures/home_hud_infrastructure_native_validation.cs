using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HomeHudInfrastructureNativeValidation
{
    const string Key="Zjcs.HomeHudInfrastructureNativeValidation";
    static HomeHudInfrastructureNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                HomeHudInfrastructureNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("@DOWNLOAD_PREFAB@");
                HomeHudInfrastructureNativeRunner.Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("@SOUND_GUID@"));
                HomeHudInfrastructureNativeRunner.Texts=AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@");
                new GameObject("OriginalHomeInfrastructureFixture").AddComponent<HomeHudInfrastructureNativeRunner>();
            }
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Home HUD infrastructure fixture timed out."));};
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
        else Debug.Log("HOME_HUD_INFRASTRUCTURE_NATIVE_PASSED: original entry configuration and activity lifecycle; original 90-object download entry, pointer dispatch, progress/state, checkpoint popup ordering and disposal. Activity/window services are fixtures; full production HomeUI remains separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeHudInfrastructureNativeRunner:MonoBehaviour
{
    public static GameObject Prefab;public static AudioClip Clip;public static TextAsset Texts;
    Camera camera;RenderTexture target;EventSystem events;int errors;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeHudInfrastructureNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeHudInfrastructureNativeValidation.Finish(null);
    }
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
        var activity=VerifyActivities();while(activity.MoveNext())yield return activity.Current;
        Require(Prefab&&Texts&&Clip,"native prefab, language and original PCM loaded");
        camera=new GameObject("MutedUiCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();camera.targetTexture=target;
        var metrics=ScreenMetrics.Bind(new Metrics());var screen=new ScreenAdaptService();screen.Initialize();
        var inactive=new GameObject("InactiveOriginalFrame");inactive.SetActive(false);
        var frame=Object.Instantiate(OfficialHomeScreenAssetCatalog.LoadDefault().framePrefab,inactive.transform,false);frame.SetActive(true);
        var rootCanvas=frame.GetComponent<Canvas>();rootCanvas.renderMode=RenderMode.ScreenSpaceCamera;rootCanvas.worldCamera=camera;rootCanvas.planeDistance=1;
        foreach(var canvas in frame.GetComponentsInChildren<Canvas>(true))canvas.worldCamera=camera;
        frame.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService=screen;
        foreach(var safe in frame.GetComponentsInChildren<SafeArea>(true))safe.ScreenAdaptService=screen;
        // A fixture slot makes this detached subtree visible; its original
        // transforms, anchor values, hierarchy and serialized fields remain intact.
        var slot=new GameObject("DetachedEntrySlot",typeof(RectTransform));slot.transform.SetParent(frame.transform.Find("Content"),false);
        var slotRect=(RectTransform)slot.transform;slotRect.anchorMin=slotRect.anchorMax=new Vector2(.5f,.5f);slotRect.sizeDelta=Vector2.zero;
        // Original LeftUpGo VerticalLayoutGroup consumes the entry's 104x176
        // LayoutElement; without a layout parent its serialized size is zero.
        var parentLayout=slot.AddComponent<VerticalLayoutGroup>();parentLayout.childAlignment=TextAnchor.UpperLeft;parentLayout.spacing=10;
        parentLayout.childControlWidth=parentLayout.childControlHeight=true;parentLayout.childForceExpandWidth=parentLayout.childForceExpandHeight=false;
        var parentFitter=slot.AddComponent<ContentSizeFitter>();parentFitter.horizontalFit=parentFitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var root=Object.Instantiate(Prefab,slot.transform,false);Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original download-entry script bindings resolve");
        var fonts=OfficialHomePlayerInfoAssetCatalog.LoadDefault().CreateFontService();fonts.Initialize();
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))fonts.SetupFont(text.font);
        var host=new DownloadServices();var control=root.GetComponent<PlayAsYouDownloadEntry>();control.Services=host;
        var tips=new Tips();foreach(var dot in root.GetComponentsInChildren<TipDotObject>(true))dot.TipDotSystem=tips;
        var textData=JsonUtility.FromJson<LanguageData>(Texts.text);var dictionary=textData.texts.ToDictionary(x=>x.key,x=>x.text);
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>dictionary.TryGetValue(key,out var value)?value:key;
        var previous=OfficialWwiseEvent.Host;var sound=new Sound();OfficialWwiseEvent.Host=sound;
        foreach(var buttonSound in root.GetComponentsInChildren<WorldButtonSound>(true))buttonSound.Post=(clip,emitter)=>{Require(clip==Clip,"original button PCM binding");sound.PostEvent(@SOUND_ID@u,emitter);};
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        inactive.SetActive(true);yield return null;yield return null;LayoutRebuilder.ForceRebuildLayoutImmediate(slotRect);Canvas.ForceUpdateCanvases();
        var button=Field<Button>(control,"mButton");var visible=Field<GameObject>(control,"mRoot");var red=Field<GameObject>(control,"mRedDotGo");var layout=Field<LayoutElement>(control,"mLayoutElement");
        Debug.Log("DOWNLOAD_LAYOUT_DIAGNOSTIC preferred="+layout.preferredWidth+","+layout.preferredHeight+" ignore="+layout.ignoreLayout+" active="+parentLayout.isActiveAndEnabled+" childCount="+slotRect.childCount+" rect="+((RectTransform)root.transform).rect+" groupControl="+parentLayout.childControlWidth+","+parentLayout.childControlHeight);
        Require(host.ProgressCount==1&&host.StateCount==1&&host.RewardCount==1&&host.ConditionCount==1,"enable binds one observer set and checkpoint");
        Require(visible.activeSelf&&!layout.ignoreLayout&&!red.activeSelf,"downloading entry visible without reward red dot");
        CheckProgress(control,.25f,"25%");Require(Field<WorldMultiLanguageText>(control,"mDownloadStateText").Text==dictionary["payd_downloading"],"original downloading language key");
        Capture("home-download-progress.png");Click(button);Require(host.Dialogs.SequenceEqual(new[]{true})&&sound.Posts==1,"native pointer opens closable dialog and original click sound");
        host.Current=150;host.EmitProgress();CheckProgress(control,1.5f,"150%");
        host.Total=0;host.EmitProgress();CheckProgress(control,1,"100%");
        host.Total=100;host.Current=25;host.State=PaydState.Downloaded;host.EmitState();
        Require(red.activeSelf&&Field<WorldMultiLanguageText>(control,"mDownloadStateText").Text==dictionary["payd_downloaded"],"downloaded state shows original reward red dot and text");CheckProgress(control,1,"100%");
        Capture("home-download-complete.png");
        var animation=root.GetComponent<Animation>();Require(animation&&animation.clip&&animation.clip.legacy&&animation.clip.wrapMode==WrapMode.Loop&&animation.isPlaying,"original legacy loop animation runs");
        var ripple=(RectTransform)root.transform.Find("Content/BgImg/RippleImg");var rippleStart=ripple.anchoredPosition;
        yield return new WaitForSeconds(.2f);Require((ripple.anchoredPosition-rippleStart).sqrMagnitude>.01f,"original ripple curve advances during normal frames");
        yield return new WaitForSeconds(animation.clip.length+.1f);Require(animation.isPlaying,"legacy loop remains playing beyond one original clip duration");
        host.Collected=true;host.EmitReward("OtherReward");Require(!visible.activeSelf&&layout.ignoreLayout,"original any-reward event refreshes actual once-reward status");
        Require(host.ProgressCount==1&&host.StateCount==1,"refresh hiding root retains original subscriptions until disable");
        root.SetActive(false);Require(host.AllCounts==0&&tips.OnTipDotShowChanged==null,"disable releases all subscriptions");
        host.Collected=false;host.Enabled=false;root.SetActive(true);yield return null;
        Require(!visible.activeSelf&&host.ConditionCount==1&&host.ProgressCount==0&&host.RewardCount==0,"disabled channel still registers original checkpoint");
        root.SetActive(false);host.Enabled=true;host.State=PaydState.Downloading;host.Met=true;host.Splash=false;root.SetActive(true);yield return null;
        Require(host.GlobalCount==0,"checkpoint waits for splash hide");host.Splash=true;yield return null;yield return null;
        Require(host.GlobalCount==1&&host.Dialogs.Last()==false&&!host.LastInstant,"checkpoint opens global non-instant dialog with close disabled");
        root.SetActive(false);host.State=PaydState.Downloaded;root.SetActive(true);yield return null;Require(host.GlobalCount==1,"already downloaded checkpoint does not reopen dialog");
        host.Collected=false;tips.Show=false;tips.OnTipDotShowChanged?.Invoke();
        Require(root.GetComponentsInChildren<TipDotObject>(true).All(x=>!Field<Image>(x,"mTipDotImage").enabled),"global tip setting updates original dot renderer");
        root.SetActive(false);Require(host.AllCounts==0,"repeated enable/disable leaves no observers");Object.Destroy(inactive);yield return null;
        screen.Dispose();metrics.Dispose();fonts.Dispose();OfficialWwiseEvent.Host=previous;sound.Dispose();Require(errors==0,"no Unity errors in exercised flows");
    }
    IEnumerator VerifyActivities()
    {
        var config=OfficialHomeRightEntryConfig.LoadDefault();var rows=config.Entries.GetSortedEntries();
        Require(rows.Count==25&&rows.Count(x=>!x.IsActivity)==11&&rows.Count(x=>x.IsActivity)==14,"exact selected official config counts");
        Require(rows.Zip(rows.Skip(1),(a,b)=>a.SortedId<b.SortedId).All(x=>x),"original parser sorts selected IDs");
        var host=new EntryServices(rows);var root=new GameObject("ActivityControllerLifecycleFixture");root.SetActive(false);
        var slot=new GameObject("OriginalEntryRootField",typeof(RectTransform));slot.transform.SetParent(root.transform,false);
        var controller=root.AddComponent<HomeUIActivityEntryController>();controller.Services=host;SetField(controller,"mEntryRoot",slot.transform);
        root.SetActive(true);yield return null;Require(slot.transform.childCount==11&&host.ObserverCount==4&&host.Loads==11,"ordinary entries instantiate with empty activity registry");
        host.EmitUpdate();host.EmitPreview(new Activity(10,217));Require(host.Loads==11,"refresh/preview do not duplicate entries");
        host.EmitOpen(new Activity(10,217));Require(host.Loads==11,"open event alone cannot create absent activity");
        var late=new Activity(10,226);host.Activities.Add(late.Id,late);host.EmitOpen(late);var early=new Activity(10,217);host.Activities.Add(early.Id,early);host.EmitOpen(early);yield return null;
        Require(slot.transform.childCount==13&&slot.transform.GetChild(2).name==rows.Single(x=>x.ActivityId==217).EntryPath,"late insertion retains SortedId sibling order");
        host.EmitOpen(new Activity(9,217));Require(host.Loads==13,"unrelated activity type ignored");
        host.EmitClose(early);yield return null;Require(slot.transform.childCount==12&&host.Disposals==0,"close destroys matching entry while handles remain in original enable-scope bag");
        host.Activities.Remove(early.Id);host.EmitUpdate();Require(host.Loads==13,"registry refresh preserves remaining activity");
        root.SetActive(false);yield return null;Require(host.ObserverCount==0&&slot.transform.childCount==0&&host.Disposals==13,"disable destroys entries and disposes every loaded handle");
        root.SetActive(true);yield return null;Require(host.ObserverCount==4&&slot.transform.childCount==12,"reenable restores one set using current records");
        root.SetActive(false);yield return null;Object.Destroy(root);host.Dispose();
        Debug.Log("HOME_HUD_ACTIVITY_LIFECYCLE_PASSED");
    }
    static void CheckProgress(PlayAsYouDownloadEntry control,float ratio,string text)
    {Require(Field<WorldMultiLanguageText>(control,"mProgress").Text==text,"original percentage text "+text);foreach(var rect in Field<List<RectTransform>>(control,"mProgressRectTransform"))Require(Mathf.Abs(rect.sizeDelta.y-(36+122*ratio))<.001f,"exact original progress height without clamping");}
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var rect=(RectTransform)button.transform;var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        if(hits.Count==0){Debug.Log("DOWNLOAD_BUTTON_RECT "+rect.rect+" active="+button.gameObject.activeInHierarchy+" interactable="+button.IsInteractable()+" parent="+((RectTransform)rect.parent).rect);foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))Debug.Log("DOWNLOAD_GRAPHIC "+graphic.name+" rect="+graphic.rectTransform.rect+" depth="+graphic.depth+" raycast="+graphic.raycastTarget+" cull="+graphic.canvasRenderer.cull+" canvas="+graphic.canvas+" alpha="+graphic.canvasRenderer.GetInheritedAlpha());}
        if(hits.Count==0)foreach(var raycaster in Object.FindObjectsOfType<GraphicRaycaster>())Debug.Log("DOWNLOAD_RAYCAST_DIAGNOSTIC "+raycaster.name+" active="+raycaster.isActiveAndEnabled+" camera="+raycaster.eventCamera+" pos="+data.position+" viewport="+(raycaster.eventCamera?raycaster.eventCamera.ScreenToViewportPoint(data.position):Vector3.zero)+" graphics="+GraphicRegistry.GetGraphicsForCanvas(raycaster.GetComponent<Canvas>()).Count);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native download button is first unobstructed raycast target: "+string.Join(",",hits.Take(8).Select(x=>x.gameObject.name)));
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
    static void SetField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(owner,value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    [Serializable] sealed class LanguageData{public LanguageRow[] texts;}[Serializable] sealed class LanguageRow{public string key,text;}
    sealed class Subscription:IDisposable{Action dispose;public Subscription(Action action){dispose=action;}public void Dispose(){var action=dispose;dispose=null;action?.Invoke();}}
    sealed class Activity:IHomeHudActivityData{public ActivityType ActivityType{get;}public int Id{get;}public Activity(int type,int id){ActivityType=(ActivityType)type;Id=id;}}
    sealed class EntryServices:IHomeHudRightEntryServices,IDisposable
    {
        public event Action<ActivityType> OnActivitiesUpdate;public event Action<IHomeHudActivityData> OnActivityPreview,OnActivityOpen,OnActivityClose;
        readonly IReadOnlyList<HomeRightEntryUIActivityInfo> rows;readonly GameObject template;public readonly Dictionary<int,IHomeHudActivityData> Activities=new Dictionary<int,IHomeHudActivityData>();public int Loads,Disposals;
        public EntryServices(IReadOnlyList<HomeRightEntryUIActivityInfo> values){rows=values;template=new GameObject("EntryPrefabFixture",typeof(RectTransform));template.SetActive(false);}
        public int ObserverCount=>(OnActivitiesUpdate?.GetInvocationList().Length??0)+(OnActivityPreview?.GetInvocationList().Length??0)+(OnActivityOpen?.GetInvocationList().Length??0)+(OnActivityClose?.GetInvocationList().Length??0);
        public IReadOnlyList<HomeRightEntryUIActivityInfo> GetSortedEntries()=>rows;
        public bool TryGetActivity(ActivityType type,int id,out IHomeHudActivityData value){if(Activities.TryGetValue(id,out value)&&value.ActivityType==type)return true;value=null;return false;}
        public bool TryGetActivityByType(ActivityType type,out Dictionary<int,IHomeHudActivityData> values){values=Activities.Where(x=>x.Value.ActivityType==type).ToDictionary(x=>x.Key,x=>x.Value);return values.Count>0;}
        public IHomeMenuPrefabHandle LoadPrefab(string path){Loads++;return new Handle(this,path);}
        public GameObject InstantiatePrefab(GameObject prefab,Transform parent){var result=Object.Instantiate(prefab,parent,false);result.name=pendingPath;return result;}string pendingPath;
        sealed class Handle:IHomeMenuPrefabHandle{readonly EntryServices host;public Handle(EntryServices owner,string path){host=owner;host.pendingPath=path;}public GameObject Result=>host.template;public void Dispose(){host.Disposals++;}}
        public void EmitUpdate()=>OnActivitiesUpdate?.Invoke(ActivityType.LinkageActivity);public void EmitPreview(IHomeHudActivityData value)=>OnActivityPreview?.Invoke(value);public void EmitOpen(IHomeHudActivityData value)=>OnActivityOpen?.Invoke(value);public void EmitClose(IHomeHudActivityData value)=>OnActivityClose?.Invoke(value);public void Dispose()=>Object.Destroy(template);
    }
    sealed class DownloadServices:IHomeHudDownloadServices,IHomeHudDownloadPlayer,IHomeHudDownloadHandler,IHomeHudCondition,IHomeHudConditionHandler,IHomeHudDownloadProgressDialog
    {
        Action<PlayAsYouDownloadProgressUpdateEvent> progress;Action<PlayAsYouDownloadStateUpdateEvent> state;Action conditions;
        public event Action<string> OnCollectOnceReward;public bool Enabled=true,Collected,Met,Splash=true;public long Current=25,Total=100;public PaydState State=PaydState.Downloading;
        public int ProgressCount=>progress?.GetInvocationList().Length??0;public int StateCount=>state?.GetInvocationList().Length??0;public int RewardCount=>OnCollectOnceReward?.GetInvocationList().Length??0;public int ConditionCount=>conditions?.GetInvocationList().Length??0;public int AllCounts=>ProgressCount+StateCount+RewardCount+ConditionCount;
        public IHomeHudDownloadPlayer Player=>this;public IHomeHudDownloadHandler PaydHandler=>this;public bool EnablePlayAsYouDownload=>Enabled;
        public bool HasCollectedOnceReward(string type){Require(type=="PlayAsYouDownload","original once reward key");return Collected;}
        public PaydState CurrentState=>State;public long CurrentDownloadBytes=>Current;public long TotalDownloadBytes=>Total;
        public IDisposable SubscribeProgress(Action<PlayAsYouDownloadProgressUpdateEvent> value){progress+=value;return new Subscription(()=>progress-=value);}public IDisposable SubscribeState(Action<PlayAsYouDownloadStateUpdateEvent> value){state+=value;return new Subscription(()=>state-=value);}
        public int CheckConditionId=>OfficialHomeRightEntryConfig.LoadDefault().CheckConditionId;public IHomeHudCondition GetCondition(int id){Require(id==CheckConditionId,"original selected checkpoint ID");return this;}public IHomeHudConditionHandler ConditionHandler=>this;public bool IsMet(IHomeHudCondition condition)=>Met;
        public void Register(IHomeHudCondition condition,Action callback){conditions+=callback;}public void Deregister(IHomeHudCondition condition,Action callback){conditions-=callback;}
        public Object MapRootIdentity=>null;public UniTask LeaveMap()=>throw new InvalidOperationException("Unused original leave-map helper must not be invoked.");public bool IsSplashHidden=>Splash;
        public readonly List<bool> Dialogs=new List<bool>();public int GlobalCount;public bool LastInstant;
        public void PopupProgress(Action<IHomeHudDownloadProgressDialog> initialize)=>initialize(this);
        public UniTask PopupProgressGlobal(Action<IHomeHudDownloadProgressDialog> initialize,bool instant){GlobalCount++;LastInstant=instant;initialize(this);return UniTask.CompletedTask;}
        public void Init(bool allowClickClose)=>Dialogs.Add(allowClickClose);public void EmitProgress()=>progress?.Invoke(default);public void EmitState()=>state?.Invoke(default);public void EmitReward(string key)=>OnCollectOnceReward?.Invoke(key);
    }
    sealed class Tips:IProfessionTipDotSystem
    {
        public bool Show=true;public bool IsShowTipDot=>Show;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;
        public bool LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback)=>throw new InvalidOperationException("No selector linker in original entry.");
        public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback)=>throw new InvalidOperationException("No selector linker in original entry.");
    }
    sealed class Sound:IProfessionSoundHost,IDisposable
    {
        AudioSource source;public int Posts;public uint PostEvent(uint id,GameObject emitter){Require(id==@SOUND_ID@u,"original button sound event");if(!source){source=new GameObject("MutedOriginalButtonVoice").AddComponent<AudioSource>();source.volume=0;source.playOnAwake=false;}source.PlayOneShot(Clip);Require(source.isPlaying,"native PCM is playing while muted");return (uint)++Posts;}public void Dispose(){if(source)Object.Destroy(source.gameObject);}
    }
}
