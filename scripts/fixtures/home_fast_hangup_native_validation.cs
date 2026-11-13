using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

[InitializeOnLoad]
public static class HomeFastHangUpNativeValidation
{
    const string Key="Zjcs.HomeFastHangUpNative";
    static HomeFastHangUpNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("NativeFastHangUpFixture").AddComponent<HomeFastHangUpNativeRunner>();};
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Original acceleration fixture timed out"));};
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_FAST_HANGUP_NATIVE_PASSED: complete original selection, pointer quantity/buttons, native item icons, preview, clamp/cooldown, confirmations, no-item/error/success branches, response presentation order and lifecycle. Counts/rates/requests, confirmation, progress and award windows are controlled ports; production profile and Player remain pending.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeFastHangUpNativeRunner:MonoBehaviour,IHomeFastHangUpServices,IHomeFastHangUpPlayer,
    IHomeHangUpWindowServices,IProfessionItemIconContext,IProfessionItemColors,IProfessionItemPlayer,
    IHomeCountColors,IWorldWindowBlurManager,IProfessionWindowService
{
    OfficialHomeFastHangUpAssetCatalog assets;OfflineHomeCurrencyAssets colorAssets;JObject proof,rules;Dictionary<string,string> texts;
    Camera cameraUi;RenderTexture target;Canvas canvas;EventSystem events;GameObject root,maskRoot;Material blur;
    FurnitureFastHangUpDialog dialog;WorldPopupWindow popup;
    FontAssetFontService fonts;IProfessionImageResourceLoader images;ItemIconAttachmentManager attachments;
    OfflinePartnerUiSoundScope sound;IDisposable metrics;readonly List<IDisposable> bindings=new List<IDisposable>();
    readonly List<string> calls=new List<string>();readonly HashSet<ItemIcon> icons=new HashSet<ItemIcon>();
    readonly List<AudioClip> postedSounds=new List<AudioClip>();
    HomeFastHangUpInfo fast;int material,errors,messages,subscribers;long held=150;bool exceed,reject,withoutAward;
    Action confirmed,progressFinished;HomeFastHangUpRequest request;
    IEnumerator Start()
    {
        Application.runInBackground=true;AudioListener.volume=0;
        Application.logMessageReceived+=(t,s,k)=>{if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors++;};
        var steps=new Stack<IEnumerator>();steps.Push(Exercise());
        while(steps.Count>0){bool next;object current=null;try{next=steps.Peek().MoveNext();if(next)current=steps.Peek().Current;}catch(Exception e){HomeFastHangUpNativeValidation.Finish(e);yield break;}if(!next){steps.Pop();continue;}if(current is IEnumerator nested){steps.Push(nested);continue;}yield return current;}
        HomeFastHangUpNativeValidation.Finish(errors==0?null:new Exception("Runtime errors: "+errors));
    }
    IEnumerator Exercise()
    {
        assets=OfficialHomeFastHangUpAssetCatalog.LoadDefault();rules=JObject.Parse(assets.rules.text);
        colorAssets=ScriptableObject.CreateInstance<OfflineHomeCurrencyAssets>();colorAssets.configuration=assets.rules;colorAssets.texts=assets.texts;
        proof=JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/HomeFastHangUpNativeValidation/fixture.json").text);
        texts=((JArray)JObject.Parse(assets.texts.text)["texts"]).ToDictionary(x=>(string)x["key"],x=>(string)x["text"]);
        fast=rules["tables"]["fast_hang_up"].First().ToObject<HomeFastHangUpInfo>();material=fast.FastHangUpItemDic.Keys.Single();
        Require(fast.BindFurniture==EFurniture.Bed&&fast.MaxUseItemCount==100&&material==120,"selected original configuration");
        LanguageManager.Initialize(new OfflinePartnerLocalizationService(assets.texts));
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();metrics=ScreenMetrics.Bind(new Metrics());
        var catalog=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        catalog.Entries=assets.images.Select(x=>new OfflineProfessionImageCatalog.Entry{Path="Assets/Image/"+x.path,AssetGuid=x.assetGuid,Asset=x.sprite})
            .Concat(new[]{new OfflineProfessionImageCatalog.Entry{Path=(string)proof["hint_attachment"]["asset"]["path"],AssetGuid=(string)proof["hint_attachment"]["asset"]["guid"],Asset=assets.hintPrefab}}).ToArray();
        images=new OfflineProfessionImageResourceLoader(catalog);attachments=new ItemIconAttachmentManager(images,()=>assets.attachmentConfig,new GAction());
        sound=new OfflinePartnerUiSoundScope(id=>((JObject)proof["audio"]).Properties().Select(x=>x.Value).Where(x=>(uint)x["event"]["id"]==id).Select(x=>Load<AudioClip>(x["pointer"])).SingleOrDefault());
        cameraUi=new GameObject("MutedNativeFastCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cameraUi.orthographic=true;cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();cameraUi.targetTexture=target;
        canvas=new GameObject("NativeAccelerationCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalNativeAccelerationPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var masks=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();blur=new Material(masks.BlurMaterial);
        root=Create(assets.selectionPrefab,canvas.transform,false);dialog=root.GetComponent<FurnitureFastHangUpDialog>();dialog.Init(fast.FastHangUpId,null);
        popup=root.GetComponent<WorldPopupWindow>();popup.SetMaskClickAction(()=>Close(root));
        maskRoot=Create(masks.BlurMaskPrefab,canvas.transform,false);var mask=maskRoot.GetComponent<WorldWindowBlurMask>();mask.BlurManager=this;
        yield return mask.Prepare(popup,false);root.SetActive(true);foreach(var close in root.GetComponentsInChildren<CloseWindowBtn>(true))close.Init(this);
        popup.OnShowing+=shown=>{shown.SetCanvasSortingOrder(10);mask.AttachToWindow(shown);};popup.Show();yield return new WaitForSeconds(.8f);
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original script bindings");
        var slider=ActiveSlider();Require(slider.CurrentCount==DefaultFastItemUseCount&&PreviewCount()==3000,"selected initial count and original hours plus unsettled preview");
        Capture("fast-hangup-selection.png");Click(slider.AddBtn);Require(slider.CurrentCount==2&&PreviewCount()==5400,"native add and preview");Click(slider.MinusBtn);Require(slider.CurrentCount==1,"native minus");
        Require(postedSounds.Contains(slider.AddBtn.GetComponent<WorldButtonSound>().ClickEvent),"original quantity button PCM posted while muted");
        Require(!slider.MaxBtn,"original acceleration template has no max button");
        Drag(slider.Slider,1);Require(slider.CurrentCount==100&&slider.Slider.value==100&&messages==1,"original single-use clamp and tip");
        Drag(slider.Slider,.9f);Require(messages==1,"original message cooldown");
        Drag(slider.Slider,.25f);Require(slider.CurrentCount>0&&slider.CurrentCount<100,"native internal slider drag updates quantity");
        Capture("fast-hangup-quantity.png");
        slider.Slider.value=0;Click(dialog.FastBtn);Require(request==null&&messages==2,"zero selection message before request");
        slider.Slider.value=2;exceed=true;calls.Clear();Click(dialog.FastBtn);Require(confirmed!=null&&request==null,"capacity confirmation precedes request");
        reject=true;confirmed();Require(request.ItemCost[material]==2&&!request.IsFree&&!request.IsReceive,"original paid request fields");
        Require(root.activeSelf&&calls.SequenceEqual(new[]{"request"}),"request failure preserves selection and skips presentation");
        reject=false;exceed=false;calls.Clear();Click(dialog.FastBtn);Require(calls.SequenceEqual(new[]{"request","close","progress"}),"success closes bed before progress");
        Require(progressFinished!=null&&!calls.Contains("award"),"award waits for progress disable callback");
        progressFinished();Require(calls.SequenceEqual(new[]{"request","close","progress","merge","award","awardWindow"}),"original merge then consume then award window");
        yield return new WaitForSeconds(popup.mHideClip.length+.3f);Require(!root.activeSelf&&icons.Count==0&&subscribers==0,"selection hides and releases all icons/subscriptions");
        Capture("fast-hangup-closed.png");
        Object.Destroy(root);yield return null;held=0;
        root=Create(assets.selectionPrefab,canvas.transform,false);dialog=root.GetComponent<FurnitureFastHangUpDialog>();dialog.Init(fast.FastHangUpId,null);
        popup=root.GetComponent<WorldPopupWindow>();root.SetActive(true);popup.Show();yield return new WaitForSeconds(.8f);
        Require(!root.GetComponentsInChildren<SetUseCountSlider>().Any(),"empty inventory returns original slider to pool");
        request=null;Require(!dialog.FastBtn.isActiveAndEnabled||!dialog.FastBtn.interactable,"empty inventory disables acceleration");
        calls.Clear();withoutAward=true;HangUpUIUtils.OperFastHangUp(this,fast.FastHangUpId,true,null,()=>calls.Add("complete"),()=>calls.Add("success"));
        Require(request.IsFree&&request.IsReceive&&request.ItemCost==null&&calls.SequenceEqual(new[]{"request","success","progress"}),"original free request and callbacks");progressFinished();Require(calls.Last()=="complete"&&!calls.Contains("award"),"null award still completes after progress");
        root.SetActive(false);Object.Destroy(root);Object.Destroy(maskRoot);yield return null;Require(icons.Count==0&&subscribers==0,"destroy releases native ownership");
        attachments.Dispose();foreach(var binding in bindings)binding.Dispose();sound.Dispose();fonts.Dispose();metrics.Dispose();
    }
    GameObject Create(GameObject prefab,Transform parent,bool keepWorld)
    {
        var stage=new GameObject("InactiveAccelerationBinding",typeof(RectTransform));stage.transform.SetParent(canvas.transform,false);stage.SetActive(false);
        try{
            var value=Object.Instantiate(prefab,stage.transform,false);value.SetActive(false);
            bindings.Add(OfflineUiBindingModule.Register(value.transform,new OfflineUiBindings{Localize=Text,Font=()=>OfflineHomeHudAssets.LoadDefault().defaultFont,Images=_=>images,Pool=(scope,p,slot,keep)=>Create(p,slot,keep)}));
            foreach(var text in value.GetComponentsInChildren<TMP_Text>(true))if(text.font)fonts.SetupFont(text.font);
            foreach(var text in value.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=Text;
            foreach(var component in value.GetComponentsInChildren<MonoBehaviour>(true)){var port=component.GetType().GetProperty("Services");if(port?.PropertyType==typeof(IHomeFastHangUpServices))port.SetValue(component,this);}
            foreach(var control in value.GetComponentsInChildren<SetCountSlider>(true))control.GlobalColors=this;
            foreach(var icon in value.GetComponentsInChildren<ItemIcon>(true))icon.Bind(this);
            foreach(var cell in value.GetComponentsInChildren<ItemIconAttachment>(true))cell.Bind(attachments);
            foreach(var audio in value.GetComponentsInChildren<WorldButtonSound>(true))audio.Post=PostAudio;
            foreach(var audio in value.GetComponentsInChildren<WorldPopupWindowSound>(true))audio.Post=PostAudio;
            value.transform.SetParent(parent,keepWorld);return value;
        }finally{Object.Destroy(stage);}
    }
    SetUseCountSlider ActiveSlider()=>root.GetComponentsInChildren<SetUseCountSlider>().Single();
    void PostAudio(AudioClip clip,GameObject owner)
    {
        Require(clip&&AudioListener.volume==0,"verified native PCM and muted listener");
        var voice=new GameObject("MutedAccelerationPCM").AddComponent<AudioSource>();
        voice.playOnAwake=false;voice.spatialBlend=0;voice.clip=clip;voice.Play();
        Require(voice.isPlaying,"original PCM playback state");postedSounds.Add(clip);
        Object.Destroy(voice.gameObject,clip.length);
    }
    long PreviewCount()=>Field<Dictionary<int,ItemIcon>>(dialog,"mPreviewItemDic")[1].myText.Text==null?-1:long.Parse(Field<Dictionary<int,ItemIcon>>(dialog,"mPreviewItemDic")[1].myText.Text,CultureInfo.InvariantCulture);
    static T Field<T>(object owner,string key)=>(T)owner.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    T Load<T>(JToken pointer)where T:Object{var path=AssetDatabase.GUIDToAssetPath((string)pointer["guid"]);return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().Single(x=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x,out string _,out long id);return id==(long)pointer["fileID"];});}
    void Click(Selectable button)
    {
        Canvas.ForceUpdateCanvases();Require(button&&button.isActiveAndEnabled&&button.interactable,"enabled native control "+(button?button.name:"<missing>"));var rect=(RectTransform)button.transform;var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"first native raycast "+button.name+": "+string.Join(",",hits.Select(x=>x.gameObject.name)));ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Drag(Slider slider,float normalized)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)slider.fillRect.parent;var data=new PointerEventData(events){button=PointerEventData.InputButton.Left};
        data.position=RectTransformUtility.WorldToScreenPoint(cameraUi,slider.handleRect.TransformPoint(slider.handleRect.rect.center));
        data.pointerPressRaycast=new RaycastResult{module=canvas.GetComponent<GraphicRaycaster>()};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject)==slider.gameObject,"first native slider handle raycast");ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerDownHandler);
        data.position=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(new Vector2(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,normalized),rect.rect.center.y)));
        ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.dragHandler);ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerUpHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(608,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,608,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Exception Outside()=>new InvalidOperationException("Outside controlled acceleration fixture scope");
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    public IHomeHangUpWindowServices Window=>this;IHomeFastHangUpPlayer IHomeFastHangUpServices.Player=>this;IHomeHangUpWindowPlayer IHomeHangUpWindowServices.Player=>this;
    public int DefaultFastItemUseCount=>(int)rules["homeSettings"]["mDefaultFastItemUseCount"];public double FastItemMaxCountMessageColdDown=>(double)rules["homeSettings"]["mFastItemMaxCountMessageColdDown"];
    public HomeFastHangUpInfo GetFastHangUpInfo(int id)=>fast;HomeWindowFastHangUpInfo IHomeHangUpWindowServices.GetFastHangUpInfo(int id)=>throw Outside();
    public bool TryGetFastHangUpItemHours(int id,out int hours)=>fast.FastHangUpItemDic.TryGetValue(id,out hours);
    public IProfessionItemInfo GetHangUpRewardItem(int id)=>GetItemInfo(PlayerExpItemId);HomeWindowItemInfo IHomeHangUpWindowServices.GetHangUpRewardItem(int id)=>throw Outside();
    public ItemIcon GetIcon(int id,Transform slot)=>GetIcon(GetItemInfo(id),slot);
    public ItemIcon GetIcon(IProfessionItemInfo item,Transform slot){var value=Create(assets.itemIconPrefab,slot,false);var icon=value.GetComponent<ItemIcon>();value.SetActive(true);icon.Clear();icon.Init(item.ClassId);Require(icons.Add(icon),"one icon lease");return icon;}
    public void RecycleIcon(ItemIcon icon){Require(icons.Remove(icon),"return known icon");icon.OnGameObjectPoolClear();icon.gameObject.SetActive(false);Object.Destroy(icon.gameObject);}
    public double GetHangUpUnsettledRewardCount(int id,out double seconds){seconds=1800;return 600;}
    public void RequestFastHangUp(HomeFastHangUpRequest value,Action<HomeFastHangUpResponse,int> complete){calls.Add("request");request=value;complete(new HomeFastHangUpResponse{AwardMsg=withoutAward?null:new AwardMessage()},reject?1:0);}
    public void OpenProgress(int id,Action finished){calls.Add("progress");progressFinished=finished;}public void OpenAward(IHomeWindowAwardMessage message)=>calls.Add("awardWindow");
    sealed class AwardMessage:IHomeWindowAwardMessage{public IList DataList{get;}=new ArrayList{new object()};public object SourceData{get;}=new object();}
    public long GetItemAmount(int id)=>id==material?held:0;public bool HasUnlockedHangUp(int id)=>id==1;public bool HasFurniture(EFurniture type)=>false;
    public int Level=>1;public int SubRank=>1;public IReadOnlyDictionary<PropType,long> NonBattleProps=>throw Outside();public IHomeWindowPropertyCenter PlayerPropCenter=>throw Outside();
    public int GetHangUpLevel(int id)=>1;public int GetBlessingLevel()=>0;public int GetLostGoddessActiveCount()=>0;public bool CanFreeFastHangUp(int id)=>false;public bool CanGetExp()=>true;public bool IsFunctionLimit(int id)=>false;public bool GetHangUpQuickReceiveState(EFurniture type)=>true;public int GetOutputAdditionPropValue(int type)=>0;public bool IsMapRegionPublic(int id,int region)=>true;
    public bool TryGetFastHangUpTip(int id,long hours,out int item,out HomeWindowText title,out HomeWindowText content){item=PlayerExpItemId;title=new HomeWindowText("ui_fast_hang_up_exp_exceed_tip_title");content=new HomeWindowText("ui_fast_hang_up_exp_exceed_tip_content");return exceed;}
    public void Award(IList data,object source)=>calls.Add("award");public event Action<int> OnFastHangUp;public event Action<object> OnAddOutputAdditionData;
    public IProfessionImageResourceLoader Resources=>images;public int HomeBedSpeedConfirmFunctionId=>163;public int PlayerExpItemId=>(int)rules["tables"]["game_settings"]["PlayerExpItemId"];
    public HomeWindowHangUpInfo GetHangUpInfo(int id)=>rules["tables"]["hang_up"].Single(x=>(int)x["Id"]==id).ToObject<HomeWindowHangUpInfo>();public bool TryGetMainHangUpInfo(EFurniture type,out HomeWindowHangUpInfo info){throw Outside();}
    public HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture type)=>throw Outside();public HomeWindowFunctionLimitInfo GetFunctionLimitInfo(int id)=>throw Outside();public string GetFurnitureTypeNameKey(EFurniture type)=>throw Outside();public string GetFurnitureIconPrefabPath(EFurniture type)=>throw Outside();
    public string GetItemNameKey(int id)=>"item_"+id+"_name";public string GetItemDescKey(int id)=>throw Outside();public string GetHangUpBaseOutput(int id)=>throw Outside();public string Text(string key)=>texts.TryGetValue(key,out var value)?value:key;public string TextFormatted(string key,params object[] args)=>string.Format(Text(key),args);public string FormatCount(long count)=>count.ToString(CultureInfo.InvariantCulture);
    public double GetHangUpReward(int id,double hours,int level=-1,bool isBase=false)=>1200*hours;public long GetHangUpMaxRewardStore(int id,int level=-1)=>throw Outside();public long GetHangUpAwardCount(int id,out double seconds){throw Outside();}public long CalcOutputAdditionExtStore(int id,long value)=>throw Outside();public long CalcOutputAdditionUnsettleExtRewardCount(int id)=>throw Outside();public double CalcOutputAdditionExtCount(int id,double count)=>throw Outside();public bool CanGetHangUpReward(int id)=>throw Outside();public double GetProp(IReadOnlyDictionary<PropType,long> p,PropType t)=>throw Outside();public void GetHangUpProp(HomeWindowHangUpInfo i,IReadOnlyDictionary<PropType,long> p,out double add,out double fix){throw Outside();}
    public int GetRankHighSubRank(int rank)=>throw Outside();public bool IsRankLimited(int rank)=>throw Outside();public void GetMaxPlayerLevel(int rank,out int b,out int l){throw Outside();}public bool TryGetFirstMainMapRegion(int rank,out HomeWindowMapRegionInfo region){throw Outside();}public int GetTipLevel(EFurniture type)=>throw Outside();public IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> callback)=>throw Outside();public void RegisterCondition(object condition,Action callback)=>throw Outside();public void DeregisterCondition(object condition,Action callback)=>throw Outside();public void SwitchQuickReceive(EFurniture type,bool value,Action<object,int> error)=>throw Outside();public void Collect(IReadOnlyList<int> ids,Action<IHomeWindowAwardMessage> success)=>throw Outside();public void MergeAward(IList data)=>calls.Add("merge");public void PopupAwardList(IList data)=>throw Outside();public void PublishCollected()=>throw Outside();public void SystemMessage(string text){messages++;}
    public void Confirm(string key,Action action)=>throw Outside();public void Confirm(HomeWindowText key,HomeWindowText title,Action action)=>confirmed=action;public void OpenFastHangUp(int id,GameObject parent)=>throw Outside();
    public void Close(GameObject value){calls.Add("close");popup.Hide(()=>{maskRoot.GetComponent<WorldWindowMask>().UnattachToWindow();value.SetActive(false);});}public void CloseLast()=>Close(root);public void ObserveDestroyed(WorldPopupWindow window,Action action)=>window.OnWindowDestory+=action;public void StopObservingDestroyed(WorldPopupWindow window,Action action)=>window.OnWindowDestory-=action;
    public void OperFastHangUp(int id,bool receive,Dictionary<int,long> cost,Action completed)=>throw Outside();public IDisposable SubscribeOutputAdditionFunctionState(Action callback)=>throw Outside();public IDisposable SubscribeNewDay(Action<object> callback)=>throw Outside();public bool IsOutputAdditionDisabled=>false;public List<(List<HomeWindowOutputAdditionInfo>,DateTime)> GetCurrOutputGroupPropList(OutputGroupType type)=>throw Outside();public void OpenOutputAddition(OutputGroupType type)=>throw Outside();public void OpenBlessingRule()=>throw Outside();
    public ItemIconConfig Config=>assets.iconConfig;public Object GrayConfig=>null;public IProfessionItemColors Colors=>this;IProfessionItemPlayer IProfessionItemIconContext.Player=>this;
    public IProfessionItemInfo GetItemInfo(int id)=>new IconInfo((JObject)rules["tables"]["item"].Single(x=>(int)x["ClassId"]==id));public object Compact(long value)=>CultureNumberArg.Compact(value);public object CultureText(string key)=>Text(key);public bool TryGetSourceData(int id,out IReadOnlyList<IProfessionItemSource> source){source=null;return false;}public bool CanMerge(int id,bool recurse)=>false;public object GetLocationInfos(int id)=>null;public void SetUIGray(Object config,GameObject value,bool gray){if(gray)throw Outside();}public void SetSoundSwitch(ProfessionItemSoundSwitch value,GameObject emitter)=>throw Outside();public GameObject Instantiate(GameObject prefab,Transform parent,EUIInstantiateFlag flag=EUIInstantiateFlag.All)=>Create(prefab,parent,false);public void InstantiateAsync(string prefab,Transform parent,Action<GameObject> callback)=>throw Outside();public void PopupInfoDialog(IProfessionItemData data,IWindowOperator owner,long count,Action<GameObject> callback)=>throw Outside();public void PopupInfoDialog(IProfessionItemInfo info,IWindowOperator owner,long count,Action<GameObject> callback)=>throw Outside();
    public Color Green=>colorAssets.Green;public Color White=>colorAssets.White;public Color LightRed=>colorAssets.LightRed;public Color GetTextColor(ThemeColorType theme,ColorType type)=>colorAssets.TextColor(theme,type);public string WrapColor(string text,Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+text+"</color>";public bool CanGetItemFromMapLocation(object location)=>false;
    public event Action<IProfessionItemData,long,object> OnItemCountUpdate{add{subscribers++;}remove{subscribers--;}}
    public RenderTexture GetRenderTexture()=>RenderTexture.GetTemporary(target.width/13,target.height/13);public void ReleaseRenderTexture(RenderTexture value)=>RenderTexture.ReleaseTemporary(value);public void TakeAShot(RenderTexture value){Canvas.ForceUpdateCanvases();cameraUi.Render();Zjcs.Presentation.World.OfficialDungeonPopupMask.Blur(target,value,blur);}
    sealed class IconInfo:IProfessionItemInfo{readonly JObject row;public IconInfo(JObject row){this.row=row;}public int ClassId=>(int)row["ClassId"];public string Icon=>(string)row["RawIcon"];public string IconEffect=>(string)row["IconEffect"];public Quality Quality=>(Quality)(int)row["Quality"];public bool CanOverlap=>(bool)row["CanOverlap"];public object GetFakeIcon()=>null;}
}
