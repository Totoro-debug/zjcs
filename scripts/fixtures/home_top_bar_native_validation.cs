using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeTopBarNativeValidation
{
    private const string Active="HomeTopBarNativeValidation";
    static HomeTopBarNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active,false))
                new GameObject("HomeTopBarFixture").AddComponent<HomeTopBarNativeRunner>(); };
        EditorApplication.update += () => {
            if(SessionState.GetBool(Active,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+"Deadline",float.MaxValue))
                Finish(new TimeoutException("Original top-bar validation timeout.")); };
    }
    public static void Run()
    {
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);
        SessionState.SetFloat(Active+"Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if(error!=null) Debug.LogException(error);
        else Debug.Log("HOME_TOP_BAR_NATIVE_PASSED: selected original hierarchy, three currency IDs/icons, internal pointer raycasts, count updates, limit colors, pause/resume, empty/update and pool return/reborrow. Fixture player services; production home integration remains separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeTopBarNativeRunner:MonoBehaviour,IPlayerItemUIHost
{
    private OfficialHomeTopBarAssetCatalog assets;
    private OfflineHomeTopBar view;
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private int errors,activeProviders,descriptionItem,addItem;
    private Vector2 inputPosition;
    private readonly List<CountProvider> providers=new List<CountProvider>();
    private readonly Dictionary<int,long> counts=new Dictionary<int,long>{{1,1234},{2,567},{57,40}};
    private void OnLog(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++; }
    private IEnumerator Start()
    {
        Application.logMessageReceived+=OnLog;
        AudioListener.volume=0;
        var steps=Exercise();
        for(;;) {
            bool next; object current=null;
            try {next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error) {HomeTopBarNativeValidation.Finish(error);yield break;}
            if(!next)break; yield return current;
        }
        HomeTopBarNativeValidation.Finish(null);
    }
    private IEnumerator Exercise()
    {
        assets=OfficialHomeTopBarAssetCatalog.LoadDefault();
        Require(assets.currencies.Select(x=>x.itemId).SequenceEqual(new[]{1,2,57}),"selected factory IDs, not enum values");
        var cameraObject=new GameObject("TopBarCamera",typeof(Camera));cameraUi=cameraObject.GetComponent<Camera>();
        cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);cameraUi.orthographic=true;
        target=new RenderTexture(Screen.width,Screen.height,24);cameraUi.targetTexture=target;
        var canvasObject=new GameObject("NativeTopBarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        Require(Object.FindObjectsOfType<EventSystem>().Length==0,"sole isolated event system");
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("HomeValidationFont"));
        view=new OfflineHomeTopBar(canvas.transform,this,font,key=>key,assets);
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        Require(Screen.width==target.width && Screen.height==target.height,"capture dimensions must match the actual editor safe area");
        var items=Items();Require(items.Length==3 && activeProviders==3,"three live original currency widgets");
        Require(items.Select(x=>Field<Image>(x,"mIcon").sprite).SequenceEqual(assets.currencies.Select(x=>x.icon)),"original currency icon binding");
        Require(items.Select(Text).SequenceEqual(new[]{"1234","567","40/300"}),"count and auto-increase maximum presentation");
        foreach(var item in items) {
            var corners=new Vector3[4];((RectTransform)item.transform).GetWorldCorners(corners);
            foreach(var corner in corners) {
                var point=cameraUi.WorldToViewportPoint(corner);
                Require(point.x>=0 && point.x<=1 && point.y>.9f && point.y<=1,"complete native top bar must remain in the upper viewport");
            }
        }
        Capture("home-top-bar-native.png");
        foreach(var item in items) {
            var button=Field<Button>(item,"mBtn");Click(button);
            Require(descriptionItem==assets.currencies[Array.IndexOf(items,item)].itemId,"original description pointer routing");
        }
        Click(Field<Button>(items[0],"mAddBtn"));Require(addItem==1,"original quick-buy add pointer routing");
        Require(!Field<Button>(items[1],"mAddBtn").gameObject.activeSelf,"confirmed offline recharge hidden");
        Require(!Field<Button>(items[2],"mAddBtn").gameObject.activeSelf,"default stamina handler has no add button");
        counts[1]=4321;Notify(1);Require(Text(items[0])=="4321","event updates visible value");
        view.Handler.SetStopUpdateCount(true);counts[1]=5000;Notify(1);Require(Text(items[0])=="4321","paused count remains unchanged");
        view.Handler.SetStopUpdateCount(false);Require(Text(items[0])=="5000","resuming reads latest provider value");
        counts[57]=300;Notify(57);Require(Text(items[2]).Contains("300/"),"limit count refresh");
        Capture("home-top-bar-limit.png");
        var identities=items.Select(x=>x.GetInstanceID()).OrderBy(x=>x).ToArray();
        view.Root.SetActive(false);Require(activeProviders==0,"return releases all player subscriptions");
        view.Root.SetActive(true);yield return null;
        Require(activeProviders==3 && identities.SequenceEqual(Items().Select(x=>x.GetInstanceID()).OrderBy(x=>x)),"reborrow reuses original pool objects without subscriptions leaking");
        view.Handler.UpdateEmptyCurrency();Require(activeProviders==0 && Items().Length==0,"empty currency returns all items");
        view.Handler.UpdateCurrencyData(new List<PlayerItemType>{PlayerItemType.TiliId,PlayerItemType.Money});yield return null;
        Require(activeProviders==2 && Items().Select(x=>Field<Image>(x,"mIcon").sprite).SequenceEqual(new[]{assets.Icon(57),assets.Icon(1)}),"dynamic currency order and reused icon identity");
        view.Dispose();view=null;yield return null;
        Require(activeProviders==0 && errors==0,"disposal releases services with no Unity runtime errors");
    }
    private MePlayerItemUI[] Items()=>view.Root.GetComponentsInChildren<MePlayerItemUI>();
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private static string Text(MePlayerItemUI item)=>System.Text.RegularExpressions.Regex.Replace(
        Field<Component>(item,"mCountText").GetComponent<TMP_Text>().text,"<[^>]*>",string.Empty);
    private void Notify(int id) {foreach(var value in providers.ToArray())if(value.active&&value.id==id)value.Notify(counts[id]);}
    private void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();inputPosition=RectTransformUtility.WorldToScreenPoint(cameraUi,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var data=new PointerEventData(events){position=inputPosition,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native button must be the unobstructed first pointer target");
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var old=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());
        RenderTexture.active=old;Object.Destroy(texture);
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public IPlayerItemUIHandler CreateHandler(PlayerItemType type,PlayerItemUIItemIdProvider provider)=>new Handler(this,provider.ItemId,type==PlayerItemType.TiliId);
    public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type,PlayerItemUIItemIdProvider provider)
    {var value=new CountProvider(this,provider.ItemId);providers.Add(value);return value;}
    public IDisposable LoadIcon(int id,Action<Sprite> callback){callback(assets.Icon(id));return new Handle();}
    public Vector2 InputPosition=>inputPosition;
    public object Compact(long value)=>value.ToString(CultureInfo.InvariantCulture);
    public string ColorFormat(string value,Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+value+"</color>";
    private sealed class Handle:IDisposable {public void Dispose(){}}
    private sealed class Handler:IPlayerItemUIHandler,IPlayerItemUIWithLimit
    {
        private readonly HomeTopBarNativeRunner owner;private readonly bool limit;
        public int ItemId{get;set;}public bool ShowAddBtn=>ItemId==1;
        public Handler(HomeTopBarNativeRunner owner,int id,bool limit){this.owner=owner;ItemId=id;this.limit=limit;}
        public bool TryGetMaxCountLimit(out long value){value=300;return limit;}
        public void HandleClick(Vector2 position){owner.descriptionItem=ItemId;}
        public void HandleAddBtn(){owner.addItem=ItemId;}
    }
    private sealed class CountProvider:IPlayerItemUICountProvider
    {
        private readonly HomeTopBarNativeRunner owner;public readonly int id;public bool active;
        public CountProvider(HomeTopBarNativeRunner owner,int id){this.owner=owner;this.id=id;}
        public long Count=>active?owner.counts[id]:0;
        public event Action<long,bool> OnItemCountUpdate;
        public void Init(){Require(!active,"provider double bind");active=true;owner.activeProviders++;}
        public void Release(){Require(active,"provider double release");active=false;owner.activeProviders--;}
        public void Notify(long count)=>OnItemCountUpdate?.Invoke(count,false);
    }
}
