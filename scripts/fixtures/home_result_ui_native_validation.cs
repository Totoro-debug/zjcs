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
using Zjcs.Official.HomeResultUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;
using StageType=Zjcs.Official.HomeResultUI.StageType;

[InitializeOnLoad]
public static class HomeResultUiNativeValidation
{
    const string Key="Zjcs.HomeResultUiNativeValidation";
    static HomeResultUiNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                HomeResultUiNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("@RESULT_PREFAB@");
                HomeResultUiNativeRunner.Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("@SOUND_GUID@"));
                HomeResultUiNativeRunner.Texts=AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@");
                new GameObject("OriginalHomeResultFixture").AddComponent<HomeResultUiNativeRunner>();
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
        else Debug.Log("HOME_RESULT_UI_NATIVE_PASSED: original 134-object result entry, internal pointer, ordered background-result filtering, popup/award completion, demon priority/clamping and lifecycle. Results/windows are fixtures; production HomeUI integration remains separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeResultUiNativeRunner:MonoBehaviour
{
    public static GameObject Prefab;public static AudioClip Clip;public static TextAsset Texts;
    Camera camera;RenderTexture target;EventSystem events;int errors;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeResultUiNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeResultUiNativeValidation.Finish(null);
    }
    void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
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
        var slot=new GameObject("DetachedOriginalLeftBotGo",typeof(RectTransform));slot.transform.SetParent(frame.transform.Find("Content"),false);
        var rect=(RectTransform)slot.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.zero;
        var layout=slot.AddComponent<VerticalLayoutGroup>();layout.childAlignment=TextAnchor.LowerLeft;layout.spacing=30;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;layout.childScaleWidth=layout.childScaleHeight=true;
        var fitter=slot.AddComponent<ContentSizeFitter>();fitter.horizontalFit=fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var root=Object.Instantiate(Prefab,slot.transform,false);Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original result-entry scripts resolve");
        var fonts=OfficialHomePlayerInfoAssetCatalog.LoadDefault().CreateFontService();fonts.Initialize();foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))fonts.SetupFont(text.font);
        var dictionary=JsonUtility.FromJson<LanguageData>(Texts.text).texts.ToDictionary(x=>x.key,x=>x.text);
        dictionary.Add("stage_103_name","测试关卡一");dictionary.Add("stage_104_name","测试关卡二");dictionary.Add("stage_105_name","回执后关卡");dictionary.Add("stage_DemonBattlefield_2","魔物测试 {0} 层");dictionary.Add("stage_VoidFissure_7","裂隙测试七层");
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>dictionary.TryGetValue(key,out var value)?value:key;
        var host=new Results(dictionary);var demon=root.GetComponent<FightDemonBattlefieldAutoPushEndNotifyUI>();demon.Services=host;
        foreach(var text in root.GetComponentsInChildren<GameCurrentStageText>(true)){text.Services=host;text.mFightRoot=host;}
        foreach(var notify in root.GetComponentsInChildren<GameEndNotifyUI>(true))notify.Services=host;
        var normal=demon.Button.GetComponent<GameEndNotifyUI>();Require(normal&&normal.GameCurrentStageText==demon.GameCurrentStageText,"original shared button and text references retained");
        var owner=new GameObject("DetachedOriginalResultHandler");owner.transform.SetParent(inactive.transform,false);var handler=owner.AddComponent<GameEndUIHandlerInHome>();handler.Services=host;handler.GameResultNotify=normal;handler.DemonBattlefieldAutoPushEndNotifyUI=demon;handler.BlackTypes=new List<StageType>{StageType.DemonBattlefield};
        host.Rows.Add(new Row(1,101,false));host.Rows.Add(new Row(2,102,true));host.Rows.Add(new Row(3,103,true));host.Rows.Add(new Row(4,104,true));
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();var previousSound=OfficialWwiseEvent.Host;var sound=new Sound();OfficialWwiseEvent.Host=sound;
        foreach(var buttonSound in root.GetComponentsInChildren<WorldButtonSound>(true))buttonSound.Post=(clip,emitter)=>{Require(clip==Clip,"original button PCM binding");sound.PostEvent(@SOUND_ID@u,emitter);};
        inactive.SetActive(true);yield return null;yield return null;LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();
        Require(host.Observers==2&&host.WindowObservers==1,"original enable installs player and window observers once");
        Require(host.FightObservers==1&&!Field<bool>(normal.GameCurrentStageText,"_Inited"),"uninitialized fight root subscribes before original UI initialization");
        host.EmitInitialized();Require(Field<bool>(normal.GameCurrentStageText,"_Inited")&&normal.GameCurrentStageText.myText.Text==dictionary["stage_103_name"],"fight initialized event runs original UIBase initialization");
        host.CurrentFight=new Fight(104);host.EmitInitialized();Require(normal.GameCurrentStageText.myText.Text==dictionary["stage_103_name"],"original GameUIBase retains first resolved fight cache");host.ActiveFight=false;
        Require(normal.gameObject.activeSelf&&Field<IHomeFightResult>(normal,"mResult").StageId==103&&normal.GameCurrentStageText.myText.Text==dictionary["stage_103_name"],"first eligible background result selected in original iteration order");
        Capture("home-result-notify.png");Click(normal.Button);Require(host.Popups==1&&host.InitializedResult.StageId==103&&host.Awards.Count==0,"pointer initializes result window before receipt completion");
        host.Complete();Require(host.Awards.SequenceEqual(new long[]{3})&&!host.LastInvokeByPlayer,"completion receives actual pending result with invokeByPlayer false");
        Require(Field<IHomeFightResult>(normal,"mResult").StageId==104,"successful result removal refreshes next eligible entry");
        Click(normal.Button);Require(host.Popups==1&&host.Awards.SequenceEqual(new long[]{3,4}),"stage without end panel receives award directly");
        Require(!normal.gameObject.activeSelf,"foreground and blacklisted results alone hide ordinary notification");
        host.Rows.Clear();host.Demon=new BackPush(2,999);host.EmitAdd();yield return null;
        Require(demon.StateSwitcher.State&&normal.gameObject.activeSelf&&normal.GameCurrentStageText.myText.Text=="魔物测试 5 层"&&host.LastLayer==5,"demon result takes priority and clamps next layer to original maximum");
        Capture("home-result-demon.png");Click(demon.Button);Require(host.HandleCalls==1&&host.RequestKey==77&&host.DemonPopups==0,"original demon pointer waits for operation success");
        host.Success(null);Require(host.DemonPopups==1&&ReferenceEquals(host.InitializedDemon,host.Demon),"success initializes original demon result payload");
        host.Rows.Add(new Row(5,105,true));host.Demon=null;host.CloseDemon();Require(!demon.StateSwitcher.State&&Field<IHomeFightResult>(normal,"mResult").StageId==105,"closing last demon result returns to normal background notification");
        host.Demon=new BackPush(2,-4);Require(demon.TryRefreshEntry()&&host.LastLayer==1,"original lower clamp keeps next demon layer at one");host.Demon=null;host.CloseDemon();
        var stageText=normal.GameCurrentStageText;stageText.ShowStageText(3007);Require(stageText.myText.Text=="裂隙测试七层","original void layer language branch");host.NullPart=true;stageText.ShowStageText(3007);Require(stageText.myText.Text=="裂隙测试七层","missing original stage part retains prior text");host.NullPart=false;
        owner.SetActive(false);Require(host.Observers==0&&Field<IHomeFightResult>(normal,"mResult")==null,"handler disable releases player observers and clears result payload");owner.SetActive(true);Require(host.Observers==2&&Field<IHomeFightResult>(normal,"mResult").StageId==105,"reenable refreshes current records without duplicate observers");
        inactive.SetActive(false);Require(host.Observers==0&&host.WindowObservers==0&&Field<IHomeDemonBackPushResult>(demon,"mResult")==null,"disable releases window subscription and demon payload");Object.Destroy(inactive);yield return null;Require(host.FightObservers==0,"destroy releases original fight initialization observer");
        Require(sound.Posts>0,"original result pointer sound plays muted");OfficialWwiseEvent.Host=previousSound;sound.Dispose();fonts.Dispose();screen.Dispose();metrics.Dispose();Require(errors==0,"no Unity errors in exercised result flows");
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var rect=(RectTransform)button.transform;var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native result button is first unobstructed target: "+button.name+" hits="+string.Join(",",hits.Take(8).Select(x=>x.gameObject.name)));
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static FieldInfo FindField(object owner,string name){for(var type=owner.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(field!=null)return field;}throw new MissingFieldException(name);}
    static T Field<T>(object owner,string name)=>(T)FindField(owner,name).GetValue(owner);
    static void SetField(object owner,string name,object value)=>FindField(owner,name).SetValue(owner,value);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    [Serializable] sealed class LanguageData{public LanguageRow[] texts;}[Serializable] sealed class LanguageRow{public string key,text;}
    sealed class Subscription:IDisposable{Action dispose;public Subscription(Action action){dispose=action;}public void Dispose(){var action=dispose;dispose=null;action?.Invoke();}}
    sealed class Row:IHomePlayerGameResult,IHomeStoredFightResult,IHomeFightResult
    {
        public readonly long Id;public int StageId{get;}public bool IsBackground{get;}public Row(long id,int stage,bool background){Id=id;StageId=stage;IsBackground=background;}
        public IHomeStoredFightResult Result=>this;public IHomeFightResult Data=>this;
    }
    sealed class BackPush:IHomeDemonBackPushResult{public int EntryIndex{get;}public int EndLayer{get;}public BackPush(int entry,int end){EntryIndex=entry;EndLayer=end;}}
    sealed class Fight:IHomeResultFight,IHomeResultFightComponent{public int StageId{get;}public Fight(int stage){StageId=stage;}public IHomeResultFightComponent Component=>this;}
    sealed class Stage:IHomeResultStage
    {
        readonly bool noPart;public int Id{get;}public Stage(int id,bool noPart){Id=id;this.noPart=noPart;}public StageType Type=>Id==102||Id>=2000&&Id<3000?StageType.DemonBattlefield:Id==3007?StageType.VoidFissure:StageType.Main;public bool ShowEndPanel=>Id!=104;
        public T GetPartInfo<T>() where T:class=>noPart?null:typeof(T)==typeof(StagePartInfoDemonBattlefield)?new StagePartInfoDemonBattlefield(2,Id-2000) as T:typeof(T)==typeof(StagePartInfoVoidFissure)?new StagePartInfoVoidFissure(7) as T:null;
    }
    sealed class Results:IHomeResultServices,IHomeResultPlayer,IHomeDemonBackPushData,IHomeResultStageParser,IHomeResultDemonParser,IHomeResultWindows,IHomeResultWindowHandle,IHomeGameEndPanel,IHomeDemonResultPanel,IHomeResultFightRoot
    {
        readonly Dictionary<string,string> texts;public Results(Dictionary<string,string> values){texts=values;}
        public readonly List<Row> Rows=new List<Row>();public readonly List<long> Awards=new List<long>();public BackPush Demon;public int Popups,DemonPopups,HandleCalls,LastLayer;public bool LastInvokeByPlayer,NullPart;public ulong RequestKey;public Action<object> Success;public IHomeFightResult InitializedResult;public IHomeDemonBackPushResult InitializedDemon;
        Action<object> added;Action<long> removed;Action<GameObject> closed,complete;
        public IHomeResultPlayer Player=>this;public IHomeResultStageParser StageInfoParser=>this;public IHomeResultDemonParser StagePartInfoDemonBattlefieldParser=>this;public IHomeResultWindows Windows=>this;
        public string GetText(string key)=>texts.TryGetValue(key,out var value)?value:key;public string GetTextFormated(string key,params object[] args)=>string.Format(GetText(key),args);
        public Type GetGameEndPanelType(IHomeResultStage stage)=>typeof(GameObject);public IHomeResultStage Get(int id)=>new Stage(id,NullPart);
        public int GetMaxLayer(int entryIndex){Require(entryIndex==2,"original demon entry index");return 5;}public IHomeResultStage GetInfoByIndexAndLayer(int entryIndex,int layer){LastLayer=layer;return Get(2000+layer);}
        public event Action<object> OnGameResultsAdd{add{added+=value;}remove{added-=value;}}public event Action<long> OnGameResultsRemove{add{removed+=value;}remove{removed-=value;}}
        public IEnumerable<KeyValuePair<long,IHomePlayerGameResult>> GameResultManager=>Rows.Select(row=>new KeyValuePair<long,IHomePlayerGameResult>(row.Id,row));public IHomeDemonBackPushData StageDemonBattlefieldData=>this;
        public bool HasGameResultByResultId(long id)=>Rows.Any(row=>row.Id==id);public bool TryGetOlderBackPushResult(out ulong key,out IHomeDemonBackPushResult result){key=Demon==null?0ul:77ul;result=Demon;return Demon!=null;}
        public void OperPlayerReceivedGameAward(long resultId,bool invokeByPlayer){LastInvokeByPlayer=invokeByPlayer;Awards.Add(resultId);Rows.RemoveAll(row=>row.Id==resultId);removed?.Invoke(resultId);}
        public void OperPlayerDemonBattlefieldHandleBackPushResult(ulong key,Action<object> success){HandleCalls++;RequestKey=key;Success=success;}
        public event Action<GameObject> OnWindowClosed{add{closed+=value;}remove{closed-=value;}}public event Action<GameObject> OnComplete{add{complete+=value;}remove{complete-=value;}}
        public IHomeResultWindowHandle Popup(Type type,Action<GameObject> initialize){Popups++;var go=new GameObject("ResultWindowFixture");initialize(go);Object.Destroy(go);return this;}
        public bool TryGetEndPanel(GameObject go,out IHomeGameEndPanel panel){panel=this;return true;}public void InitMsg(IHomeFightResult result){InitializedResult=result;}
        public void PopupDemon(Action<IHomeDemonResultPanel> initialize){DemonPopups++;initialize(this);}public void Init(IHomeDemonBackPushResult result){InitializedDemon=result;}
        public bool IsDemonResultPanel(GameObject go)=>true;public void Complete(){var listeners=complete;complete=null;listeners?.Invoke(null);}public void CloseDemon()=>closed?.Invoke(null);public void EmitAdd()=>added?.Invoke(null);
        public int Observers=>(added?.GetInvocationList().Length??0)+(removed?.GetInvocationList().Length??0);public int WindowObservers=>closed?.GetInvocationList().Length??0;
        Action initialized;public Fight CurrentFight=new Fight(103);public bool ActiveFight=true;
        public bool HasFightRoot=>true;public bool Initialized{get;private set;}public bool InFight=>ActiveFight;public IHomeResultFight Fight=>CurrentFight;public event Action OnInitialized{add{initialized+=value;}remove{initialized-=value;}}
        public int FightObservers=>initialized?.GetInvocationList().Length??0;public void EmitInitialized(){Initialized=true;initialized?.Invoke();}
    }
    sealed class Sound:IProfessionSoundHost,IDisposable
    {
        readonly GameObject root=new GameObject("MutedOriginalClick");public int Posts;
        public uint PostEvent(uint id,GameObject emitter){Require(id==@SOUND_ID@u,"original click event ID");Posts++;var source=root.GetComponent<AudioSource>();if(!source)source=root.AddComponent<AudioSource>();source.clip=Clip;source.volume=0;source.Play();Require(source.isPlaying,"original PCM playback starts while muted");return (uint)Posts;}
        public void Dispose()=>Object.Destroy(root);
    }
}
