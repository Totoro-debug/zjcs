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
using Zjcs.Official.HomeSocialUiControls;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;
using TeamTargetCatalog=Zjcs.Official.HomeSocialUiControls.TeamTargetCatalog;

[InitializeOnLoad]
public static class HomeSocialUiNativeValidation
{
    const string Key="Zjcs.HomeSocialUiNativeValidation";
    static HomeSocialUiNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                HomeSocialUiNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("@MATCH_PREFAB@");
                HomeSocialUiNativeRunner.Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("@SOUND_GUID@"));
                HomeSocialUiNativeRunner.Texts=AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@");
                new GameObject("OriginalHomeMatchingFixture").AddComponent<HomeSocialUiNativeRunner>();
            }
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Home matching fixture timed out."));};
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
        else Debug.Log("HOME_SOCIAL_UI_NATIVE_PASSED: original 156-object matching panel, elapsed time, target filtering, original switch animations, pointer cancellation and lifecycle. Matching services are fixtures; production offline closure and full HomeUI assembly remain separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeSocialUiNativeRunner:MonoBehaviour
{
    public static GameObject Prefab;public static AudioClip Clip;public static TextAsset Texts;
    Camera camera;RenderTexture target;EventSystem events;int errors;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeSocialUiNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeSocialUiNativeValidation.Finish(null);
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
        var root=Object.Instantiate(Prefab,frame.transform.Find("Content"),false);
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original matching-panel scripts resolve");
        var fonts=OfficialHomePlayerInfoAssetCatalog.LoadDefault().CreateFontService();fonts.Initialize();
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))fonts.SetupFont(text.font);
        var dictionary=JsonUtility.FromJson<LanguageData>(Texts.text).texts.ToDictionary(x=>x.key,x=>x.text);
        dictionary.Add("Fixture.MatchTarget","测试匹配目标");LanguageManager.Initialize(new Language(dictionary));
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=key=>dictionary.TryGetValue(key,out var value)?value:key;
        var host=new MatchServices(dictionary);var control=root.GetComponent<GameMatchMiniSwitchPanel>();control.Services=host;
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var previousSound=OfficialWwiseEvent.Host;var sound=new Sound();OfficialWwiseEvent.Host=sound;
        foreach(var buttonSound in root.GetComponentsInChildren<WorldButtonSound>(true))buttonSound.Post=(clip,emitter)=>{Require(clip==Clip,"original button PCM binding");sound.PostEvent(@SOUND_ID@u,emitter);};
        inactive.SetActive(true);yield return null;yield return null;
        var content=Field<GameObject>(control,"mContentGo");var cancel=Field<Button>(control,"mCancelMatcBtn");
        Require(!content.activeSelf&&host.Observers==2&&Field<Coroutine>(control,"mCoroutine")==null,"no-match state hides content and binds one observer set without a counter");
        host.InMatch=true;host.EmitUpdate();yield return new WaitForSeconds(.25f);
        Require(content.activeSelf&&cancel.gameObject.activeSelf,"matching leader sees original content and cancel");
        var title=Field<WorldMultiLanguageText>(control,"mTitleText");Require(title.Text==string.Format(dictionary[title.myKey],dictionary[host.Target.GetItemName()]),"target title formats original language key with localized target name");
        Require(Field<WorldMultiLanguageText>(control,"mMatchTipText").Text==string.Format(dictionary["GameMatchPanel.MatchTips"],"01:05"),"target duration includes already-waited seconds");
        CheckTime(control,"00:17");Require(SwitchState(control)=="InstantToSimple","show resets original compact state");
        Capture("home-match-simple.png");
        var full=Field<Button>(control,"mFullContentBtn");Click(full);Require(SwitchState(control)=="SwitchToFull","native compact button dispatches full trigger");
        yield return new WaitForSeconds(.6f);Require(root.transform.Find("ContentGo/FullContent").gameObject.activeSelf,"original animator shows full panel");Capture("home-match-full.png");
        Click(Field<Button>(control,"mSimpleContentBtn"));Require(SwitchState(control)=="SwitchToSimple","native full button dispatches compact trigger");yield return new WaitForSeconds(.6f);
        host.Now=host.Start.AddSeconds(-20);yield return new WaitForSeconds(.2f);CheckTime(control,"00:00");
        host.Now=host.Start.AddSeconds(3661);yield return new WaitForSeconds(.2f);CheckTime(control,"01:06");
        host.Leader=false;host.EmitUpdate();Require(!cancel.gameObject.activeSelf,"nonleader cancel is hidden");
        host.Leader=true;host.EmitUpdate();Click(full);yield return new WaitForSeconds(.6f);
        Click(cancel);Require(host.StopCalls==1&&content.activeSelf,"cancel waits for operation success");host.Success(null);Require(!content.activeSelf,"successful cancellation hides content");
        host.EmitUpdate();yield return new WaitForSeconds(.25f);Require(content.activeSelf&&SwitchState(control)=="InstantToSimple","refresh restores matching compact state");
        var counter=Field<Coroutine>(control,"mCoroutine");host.Confirm();Require(!content.activeSelf&&ReferenceEquals(counter,Field<Coroutine>(control,"mCoroutine")),"confirm hides content while retaining original counter");
        SetField(control,"mTargetCatalog",new List<TeamTargetCatalog>{(TeamTargetCatalog)99999});host.EmitUpdate();Require(!content.activeSelf&&Field<Coroutine>(control,"mCoroutine")==null,"unmatched target stops counter");
        SetField(control,"mTargetCatalog",null);host.EmitUpdate();Require(content.activeSelf,"null target catalog accepts matching target");
        root.SetActive(false);Require(host.Observers==0&&Field<Coroutine>(control,"mCoroutine")==null,"disable releases observers and counter");
        host.InMatch=false;root.SetActive(true);yield return null;Require(host.Observers==2&&!content.activeSelf,"reenable installs one observer set with actual current state");
        root.SetActive(false);Object.Destroy(inactive);yield return null;Require(host.Observers==0,"destroy after disable leaves no matching observers");
        Require(sound.Posts>0,"native pointer plays original click event");OfficialWwiseEvent.Host=previousSound;sound.Dispose();fonts.Dispose();screen.Dispose();metrics.Dispose();Require(errors==0,"no Unity errors in exercised flows");
    }
    static void CheckTime(GameMatchMiniPanel control,string expected){Require(Field<WorldMultiLanguageText>(control,"mLeftTimeText").Text==expected&&Field<WorldMultiLanguageText>(control,"mSmallLeftTimeText").Text==expected,"both original elapsed-time labels equal "+expected);}
    static string SwitchState(object control){var state=Field<object>(control,"mShowStates");return state.GetType().GetProperty("State").GetValue(state).ToString();}
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var rect=(RectTransform)button.transform;var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native matching button is first unobstructed target: "+button.name+" hits="+string.Join(",",hits.Take(8).Select(x=>x.gameObject.name)));
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
    sealed class MatchTarget:IHomeMatchTarget{public TeamTargetCatalog TargetCatalog=>(TeamTargetCatalog)1;public string GetItemName()=>"Fixture.MatchTarget";}
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
    sealed class MatchServices:IHomeMatchServices,IHomeMatchPlayer,IHomeMatchInfo
    {
        event Action update,confirm;readonly Dictionary<string,string> dictionary;
        public readonly MatchTarget Target=new MatchTarget();public DateTime Start=new DateTime(2026,1,1),Now=new DateTime(2026,1,1).AddSeconds(12);public bool InMatch,Leader=true;public int StopCalls;public Action<object> Success;
        public MatchServices(Dictionary<string,string> texts){dictionary=texts;}
        public IHomeMatchPlayer Player=>this;public bool IsLeader=>Leader;public DateTime GetDateTime()=>Now;public string GetText(string key)=>dictionary[key];
        public IDisposable SubscribeConfirmStart(Action listener){confirm+=listener;return new Subscription(()=>confirm-=listener);}
        public void OperPlayerStopGameMatch(Action<object> onSuccess){StopCalls++;Success=onSuccess;}
        public event Action OnGameMatchUpdate{add{update+=value;}remove{update-=value;}}
        public bool IsInGameMatch=>InMatch;public IHomeMatchInfo GameMatchInfo=>this;public DateTime MatchStartTime=>Start;public DateTime MatchTargetTime=>Start.AddSeconds(60);public int AlreadyWaitSec=>5;public IHomeMatchTarget MatchTarget=>Target;
        public int Observers=>(update?.GetInvocationList().Length??0)+(confirm?.GetInvocationList().Length??0);public void EmitUpdate()=>update?.Invoke();public void Confirm()=>confirm?.Invoke();
    }
    sealed class Sound:IProfessionSoundHost,IDisposable
    {
        readonly GameObject root=new GameObject("MutedOriginalClick");public int Posts;
        public uint PostEvent(uint id,GameObject emitter){Require(id==@SOUND_ID@u,"original click event ID");Posts++;var source=root.GetComponent<AudioSource>();if(!source)source=root.AddComponent<AudioSource>();source.clip=Clip;source.volume=0;source.Play();Require(source.isPlaying,"original PCM playback starts while muted");return (uint)Posts;}
        public void Dispose()=>Object.Destroy(root);
    }
}
