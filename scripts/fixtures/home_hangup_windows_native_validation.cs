using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

[InitializeOnLoad]
public static class HomeHangUpWindowsNativeValidation
{
    const string Key = "Zjcs.HomeHangUpWindowsNative";
    static HomeHangUpWindowsNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("OriginalIncomeWindowFixture").AddComponent<HomeHangUpWindowsNativeRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original income window validation timeout."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key+".Deadline", (float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("HOME_HANGUP_WINDOWS_NATIVE_PASSED: complete native bed, pointer collect/foldouts/quick-receive, reward consumption order, error rollback, confirmation priority, timer, popup clips and resource disposal. Player, transaction, property chunks, secondary windows and tip owners are controlled fixture ports; production binding and Windows Player remain pending.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeHangUpWindowsNativeRunner : MonoBehaviour, IHomeHangUpWindowServices,
    IHomeHangUpWindowPlayer, IHomeWindowPropertyCenter, IWorldWindowBlurManager, IProfessionWindowService
{
    Camera cameraUi; RenderTexture target; Material blur;
    Canvas canvas; EventSystem events; GameObject root, maskRoot;
    WorldPopupWindow popup; FurnitureBedDialog bed;
    OfficialHomeHangUpWindowAssetCatalog assets;
    OfficialHomeRepairAssetCatalog repair;
    IDisposable binding, metrics; FontAssetFontService fonts;
    OfflinePartnerUiSoundScope soundScope;
    IProfessionImageResourceLoader images;
    int errors, subscriptions, operations, loads, messages, fastOpens, awards;
    bool quick = true, rejectQuick, blockedSwitch, free, speedGate, rankLimited = true, mapPublic = true, exceed;
    long pool = 600; double seconds = 1800;
    string confirmation; Action confirmed;
    readonly List<string> calls = new List<string>();
    readonly Dictionary<PropType,long> props = new Dictionary<PropType,long>();
    readonly HomeWindowHangUpInfo info = new HomeWindowHangUpInfo { Id=1, FastHangUpId=1, BindFurniture=EFurniture.Bed,
        EffectFixedPropType=PropType.HangUpExpAdd, EffectPercentPropType=PropType.HangUpExpAddPercent, MaxStorePropType=PropType.HangUpExpMaxStore };
    readonly HomeWindowFastHangUpInfo fast = new HomeWindowFastHangUpInfo { FastHangUpId=1,
        FastHangUpItemDic=new Dictionary<int,int>{{3000,2}}, HangUpIdList=new List<int>{1} };

    IEnumerator Start()
    {
        Application.runInBackground=true; AudioListener.volume=0;
        Application.logMessageReceived += OnLog;
        var steps = new Stack<IEnumerator>(); steps.Push(Exercise());
        while (steps.Count > 0)
        {
            bool next; object current=null;
            try { next=steps.Peek().MoveNext(); if(next)current=steps.Peek().Current; }
            catch(Exception error) { HomeHangUpWindowsNativeValidation.Finish(error); yield break; }
            if(!next){steps.Pop();continue;}
            if(current is IEnumerator nested){steps.Push(nested);continue;}
            yield return current;
        }
        HomeHangUpWindowsNativeValidation.Finish(errors==0 ? null : new Exception("Unexpected runtime errors: "+errors));
    }
    void OnLog(string text,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++; }

    IEnumerator Exercise()
    {
        assets=OfficialHomeHangUpWindowAssetCatalog.LoadDefault(); repair=OfficialHomeRepairAssetCatalog.LoadDefault();
        Require(assets.bedPrefab,"explicit original bed prefab binding");
        Require(assets.bedRewardImage,"explicit original experience sprite binding");
        var receipt=JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/HomeHangUpWindowsNativeValidation/fixture.json").text);
        soundScope=new OfflinePartnerUiSoundScope(id=>((JObject)receipt["audio"]).Properties().Select(p=>p.Value)
            .Where(row=>(uint)row["event"]["id"]==id).Select(row=>Load<AudioClip>(row["pointer"])).SingleOrDefault());
        var catalog=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        catalog.Entries=new[]{new OfflineProfessionImageCatalog.Entry{Path=assets.bedRewardImagePath,Asset=assets.bedRewardImage,
                AssetGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(assets.bedRewardImage))},
            new OfflineProfessionImageCatalog.Entry{Path="Bed",Asset=repair.IconPrefab(EFurniture.Bed).gameObject,
                AssetGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(repair.IconPrefab(EFurniture.Bed)))}};
        images=new CountingImages(new OfflineProfessionImageResourceLoader(catalog),this);
        var language=new OfflinePartnerLocalizationService(assets.texts);
        LanguageManager.Initialize(language);
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService(); fonts.Initialize();
        metrics=ScreenMetrics.Bind(new Metrics());
        ProfessionPropertyBindings.Current=new OfflineProfessionPropertyBindings(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Partners/partner-property-ui.json"), images, language,
            (owner,path,name,desc,control)=>{throw new InvalidOperationException("Unexpected entry popup");},
            (owner,rank,desc,effect,noEffect,noSeason)=>{throw new InvalidOperationException("Unexpected blessing popup");});
        cameraUi=new GameObject("MutedOriginalIncomeCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.orthographic=true; cameraUi.clearFlags=CameraClearFlags.SolidColor; cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24); target.Create(); cameraUi.targetTexture=target;
        canvas=new GameObject("IncomeWindowTestCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var masks=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();blur=new Material(masks.BlurMaterial);
        var staging=new GameObject("IncomeWindowStaging",typeof(RectTransform));staging.transform.SetParent(canvas.transform,false);staging.SetActive(false);
        root=Object.Instantiate(assets.bedPrefab,staging.transform,false);
        var originalFont=root.GetComponentsInChildren<TMP_Text>(true).First(text=>text.font!=null).font;
        foreach(var font in root.GetComponentsInChildren<TMP_Text>(true).Select(text=>text.font).Where(font=>font!=null).Distinct())
            fonts.SetupFont(font);
        binding=OfflineUiBindingModule.Register(root.transform,new OfflineUiBindings{Localize=assets.Text,
            Font=()=>originalFont,Pool=(scope,template,parent,keepWorld)=>Object.Instantiate(template,parent,keepWorld)});
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=assets.Text;
        foreach(var sound in root.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=PostSound;
        foreach(var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))sound.Post=PostSound;
        foreach(var control in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var port=control.GetType().GetProperty("Services");
            if(port!=null && port.PropertyType==typeof(IHomeHangUpWindowServices))port.SetValue(control,this);
        }
        bed=root.GetComponent<FurnitureBedDialog>();bed.Init(EFurniture.Bed);
        root.SetActive(false);root.transform.SetParent(canvas.transform,false);
        popup=root.GetComponent<WorldPopupWindow>();popup.SetCanvasSortingOrder(10);popup.SetMaskClickAction(()=>Close(root));
        maskRoot=Object.Instantiate(masks.BlurMaskPrefab,staging.transform,false);maskRoot.SetActive(false);maskRoot.transform.SetParent(canvas.transform,false);
        var mask=maskRoot.GetComponent<WorldWindowBlurMask>();mask.BlurManager=this;
        yield return mask.Prepare(popup,false);
        root.SetActive(true);
        foreach(var close in root.GetComponentsInChildren<CloseWindowBtn>(true))close.Init(this);
        popup.Show();mask.AttachToWindow(popup);Object.Destroy(staging);
        yield return new WaitForSeconds(.8f);
        Require(popup.State==WorldPopupWindow.EState.Show && popup.mShowClip && popup.mHideClip,"original popup animation");
        Require(bed.Name.Text==assets.Text("item_4000_name"),"original bed localization");
        Require(bed.HangUpRewardBtnImg.sprite==assets.bedRewardImage,"actual dynamic experience sprite");
        Require(bed.HangUpProgress.fillAmount>.149f && bed.HangUpProgress.fillAmount<.152f,"settled plus elapsed timer snapshot");
        Capture("hangup-windows-bed.png");
        bool fold=bed.LevelFoldout;Click(bed.LevelFoldoutBtn);Require(bed.LevelFoldout!=fold,"native level foldout pointer");
        yield return null;Capture("hangup-windows-bed-level.png");
        Click(bed.LevelFoldoutBtn);yield return null;
        fold=bed.OutputFoldout;Click(bed.OutputFoldoutBtn);Require(bed.OutputFoldout!=fold,"native output foldout pointer");
        yield return null;Capture("hangup-windows-bed-output.png");Click(bed.OutputFoldoutBtn);yield return null;
        calls.Clear();Click(bed.GetHangUpRewardBtn);
        Require(string.Join(",",calls)=="collect,award,merge,popup,publish", "original award sequence: "+string.Join(",",calls));
        Require(awards==1 && pool==0,"reward consumed once");
        Click(bed.GetHangUpRewardBtn);Require(awards==1 && messages==1,"empty award rejected before request");
        var toggle=root.GetComponentsInChildren<Toggle>(true).Single();
        Click(toggle);Require(!quick && !toggle.isOn,"native quick-receive pointer persists controlled state");
        int previousOperations=operations;rejectQuick=true;Click(toggle);
        Require(!quick && !toggle.isOn && operations==previousOperations+1,"failed switch reverts without another request");rejectQuick=false;
        blockedSwitch=true;root.SetActive(false);root.SetActive(true);yield return null;
        Require(quick==false,"function gate refresh preserves preference");blockedSwitch=false;
        speedGate=true;Click(bed.FastHangUpBtn);Require(fastOpens==1 && confirmation==null,"gated confirmation bypass");
        speedGate=false;rankLimited=false;Click(bed.FastHangUpBtn);Require(confirmation=="home_bed_confirm1","rank confirmation priority");confirmed();
        Require(fastOpens==2,"confirmed speed entry");confirmation=null;
        rankLimited=true;mapPublic=false;Click(bed.FastHangUpBtn);Require(confirmation=="home_bed_confirm2","map confirmation priority");confirmed();confirmation=null;
        mapPublic=true;free=true;exceed=true;
        // Original free-state text/branch refreshes on enabling the window;
        // OnFastHangUp alone refreshes only its reward timer.
        root.SetActive(false);root.SetActive(true);yield return null;
        OnFastHangUp?.Invoke(1);yield return null;
        Require(bed.FastHangUpSoundEvent.PlayingId>0,"complete original layered speed sound posted while muted");
        Click(bed.FastHangUpBtn);Require(confirmation=="exceed","free speed capacity confirmation");
        // Confirming the free branch closes the original window before request.
        calls.Clear();confirmed();Require(calls.SequenceEqual(new[]{"close","fast"}),"free speed close/request order");
        yield return new WaitForSeconds(popup.mHideClip.length+.2f);
        Require(!root.activeSelf,"original hide completion");Capture("hangup-windows-bed-closed.png");
        Object.Destroy(root);Object.Destroy(maskRoot);yield return null;
        Require(subscriptions==0 && loads==0,"all window subscriptions and loaded handles released");
        Require(OnFastHangUp==null && OnAddOutputAdditionData==null,"original player event handlers released");
        binding.Dispose();soundScope.Dispose();fonts.Dispose();metrics.Dispose();
    }

    sealed class Metrics:IScreenMetrics
    {
        public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;
        public Rect SafeArea=>new Rect(0,0,608,1080);
    }
    T Load<T>(JToken pointer) where T:Object
    {
        var path=AssetDatabase.GUIDToAssetPath((string)pointer["guid"]);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().Single(value=>{
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string _,out long id);return id==(long)pointer["fileID"];});
    }
    void Click(Selectable selectable)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)selectable.transform;
        var point=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
        if(hits.Count==0 || !(hits[0].gameObject==selectable.gameObject || hits[0].gameObject.transform.IsChildOf(selectable.transform)))
        {
            Debug.Log("INCOME_POINTER_DIAGNOSTIC target="+selectable.name+" point="+point+" rect="+rect.rect+
                " graphic="+selectable.targetGraphic+" depth="+selectable.targetGraphic?.depth+
                " cull="+selectable.targetGraphic?.canvasRenderer.cull+" raycast="+selectable.targetGraphic?.raycastTarget+
                " hitPaths="+string.Join(";",hits.Select(hit=>PathOf(hit.gameObject.transform))));
        }
        Require(hits.Count>0 && (hits[0].gameObject==selectable.gameObject || hits[0].gameObject.transform.IsChildOf(selectable.transform)),
            "native pointer reaches "+selectable.name+" actual="+(hits.Count==0?"none":hits[0].gameObject.name));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static string PathOf(Transform value)=>value.parent==null?value.name:PathOf(value.parent)+"/"+value.name;
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());Object.Destroy(texture);
    }
    void PostSound(AudioClip clip,GameObject owner){Require(clip && owner,"original popup/button PCM");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    sealed class Release:IDisposable{Action action;public Release(Action action){this.action=action;}public void Dispose(){var a=action;action=null;a?.Invoke();}}
    sealed class CountingImages:IProfessionImageResourceLoader
    {
        readonly IProfessionImageResourceLoader inner;readonly HomeHangUpWindowsNativeRunner owner;
        public CountingImages(IProfessionImageResourceLoader inner,HomeHangUpWindowsNativeRunner owner){this.inner=inner;this.owner=owner;}
        IProfessionAssetOperation<T> Track<T>(IProfessionAssetOperation<T> value) where T:Object
        {owner.loads++;return new Operation<T>(value,()=>owner.loads--);}
        public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T:Object=>Track(inner.LoadAsset<T>(path));
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T:Object=>Track(inner.LoadAssetAsync<T>(path));
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T:Object=>Track(inner.LoadAssetByGuid<T>(guid));
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T:Object=>Track(inner.LoadAssetAsyncByGuid<T>(guid));
        sealed class Operation<T>:IProfessionAssetOperation<T> where T:Object
        {
            readonly IProfessionAssetOperation<T> value;Action dispose;
            public Operation(IProfessionAssetOperation<T> value,Action dispose){this.value=value;this.dispose=dispose;}
            public T Result=>value.Result;public bool IsValid=>value.IsValid;public int Status=>value.Status;
            public event Action<IProfessionAssetOperation<T>> Completed{add{value(this);}remove{}}
            public void Dispose(){var a=dispose;dispose=null;if(a!=null){value.Dispose();a();}}
        }
    }
    public RenderTexture GetRenderTexture()=>RenderTexture.GetTemporary(target.width/13,target.height/13);
    public void ReleaseRenderTexture(RenderTexture value)=>RenderTexture.ReleaseTemporary(value);
    public void TakeAShot(RenderTexture value){Canvas.ForceUpdateCanvases();cameraUi.Render();Zjcs.Presentation.World.OfficialDungeonPopupMask.Blur(target,value,blur);}
    public void ObserveDestroyed(WorldPopupWindow window,Action action)=>window.OnWindowDestory+=action;
    public void StopObservingDestroyed(WorldPopupWindow window,Action action)=>window.OnWindowDestory-=action;
    public void Close(GameObject window){calls.Add("close");popup.Hide(()=>{maskRoot.GetComponent<WorldWindowMask>().UnattachToWindow();window.SetActive(false);});}
    public void CloseLast()=>Close(root);
    public IHomeHangUpWindowPlayer Player=>this;public IProfessionImageResourceLoader Resources=>images;
    public int HomeBedSpeedConfirmFunctionId=>163;public int PlayerExpItemId=>50;
    public HomeWindowHangUpInfo GetHangUpInfo(int id)=>info;
    public bool TryGetMainHangUpInfo(EFurniture type,out HomeWindowHangUpInfo value){value=info;return true;}
    public HomeWindowFastHangUpInfo GetFastHangUpInfo(int id)=>fast;
    public HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture type)=>new HomeWindowFurnitureInfo{ClassId=4000};
    public HomeWindowFunctionLimitInfo GetFunctionLimitInfo(int id)=>new HomeWindowFunctionLimitInfo{Condition=this};
    public string GetFurnitureTypeNameKey(EFurniture type)=>repair.FurnitureTypeNameKey(type);
    public string GetFurnitureIconPrefabPath(EFurniture type)=>"Bed";
    public string GetItemNameKey(int id)=>"item_"+id+"_name";public string GetItemDescKey(int id)=>"item_"+id+"_desc";
    public string GetHangUpBaseOutput(int id)=>"ui_common_world_level";
    public string Text(string key)=>assets.Text(key);public string TextFormatted(string key,params object[] args)=>string.Format(Text(key),args);
    public string FormatCount(long count)=>count.ToString(CultureInfo.InvariantCulture);
    public double GetHangUpReward(int id,double hours,int level=-1,bool isBase=false)=>1200*hours;
    public long GetHangUpMaxRewardStore(int id,int level=-1)=>8000;
    public long GetHangUpAwardCount(int id,out double value){value=seconds;return pool;}
    public long CalcOutputAdditionExtStore(int id,long capacity)=>0;
    public long CalcOutputAdditionUnsettleExtRewardCount(int id)=>0;
    public double CalcOutputAdditionExtCount(int id,double count)=>0;
    public bool CanGetHangUpReward(int id)=>pool>0;
    public HomeWindowItemInfo GetHangUpRewardItem(int id)=>new HomeWindowItemInfo{Icon=assets.bedRewardImagePath};
    public double GetProp(IReadOnlyDictionary<PropType,long> values,PropType type)=>values.TryGetValue(type,out long value)?value/10000.0:0;
    public void GetHangUpProp(HomeWindowHangUpInfo i,IReadOnlyDictionary<PropType,long> values,out double percent,out double fixedValue){percent=1;fixedValue=0;}
    public int GetRankHighSubRank(int rank)=>rank;public bool IsRankLimited(int rank)=>rankLimited;
    public void GetMaxPlayerLevel(int rank,out int baseLevel,out int blessLevel){baseLevel=1;blessLevel=0;}
    public bool TryGetFirstMainMapRegion(int rank,out HomeWindowMapRegionInfo region){region=new HomeWindowMapRegionInfo{ClassId=10,Region=1};return true;}
    public int GetTipLevel(EFurniture type)=>1;
    public IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> callback){subscriptions++;return new Release(()=>subscriptions--);}
    public void RegisterCondition(object condition,Action callback){subscriptions++;}
    public void DeregisterCondition(object condition,Action callback){subscriptions--;}
    public void SwitchQuickReceive(EFurniture type,bool value,Action<object,int> onError){operations++;if(rejectQuick)onError(null,1);else quick=value;}
    sealed class AwardMessage:IHomeWindowAwardMessage{public IList DataList{get;}=new ArrayList{new object()};public object SourceData{get;}=new object();}
    public void Collect(IReadOnlyList<int> ids,Action<IHomeWindowAwardMessage> onSuccess){calls.Add("collect");pool=0;seconds=0;onSuccess(new AwardMessage());}
    public void MergeAward(IList data){calls.Add("merge");}public void PopupAwardList(IList data){calls.Add("popup");}
    public void PublishCollected(){calls.Add("publish");}public void SystemMessage(string text){messages++;}
    public void Confirm(string key,Action action){confirmation=key;confirmed=action;}
    public void Confirm(HomeWindowText key,HomeWindowText title,Action action){confirmation=key.Key;confirmed=action;}
    public void OpenFastHangUp(int id,GameObject parent){fastOpens++;}
    public void OperFastHangUp(int id,bool autoReceive,Dictionary<int,long> cost,Action complete){calls.Add("fast");complete?.Invoke();}
    public IDisposable SubscribeOutputAdditionFunctionState(Action callback){subscriptions++;return new Release(()=>subscriptions--);}
    public IDisposable SubscribeNewDay(Action<object> callback){subscriptions++;return new Release(()=>subscriptions--);}
    public bool IsOutputAdditionDisabled=>false;
    public List<(List<HomeWindowOutputAdditionInfo>,DateTime)> GetCurrOutputGroupPropList(OutputGroupType type)=>new List<(List<HomeWindowOutputAdditionInfo>,DateTime)>();
    public void OpenOutputAddition(OutputGroupType type){throw new InvalidOperationException("Secondary window outside fixture");}
    public void OpenBlessingRule(){throw new InvalidOperationException("Secondary window outside fixture");}
    public int Level=>1;public int SubRank=>1;public IReadOnlyDictionary<PropType,long> NonBattleProps=>props;
    public IHomeWindowPropertyCenter PlayerPropCenter=>this;
    public int GetHangUpLevel(int id)=>1;public int GetBlessingLevel()=>1;public int GetLostGoddessActiveCount()=>0;
    public bool CanFreeFastHangUp(int id)=>free;public bool CanGetExp()=>true;
    public bool IsFunctionLimit(int id)=>id==164?blockedSwitch:speedGate;
    public bool GetHangUpQuickReceiveState(EFurniture type)=>quick;
    public int GetOutputAdditionPropValue(int type)=>0;public bool IsMapRegionPublic(int id,int region)=>mapPublic;
    public bool TryGetFastHangUpTip(int id,long hours,out int item,out HomeWindowText title,out HomeWindowText content)
    {item=50;title=new HomeWindowText("capacity");content=new HomeWindowText("exceed");return exceed;}
    public void Award(IList data,object source){calls.Add("award");awards++;}
    public event Action<int> OnFastHangUp;public event Action<object> OnAddOutputAdditionData;
    public void GetProps(List<EPropHandleType> types,Dictionary<PropType,long> result){result.Clear();}
    public Dictionary<PropType,long> GetChunckProps(EPropHandleType type)=>new Dictionary<PropType,long>();
}
