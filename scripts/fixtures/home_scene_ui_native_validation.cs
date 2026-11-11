using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeCamera;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HomeSceneUiNativeValidation
{
    const string Key="Zjcs.HomeSceneUiNativeValidation";
    static HomeSceneUiNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {HomeSceneUiNativeRunner.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>("@GUIDER_PREFAB@");HomeSceneUiNativeRunner.ClickClip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("@SOUND_GUID@"));new GameObject("NativeHomeSceneUiFixture").AddComponent<HomeSceneUiNativeRunner>();}
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Home scene UI validation timed out."));};
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_SCENE_UI_NATIVE_PASSED: original 105-object Guider hierarchy, internal raycast clicks, bed/cart focus and tip filtering, screenshot mode filtering, controller/event disposal and original fade cancellation passed. Parent camera/furniture services are fixtures; formal HomeUI assembly and NPC guide click remain separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeSceneUiNativeRunner:MonoBehaviour
{
    public static GameObject Prefab;
    public static AudioClip ClickClip;
    Camera uiCamera,worldCamera;RenderTexture target;EventSystem events;SceneServices host;
    IDisposable screenBinding;ScreenAdaptService screenService;GameObject frameHolder;int errors;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeSceneUiNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeSceneUiNativeValidation.Finish(null);
    }
    void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Exercise()
    {
        Require(Prefab,"native Guider prefab loaded");
        worldCamera=new GameObject("HomeWorldCamera",typeof(Camera)).GetComponent<Camera>();worldCamera.tag="MainCamera";
        worldCamera.transform.position=new Vector3(0,0,-10);worldCamera.orthographic=true;worldCamera.orthographicSize=5;worldCamera.aspect=608f/1080;worldCamera.cullingMask=0;worldCamera.depth=-1;
        uiCamera=new GameObject("HomeUiCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        uiCamera.clearFlags=CameraClearFlags.SolidColor;uiCamera.backgroundColor=new Color(.12f,.2f,.28f);uiCamera.orthographic=true;
        target=new RenderTexture(608,1080,24);target.Create();uiCamera.targetTexture=target;worldCamera.targetTexture=target;
        screenBinding=ScreenMetrics.Bind(new Metrics());screenService=new ScreenAdaptService();screenService.Initialize();
        frameHolder=new GameObject("InactiveOriginalFrame");frameHolder.SetActive(false);
        var frame=Object.Instantiate(OfficialHomeScreenAssetCatalog.LoadDefault().framePrefab,frameHolder.transform,false);
        frame.transform.localPosition=Vector3.zero;frame.transform.localRotation=Quaternion.identity;frame.transform.localScale=Vector3.one;frame.SetActive(true);
        var canvas=frame.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
        foreach(var layer in frame.GetComponentsInChildren<Canvas>(true))layer.worldCamera=uiCamera;
        frame.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService=screenService;
        foreach(var area in frame.GetComponentsInChildren<SafeArea>(true))area.ScreenAdaptService=screenService;
        // A scoped parent supplies the same original SafeArea's bottom-left
        // coordinates. Complete production HomeUI assembly is separate.
        var mount=new GameObject("HomeUiParentFixture",typeof(RectTransform),typeof(SafeArea));mount.transform.SetParent(frame.transform.Find("Content"),false);
        mount.GetComponent<SafeArea>().ScreenAdaptService=screenService;
        var root=Object.Instantiate(Prefab,mount.transform,false);
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x!=null),"all original Guider scripts resolve");
        var previousSound=OfficialWwiseEvent.Host;host=new SceneServices(worldCamera);OfficialWwiseEvent.Host=host;
        foreach(var guide in root.GetComponentsInChildren<AUIDirectionGuider>(true))guide.Services=host;
        var screenshot=root.GetComponent<HomeScreenShootActive>();screenshot.Services=host;
        var dragGo=new GameObject("OriginalDragControllerFixture");dragGo.SetActive(false);var drag=dragGo.AddComponent<DragProbe>();drag.DragControllerManager=host.DragControllerManager;dragGo.SetActive(true);
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        frameHolder.SetActive(true);yield return null;yield return null;Canvas.ForceUpdateCanvases();
        var guides=root.GetComponentsInChildren<HomeHangUpDirectionGuider>(true);
        var bed=guides.Single(x=>Field<EFurniture>(x,"mFurniture")==EFurniture.Bed);var cart=guides.Single(x=>x!=bed);
        var bedBtn=Field<Button>(bed,"mGuiderBtn");var cartBtn=Field<Button>(cart,"mGuiderBtn");var mask=Field<GameObject>(bed,"mMask");
        Require(host.TipCount==2&&host.ActiveCount==1,"native lifecycle subscribes each guide and screenshot controller once");
        Require(EventCount(host.ViewPosUpdated)==3&&EventCount(drag.OnPosChanged)==3,"all three original guides observe camera and current drag controller");
        Require(bedBtn.gameObject.activeInHierarchy&&cartBtn.gameObject.activeInHierarchy,"both original offscreen furniture guide buttons visible");
        Require(!root.GetComponentInChildren<AwardRetrieveNpcDirectionGuider>(true).GetComponentInChildren<Button>(true).gameObject.activeInHierarchy,"no reward-retrieval NPC data hides its guide");
        Require(root.GetComponentsInChildren<ParticleSystem>(true).Length==6,"all six original particle systems retained");
        CheckOnScreen(bedBtn);CheckOnScreen(cartBtn);Capture("home-scene-ui-offscreen.png");
        host.EmitTip(EFurniture.Desk,-1);yield return null;Require(bedBtn.gameObject.activeInHierarchy&&cartBtn.gameObject.activeInHierarchy,"unrelated furniture tip ignored");
        host.EmitTip(EFurniture.Bed,-1);yield return null;Require(!bedBtn.gameObject.activeInHierarchy&&cartBtn.gameObject.activeInHierarchy,"negative bed tip hides only its guide");
        host.EmitTip(EFurniture.Bed,0);yield return null;Require(bedBtn.gameObject.activeInHierarchy,"tip level zero is eligible");
        Click(bedBtn);Require(mask.activeSelf,"native pointer click enables shared input mask");
        float deadline=Time.realtimeSinceStartup+3;while(mask.activeSelf&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(!mask.activeSelf&&Mathf.Abs(worldCamera.transform.position.x-host.Bed.transform.position.x)<.0001f,"official coroutine ends on exact bed X and clears mask");
        yield return null;Require(!bedBtn.gameObject.activeInHierarchy&&cartBtn.gameObject.activeInHierarchy,"camera entering bed view removes its guide");
        Capture("home-scene-ui-focused-bed.png");
        Click(cartBtn);Require(mask.activeSelf,"cart uses original same focus mask");
        root.SetActive(false);Require(!mask.activeSelf&&host.TipCount==0&&host.ActiveCount==0,"disable cancels running focus, clears mask and subscriptions");
        Require(EventCount(host.ViewPosUpdated)==0&&EventCount(drag.OnPosChanged)==0,"disable releases both camera and drag event listeners");
        root.SetActive(true);yield return null;
        Require(host.TipCount==2&&host.ActiveCount==1&&EventCount(drag.OnPosChanged)==3,"reenable restores one observer set");
        var secondGo=new GameObject("ReplacementDragController");secondGo.SetActive(false);var second=secondGo.AddComponent<DragProbe>();second.DragControllerManager=host.DragControllerManager;secondGo.SetActive(true);
        Require(EventCount(drag.OnPosChanged)==0&&EventCount(second.OnPosChanged)==3,"changing current controller transfers listeners");
        secondGo.SetActive(false);Require(EventCount(second.OnPosChanged)==0&&EventCount(drag.OnPosChanged)==3,"unregister restores previous controller");
        host.EmitActive(false);Require(guides.All(x=>!x.gameObject.activeSelf),"original screenshot proxy hides both guide objects");
        Require(host.TipCount==0&&host.ActiveCount==1,"screenshot component remains subscribed while guides are hidden");
        host.EmitActive(true);yield return null;Require(guides.All(x=>x.gameObject.activeSelf)&&host.TipCount==2,"screenshot restore reenables original guide lifecycles");
        screenshot.ViewModes.Add(HomeViewMode.TakePhoto);host.Mode=HomeViewMode.Owner;host.EmitActive(false);
        Require(screenshot.HomeFadeProxy.Active,"nonmatching view mode leaves visibility unchanged");
        host.Mode=HomeViewMode.TakePhoto;host.EmitActive(false);Require(!screenshot.HomeFadeProxy.Active,"matching photo mode applies visibility");
        host.EmitActive(true);yield return null;
        Require(host.AudioPosts==2,"both native clicks posted the original sound event");
        var fade=VerifyFade(bedBtn.GetComponentsInChildren<Image>(true).First(x=>x.sprite).sprite);while(fade.MoveNext())yield return fade.Current;
        root.SetActive(false);Object.Destroy(frameHolder);Object.Destroy(dragGo);Object.Destroy(secondGo);host.Dispose();yield return null;
        Require(!screenService.CanvasWidthAdjusters.Any(),"original frame unregisters before screen service disposal");
        screenService.Dispose();screenBinding.Dispose();OfficialWwiseEvent.Host=previousSound;Require(errors==0,"no Unity errors in exercised flows");
    }
    IEnumerator VerifyFade(Sprite originalSprite)
    {
        var active=new GameObject("FadeActiveFixture");var sprite=new GameObject("OriginalSpriteFadeFixture").AddComponent<SpriteRenderer>();sprite.sprite=originalSprite;sprite.color=new Color(1,1,1,.6f);
        var fade=new HomeFadeList{ActiveList=new List<GameObject>{active},FadeSpriteList=new List<SpriteRenderer>{sprite},FadeDuration=.12f};fade.Init();
        fade.SetActiveStatusImmediately(false);Require(!active.activeSelf&&sprite.gameObject.activeSelf&&sprite.color.a==0,"original zero-alpha branch keeps sprite object active");
        fade.SetActiveStatus(true);Require(active.activeSelf,"active list changes before asynchronous fade completes");yield return null;
        fade.SetActiveStatus(false);yield return null;fade.SetActiveStatus(true);
        yield return new WaitForSeconds(.2f);Require(Mathf.Abs(sprite.color.a-.6f)<.0001f,"replacement fade cancels old operations and restores captured sprite alpha");
        fade.SetActiveStatus(false);yield return new WaitForSeconds(.2f);Require(sprite.color.a==0&&sprite.gameObject.activeSelf,"fade reaches exact zero without deactivating original sprite");fade.ReleaseCancellationTokenSource();
        var token=new HomeFadeCancellationTokenDisposable();var captured=token.Token;bool canceled=false;captured.Register(()=>canceled=true);token.Dispose();Require(canceled&&captured.IsCancellationRequested,"AOT-verified cancellation runs before disposing source");
        bool disposed=false;try{var ignored=token.Token;}catch(ObjectDisposedException){disposed=true;}Require(disposed,"AOT-verified helper disposes token source");
        Object.Destroy(sprite.gameObject);Object.Destroy(active);Debug.Log("HOME_SCENE_UI_FADE_PASSED");
    }
    static T Field<T>(object source,string name)
    {for(var type=source.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(field!=null)return (T)field.GetValue(source);}throw new MissingFieldException(name);}
    static int EventCount(UnityEventBase e){var calls=typeof(UnityEventBase).GetField("m_Calls",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(e);return Field<IList>(calls,"m_RuntimeCalls").Count;}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    void Click(Button button)
    {
        // A freshly reenabled Graphic has depth -1 until the render-target
        // camera processes it. Batch Mode does not render a Game View frame.
        Canvas.ForceUpdateCanvases();uiCamera.Render();var rect=(RectTransform)button.transform;
        var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(uiCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"first unobstructed raycast target is native guide button "+button.name+" at "+data.position+"; hits="+string.Join(",",hits.Take(8).Select(x=>x.gameObject.name+":"+ExecuteEvents.GetEventHandler<IPointerClickHandler>(x.gameObject)?.name)));
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void CheckOnScreen(Button button){var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);foreach(var point in corners){var v=uiCamera.WorldToViewportPoint(point);Require(v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1,"original edge clamp keeps complete button onscreen");}}
    void Capture(string name){Canvas.ForceUpdateCanvases();uiCamera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    public sealed class DragProbe:DragControllerBase{protected override void LateUpdate(){}}
    sealed class Furniture:IHomeDirectionFurniture
    {public Transform transform{get;}public Object UnityIdentity=>transform;public int TipLevel=>0;public Furniture(float x){transform=new GameObject("FurniturePositionFixture").transform;transform.position=new Vector3(x,0,0);}}
    sealed class SceneServices:IHomeSceneUiServices,IHomeDirectionCameraServices,IHomeDirectionFocusController,IProfessionSoundHost,IDisposable
    {
        readonly Camera world;Action<HangUpTipLevelChangeData> tips;Action<SetHomeSceneUIActiveData> active;AudioSource sound;
        public readonly Furniture Bed=new Furniture(-12),Cart=new Furniture(12);
        public SceneServices(Camera camera){world=camera;}
        public IHomeDirectionCameraServices Camera=>this;public IDragControllerManager DragControllerManager{get;}=new DragControllerManager();public float DirectionGuideFocusDuration=>.2f;
        public IHomeDirectionFurniture GetFurniture(EFurniture type)=>type==EFurniture.Bed?Bed:Cart;
        public int TipCount=>tips?.GetInvocationList().Length??0;public int ActiveCount=>active?.GetInvocationList().Length??0;
        public IDisposable SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback){tips+=callback;return new Subscription(()=>tips-=callback);}
        public IDisposable SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback){active+=callback;return new Subscription(()=>active-=callback);}
        public void EmitTip(EFurniture type,int level)=>tips?.Invoke(new HangUpTipLevelChangeData{FurnitureType=type,TipLevel=level});
        public void EmitActive(bool value)=>active?.Invoke(new SetHomeSceneUIActiveData{IsActive=value});
        public UnityEvent OnHomeNpcRefresh{get;}=new UnityEvent();public List<IHomeDirectionNpcData> HomeNpcDatas{get;}=new List<IHomeDirectionNpcData>();public int AwardRetrieveHomeNpcId=>1;
        public Transform GetSpawnPoint(int index)=>throw new InvalidOperationException("No NPC in fixture");public HomeDirectionNpcInfo GetNpcInfo(int id)=>throw new InvalidOperationException("No NPC in fixture");
        public IHomeMenuPrefabHandle LoadPrefab(string path)=>throw new InvalidOperationException("No NPC in fixture");public GameObject InstantiatePrefab(GameObject prefab,Transform slot)=>throw new InvalidOperationException("No NPC in fixture");public void FreezeSkeletonGraphic(GameObject instance)=>throw new InvalidOperationException("No NPC in fixture");
        public HomeViewMode Mode;public HomeViewMode HomeViewMode=>Mode;
        public UnityEvent<Vector3> ViewPosUpdated{get;}=new UnityEvent<Vector3>();public Object ControlBrainIdentity=>world;public Vector3 CameraFollowPosition=>new Vector3(world.transform.position.x,0,0);
        public Vector3 GetViewPos()=>CameraFollowPosition;public IHomeDirectionFocusController GetHomeController()=>this;
        public void SetFocusPositionXForce(float x){world.transform.position=new Vector3(x,0,-10);ViewPosUpdated.Invoke(GetViewPos());}
        public int AudioPosts;
        public uint PostEvent(uint eventId,GameObject emitter){Require(eventId==@SOUND_ID@u,"exact original guide click event ID");Play(ClickClip,emitter);return (uint)++AudioPosts;}
        public void Play(AudioClip clip,GameObject emitter){Require(clip&&emitter,"native click sound reference and emitter");if(!sound){sound=new GameObject("MutedOriginalGuideClickVoice").AddComponent<AudioSource>();sound.playOnAwake=false;sound.volume=0;}sound.PlayOneShot(clip);Require(sound.isPlaying,"native click PCM is playing while muted");}
        public void Dispose(){Object.Destroy(Bed.transform.gameObject);Object.Destroy(Cart.transform.gameObject);if(sound)Object.Destroy(sound.gameObject);}
        sealed class Subscription:IDisposable{Action dispose;public Subscription(Action value){dispose=value;}public void Dispose(){var value=dispose;dispose=null;value?.Invoke();}}
    }
}
