using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HomeCurrencyWindowsNativeValidation
{
    const string Key="Zjcs.HomeCurrencyWindowsNativeValidation";
    static HomeCurrencyWindowsNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))new GameObject("OriginalCurrencyWindowFixture").AddComponent<HomeCurrencyWindowsNativeRunner>();
        };
        EditorApplication.update+=()=>{if(SessionState.GetBool(Key,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))Finish(new TimeoutException("Original currency-window fixture timed out."));};
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
        else Debug.Log("HOME_CURRENCY_WINDOWS_NATIVE_PASSED: complete original quick-buy/pay-change prefabs, original configuration, simulated button input, price/quantity/balance branches, popup clips and subscription disposal. Transaction/award/secondary-window ports are controlled; production save and Player remain pending.");
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class HomeCurrencyWindowsNativeRunner:MonoBehaviour,IHomeCurrencyWindowServices,IHomeCurrencyWindowPlayer,
    IProfessionItemIconContext,IProfessionItemColors,IProfessionItemPlayer,IHomeCountColors,IWorldWindowBlurManager,IPlayerItemUIHost
{
    Camera cameraUi;RenderTexture target;Canvas canvas;EventSystem events;Vector2 pointer;int errors,sounds,borrowedIcons,subscribers,details;
    OfflineHomeCurrencyAssets assets;OfflineProfessionImageResourceLoader loader;ItemIconAttachmentManager attachments;
    Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog topBarAssets;
    FontAssetFontService fonts;ScreenAdaptService screen;OfflineHomeTopBar topBar;OfflinePartnerUiSoundScope soundScope;IDisposable metrics,uiScope;
    GameObject active,maskObject;WorldPopupWindow popup;WorldWindowBlurMask mask;Material blur;
    readonly Dictionary<int,long> counts=new Dictionary<int,long>{{1,100},{2,500},{15,5}};
    readonly List<string> calls=new List<string>();Action<IProfessionItemData,long,object> changed;
    Action<HomeCurrencyEnterNewDay> day;Action<HomeCurrencyAutoIncreaseEvent> increase;readonly List<Func<HomeCurrencyAwardMessage,bool>> awardHandlers=new List<Func<HomeCurrencyAwardMessage,bool>>();
    bool banCost,allowLimit=true,payAward;long alreadyBought;int lack,confirm,directPay,exchanges,awards;Action confirmAction;JObject proof;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=(text,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;};
        var steps=Exercise();for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}catch(Exception e){HomeCurrencyWindowsNativeValidation.Finish(e);yield break;}if(!next)break;yield return current;}
        HomeCurrencyWindowsNativeValidation.Finish(null);
    }
    IEnumerator Exercise()
    {
        proof=JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/HomeCurrencyWindowsNativeValidation/fixture.json").text);
        assets=ScriptableObject.CreateInstance<OfflineHomeCurrencyAssets>();
        assets.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("@RULES_PATH@");assets.texts=AssetDatabase.LoadAssetAtPath<TextAsset>("@TEXT_PATH@");
        assets.quickBuyPrefab=Load<GameObject>(proof["roots"]["ShopQuickBuyDialog"]);assets.payChangePrefab=Load<GameObject>(proof["roots"]["PayMoneyChangeDialog"]);
        assets.itemIconPrefab=Load<GameObject>(proof["roots"]["HomeCurrencyItemIconDefault"]);assets.iconConfig=Load<ItemIconConfig>(proof["icon_config"]);assets.attachmentConfig=Load<ItemIconAttachmentConfig>(proof["attachment_config"]);
        assets.images=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        assets.images.Entries=((JObject)proof["currency_images"]).Properties().Select(row=>new OfflineProfessionImageCatalog.Entry{
            Path="Assets/Image/"+(string)row.Value["item"]["RawIcon"],AssetGuid=(string)row.Value["manifest_asset"]["guid"],Asset=Load<Sprite>(row.Value["pointer"])}).Concat(new[]{new OfflineProfessionImageCatalog.Entry{
                Path=(string)proof["hint_attachment"]["asset"]["path"],AssetGuid=(string)proof["hint_attachment"]["asset"]["guid"],
                Asset=Load<GameObject>(proof["roots"]["HomeCurrency"+(string)proof["hint_attachment"]["name"]])}}).ToArray();
        loader=new OfflineProfessionImageResourceLoader(assets.images);ProfessionImageResourceBridge.Provider=image=>loader;
        attachments=new ItemIconAttachmentManager(loader,()=>assets.attachmentConfig,new GAction());
        soundScope=new OfflinePartnerUiSoundScope(id=>((JObject)proof["audio"]).Properties().Select(p=>p.Value).Where(row=>(uint)row["event"]["id"]==id).Select(row=>Load<AudioClip>(row["pointer"])).SingleOrDefault());
        var dictionary=((JArray)JObject.Parse(assets.texts.text)["texts"]).ToDictionary(row=>(string)row["key"],row=>(string)row["text"]);
        LanguageManager.Initialize(new Language(dictionary));fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();
        screen=new ScreenAdaptService();screen.Initialize();metrics=ScreenMetrics.Bind(new Metrics());
        cameraUi=new GameObject("MutedCurrencyWindowCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cameraUi.orthographic=true;cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();cameraUi.targetTexture=target;
        canvas=new GameObject("NativeWindowCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        uiScope=OfflineUiBindingModule.Register(canvas.transform,new OfflineUiBindings{Localize=assets.Text,Font=()=>OfflineHomeHudAssets.LoadDefault().defaultFont,Images=_=>loader,Pool=(scope,prefab,parent,keepWorld)=>InstantiateBound(prefab,parent,keepWorld)});
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalWindowPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var maskAssets=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();blur=new Material(maskAssets.BlurMaterial);
        topBarAssets=assets.CreateTopBarCatalog();topBar=new OfflineHomeTopBar(canvas.transform,this,OfflineHomeHudAssets.LoadDefault().defaultFont,assets.Text,topBarAssets);topBar.Root.SetActive(false);
        var info=assets.QuickBuy(1);Require(info.PriceList.SequenceEqual(new long[]{30,50,100})&&info.PriceBuyCount.SequenceEqual(new long[]{5,5,-1}),"selected official price tiers");
        Require(info.GetPrice(4,3,out var price)&&price==130,"price crosses original tiers");Require(info.GetMaxBuyCount(0,500)==11,"original multi-tier affordable count");
        long award=info.GetAwardItemCount(Level,SubRank);Require(award>0,"original award coefficient");
        yield return Open(assets.quickBuyPrefab,root=>root.GetComponent<QuickBuyDialog>().Init(1,award+1,true,null,true));
        var quick=active.GetComponent<QuickBuyDialog>();var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");var group=Field<SetCountButtonGroup>(quick,"mSetCountButtonGroup");
        Require(Field<long>(quick,"mCurrentValue")==2&&cost.CostItemCount==60,"original ceiling of required count and cost");
        Require(Field<ItemIcon>(quick,"mIcon").IconSprite==Load<Sprite>(proof["currency_images"]["1"]["pointer"]),"actual original money icon");
        Capture("home-quick-buy-native.png");Click(Field<Button>(group,"mAddBtn"));Require(cost.CostItemCount==90,"native add updates original tier price");
        Click(Field<Button>(group,"mMaxBtn"));Require(cost.CostItemCount==500&&Field<long>(quick,"mCurrentValue")==11,"native max includes all affordable tiers");
        counts[2]=0;Notify(2);Require(Field<long>(quick,"mCurrentValue")==1&&cost.CostItemCount==500,"original balance change clamps quantity while preserving displayed cost until next refresh");
        day(default);Require(cost.CostItemCount==30,"original new-day event refreshes selected price");
        Click(cost.Btn);Require(lack==1&&calls.Last()=="lack:2:30","original insufficient-cost callback");
        counts[2]=500;Notify(2);banCost=true;Click(cost.Btn);Require(calls.Last()=="error:1659","original ban-cost branch");banCost=false;
        cost.SetConditionChecker(()=>false);int before=calls.Count;Click(cost.Btn);Require(calls.Count==before,"original cost condition checker prevents action");cost.SetConditionChecker(null);
        allowLimit=false;Click(cost.Btn);yield return null;Require(active&&calls.Last()=="limit:1:"+award,"original limit check receives one award rather than total purchase");
        allowLimit=true;Click(cost.Btn);yield return WaitClosed();Require(calls.IndexOf("buy:1:1")<calls.LastIndexOf("close"),"request precedes immediate close");Require(borrowedIcons==0&&subscribers==0&&day==null&&increase==null,"quick-buy releases icon and all subscriptions");
        int cancellations=0;yield return Open(assets.quickBuyPrefab,root=>root.GetComponent<QuickBuyDialog>().Init(1,0,true,()=>cancellations++));
        quick=active.GetComponent<QuickBuyDialog>();Click(Field<Button>(quick,"mCancelBtn"));yield return WaitClosed();Require(cancellations==1,"native cancel callback once");
        yield return Open(assets.payChangePrefab,root=>root.GetComponent<PayMoneyChangeDialog>().Init());
        var pay=active.GetComponent<PayMoneyChangeDialog>();Require(pay.SetCountSlider.CurrentCount==1&&pay.CountSliderGo.activeSelf,"original pay default selection");
        Click(pay.SetCountSlider.AddBtn);Require(Field<long>(pay,"mCurrentCount")==2,"native exchange add");Click(pay.SetCountSlider.MaxBtn);Require(Field<long>(pay,"mCurrentCount")==5,"native exchange maximum");Capture("home-pay-change-native.png");
        Click(pay.ConfirmBtn);yield return WaitClosed();Require(exchanges==1&&calls.Contains("exchange:5"),"original exchange request and success close");Require(awardHandlers.Count==0&&subscribers==0,"pay releases award and item handlers");
        counts[15]=2;yield return Open(assets.payChangePrefab,root=>root.GetComponent<PayMoneyChangeDialog>().Init(3));
        pay=active.GetComponent<PayMoneyChangeDialog>();Require(!pay.CountSliderGo.activeSelf&&pay.ExchangeFixedTxt.gameObject.activeSelf&&Field<long>(pay,"mCurrentCount")==3,"original explicit quantity keeps fixed mode");
        Click(pay.ConfirmBtn);Require(confirm==1&&exchanges==1,"insufficient fixed amount asks before direct pay");
        payAward=false;Require(!awardHandlers.Single()(new HomeCurrencyAwardMessage()),"non-pay award continues propagation");payAward=true;Require(awardHandlers.Single()(new HomeCurrencyAwardMessage())&&awards==1,"pay award original interception");
        confirmAction();yield return WaitClosed();Require(directPay==1,"confirmed shortage initializes direct-pay port then closes");
        counts[15]=0;yield return Open(assets.payChangePrefab,root=>root.GetComponent<PayMoneyChangeDialog>().Init());pay=active.GetComponent<PayMoneyChangeDialog>();Require(pay.SetCountSlider.CurrentCount==0&&Field<long>(pay,"mCurrentCount")==0,"zero balance original disabled range");Click(pay.CancelBtn);yield return WaitClosed();
        topBar.Dispose();Object.Destroy(topBarAssets);attachments.Dispose();yield return null;uiScope.Dispose();fonts.Dispose();soundScope.Dispose();screen.Dispose();metrics.Dispose();Require(errors==0&&sounds>0&&subscribers==0,"runtime, audio and disposal checks");
    }
    IEnumerator Open(GameObject prefab,Action<GameObject> initialize)
    {
        var staging=new GameObject("InactiveWindowBindings",typeof(RectTransform));staging.transform.SetParent(canvas.transform,false);staging.SetActive(false);
        active=Object.Instantiate(prefab,staging.transform,false);Require(active.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original window scripts resolve");Bind(active);
        active.SetActive(false);initialize(active);active.transform.SetParent(canvas.transform,false);popup=active.GetComponent<WorldPopupWindow>();popup.SetCanvasSortingOrder(20);
        maskObject=Object.Instantiate(OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault().BlurMaskPrefab,staging.transform,false);maskObject.SetActive(false);maskObject.transform.SetParent(canvas.transform,false);mask=maskObject.GetComponent<WorldWindowBlurMask>();mask.BlurManager=this;
        yield return mask.Prepare(popup,false);active.SetActive(true);popup.Show();mask.AttachToWindow(popup);Object.Destroy(staging);
        yield return new WaitForSeconds(popup.mShowClip.length+.2f);Require(popup.State==WorldPopupWindow.EState.Show,"original popup show clip finishes");
    }
    void Bind(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(text.font)fonts.SetupFont(text.font);
        foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=assets.Text;
        foreach(var safe in root.GetComponentsInChildren<Zjcs.Official.HomeScreenUI.SafeArea>(true))safe.ScreenAdaptService=screen;
        foreach(var sound in root.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=Post;
        foreach(var sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))sound.Post=Post;
        foreach(var icon in root.GetComponentsInChildren<ItemIcon>(true))icon.Bind(this);
        foreach(var cell in root.GetComponentsInChildren<ItemIconAttachment>(true))cell.Bind(attachments);
        foreach(var progress in root.GetComponentsInChildren<WorldItemProgress>(true))progress.Compact=value=>CultureNumberArg.Compact(value).ToString();
        foreach(var control in root.GetComponentsInChildren<SetCountSlider>(true))control.GlobalColors=this;
        foreach(var touch in root.GetComponentsInChildren<LongTouch>(true))touch.PointerPositionOverride=()=>pointer;
        foreach(var bar in root.GetComponentsInChildren<TopBarHandler>(true))bar.Bind(topBar.Manager);
        foreach(var cost in root.GetComponentsInChildren<PlayerCostMoneyContent>(true))cost.Services=this;
        foreach(var quick in root.GetComponentsInChildren<QuickBuyDialog>(true))quick.Services=this;
        foreach(var pay in root.GetComponentsInChildren<PayMoneyChangeDialog>(true))pay.Services=this;
    }
    void Post(AudioClip clip,GameObject owner){Require(clip&&owner,"original window PCM binding");sounds++;var source=owner.GetComponent<AudioSource>();if(!source)source=owner.AddComponent<AudioSource>();source.volume=0;source.PlayOneShot(clip);}
    GameObject InstantiateBound(GameObject prefab,Transform parent,bool keepWorld){var staging=new GameObject("InactivePoolBindings");staging.SetActive(false);staging.transform.SetParent(canvas.transform,false);var value=Object.Instantiate(prefab,staging.transform,false);Bind(value);value.transform.SetParent(parent,keepWorld);Object.Destroy(staging);return value;}
    public void Close(GameObject window){Require(window==active,"close current original window");calls.Add("close");popup.Hide(()=>{mask.UnattachToWindow();Object.Destroy(maskObject);active.SetActive(false);Object.Destroy(active);active=null;});}
    IEnumerator WaitClosed(){var deadline=Time.time+5;while(active){Require(Time.time<deadline,"original hide completes");yield return null;}yield return null;}
    void Notify(int id)=>changed?.Invoke(new ItemData(id,new IconInfo(assets.ItemRow(id))),0,null);
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();Require(button&&button.isActiveAndEnabled&&button.interactable,"native enabled button");var rect=(RectTransform)button.transform;pointer=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center));var data=new PointerEventData(events){position=pointer,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native unobscured first pointer: "+button.name);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static T Load<T>(JToken reference) where T:Object{var value=AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath((string)reference["guid"]));Require(value,"original reference "+reference);return value;}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public int Level=>10;public int SubRank=>0;public long GetItemAmount(int id)=>counts.TryGetValue(id,out var count)?count:0;
    public bool HasItem(int id,long count)=>GetItemAmount(id)>=count;public bool IsBanCostItem(int id)=>banCost;
    public long QuickBuyGetItemBuyCount(int id,EQuickBuyRefreshType type)=>alreadyBought;
    public event Action<IProfessionItemData,long,object> OnItemCountUpdate {add{changed+=value;subscribers++;}remove{changed-=value;subscribers--;}}
    IHomeCurrencyWindowPlayer IHomeCurrencyWindowServices.Player=>this;IProfessionItemPlayer IProfessionItemIconContext.Player=>this;
    public int MoneyId=>assets.MoneyId;public int DiamonId=>assets.DiamonId;public int FlareStarId=>assets.FlareStarId;public HomeCurrencyPaySettings Pay=>assets.Pay;
    public HomeCurrencyWindowItemInfo GetItemInfo(int id)=>assets.Item(id);public QuickBuyInfo GetQuickBuyInfo(int id)=>assets.QuickBuy(id);
    public long GetItemCountLimit(int id)=>assets.CountLimit(id);public string GetItemNameKey(int id)=>"item_"+id+"_name";
    public string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type)=>"ui_quick_buy_"+type+"_limited";
    public IProfessionAssetOperation<Sprite> LoadSprite(string path)=>loader.LoadAsset<Sprite>("Assets/Image/"+path);
    public bool IconServiceAvailable=>true;
    public ItemIcon GetIcon(HomeCurrencyWindowItemInfo item,Transform slot){var staging=new GameObject("InactiveItemBinding");staging.SetActive(false);var go=Object.Instantiate(assets.itemIconPrefab,staging.transform,false);Bind(go);go.SetActive(false);go.transform.SetParent(slot,false);var icon=go.GetComponent<ItemIcon>();icon.Init(item.Id);go.SetActive(true);Object.Destroy(staging);borrowedIcons++;return icon;}
    public void RecycleIcon(ItemIcon icon){icon.Clear();icon.gameObject.SetActive(false);Object.Destroy(icon.gameObject);borrowedIcons--;}
    public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> callback){day+=callback;return new Release(()=>day-=callback);}
    public IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> callback){increase+=callback;return new Release(()=>increase-=callback);}
    public UniTask<bool> CheckBuyLimitCountItem(int id,long count){calls.Add("limit:"+id+":"+count);return UniTask.FromResult(allowLimit);}
    public void PlayerQuickBuy(int id,long count)=>calls.Add("buy:"+id+":"+count);
    public void PlayerDiamonChange(long count,Action success){exchanges++;calls.Add("exchange:"+count);success();}
    public void ShowLackOfItem(int id,long count,IWindowOperator owner){lack++;calls.Add("lack:"+id+":"+count);}
    public void ShowError(int code,string name)=>calls.Add("error:"+code);public void SystemMessage(string text)=>calls.Add("message:"+text);
    public void PopupItemDetail(IWindowOperator owner,int id,long count){details++;Require(count==0,"original details initial count");}
    public void PopupConfirm(IWindowOperator owner,string key,Action accept){Require(key=="UI_Pay_Change_Item_Not_Enough_Confirm","original confirmation key");confirm++;confirmAction=accept;}
    public void PopupDirectPay(IWindowOperator owner,Action initialized){directPay++;initialized();}
    public void ClosePayMoneyChangeDialog()=>Close(active);public bool AwardServiceAvailable=>true;
    public void RegisterAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler)=>awardHandlers.Add(handler);public void RemoveAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler)=>Require(awardHandlers.Remove(handler),"original award handler removed");
    public bool IsPayAward(HomeCurrencyAwardMessage message)=>payAward;public void PopupAward(HomeCurrencyAwardMessage message)=>awards++;
    public ItemIconConfig Config=>assets.iconConfig;public Object GrayConfig=>throw new InvalidOperationException("Unexpected gray branch");public IProfessionItemColors Colors=>this;
    IProfessionItemInfo IProfessionItemIconContext.GetItemInfo(int id)=>new IconInfo(assets.ItemRow(id));public object Compact(long count)=>CultureNumberArg.Compact(count);
    public object CultureText(string key)=>assets.Text(key);public bool TryGetSourceData(int id,out IReadOnlyList<IProfessionItemSource> sources){sources=null;return false;}
    public bool CanMerge(int id,bool recursion)=>false;public object GetLocationInfos(int id)=>null;public void SetUIGray(Object config,GameObject target,bool gray)=>throw new InvalidOperationException("Unexpected gray branch");
    public void SetSoundSwitch(ProfessionItemSoundSwitch value,GameObject emitter)=>throw new InvalidOperationException("Unexpected frame sound branch");
    public GameObject Instantiate(GameObject prefab,Transform parent,EUIInstantiateFlag flag=EUIInstantiateFlag.All)=>Object.Instantiate(prefab,parent,false);
    public void InstantiateAsync(string prefab,Transform parent,Action<GameObject> callback)=>throw new InvalidOperationException("Unexpected item effect branch");
    public void PopupInfoDialog(IProfessionItemData data,IWindowOperator owner,long count,Action<GameObject> callback)=>PopupItemDetail(owner,data.Id,count);
    public void PopupInfoDialog(IProfessionItemInfo info,IWindowOperator owner,long count,Action<GameObject> callback)=>PopupItemDetail(owner,info.ClassId,count);
    public Color White=>assets.White;public Color Green=>assets.Green;public Color LightRed=>assets.LightRed;public Color GetTextColor(ThemeColorType theme,ColorType type)=>assets.TextColor(theme,type);
    public bool CanGetItemFromMapLocation(object location)=>false;public string WrapColor(string text,Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+text+"</color>";
    public RenderTexture GetRenderTexture()=>RenderTexture.GetTemporary(target.width/13,target.height/13);public void ReleaseRenderTexture(RenderTexture texture)=>RenderTexture.ReleaseTemporary(texture);
    public void TakeAShot(RenderTexture texture){Canvas.ForceUpdateCanvases();cameraUi.Render();Zjcs.Presentation.World.OfficialDungeonPopupMask.Blur(target,texture,blur);}
    public IPlayerItemUIHandler CreateHandler(PlayerItemType type,PlayerItemUIItemIdProvider provider)=>new CurrencyHandler();
    public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type,PlayerItemUIItemIdProvider provider)=>new Counter(this,provider.ItemId);
    public IDisposable LoadIcon(int id,Action<Sprite> callback){var row=proof["currency_images"][id.ToString()];callback(row!=null?Load<Sprite>(row["pointer"]):Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog.LoadDefault().Icon(id));return new Release(()=>{});}
    public Vector2 InputPosition=>pointer;public string ColorFormat(string value,Color color)=>WrapColor(value,color);
    sealed class CurrencyHandler:IPlayerItemUIHandler{public int ItemId{get;set;}public bool ShowAddBtn=>false;public void HandleAddBtn(){}public void HandleClick(Vector2 point){}}
    sealed class Counter:IPlayerItemUICountProvider {readonly HomeCurrencyWindowsNativeRunner owner;readonly int id;public Counter(HomeCurrencyWindowsNativeRunner owner,int id){this.owner=owner;this.id=id;}public long Count=>owner.GetItemAmount(id);public event Action<long,bool> OnItemCountUpdate;public void Init(){}public void Release(){}}
    sealed class Release:IDisposable{Action action;public Release(Action value){action=value;}public void Dispose(){var value=action;action=null;value?.Invoke();}}
    sealed class IconInfo:IProfessionItemInfo {readonly JObject row;public IconInfo(JObject value){row=value;}public int ClassId=>(int)row["ClassId"];public string Icon=>(string)row["RawIcon"];public string IconEffect=>(string)row["IconEffect"];public Quality Quality=>(Quality)(int)row["Quality"];public bool CanOverlap=>(bool)row["CanOverlap"];public object GetFakeIcon()=>null;}
    sealed class ItemData:IProfessionItemData{public ItemData(int id,IProfessionItemInfo info){Id=id;Info=info;}public int Id{get;}public long Count=>0;public IProfessionItemInfo Info{get;}public Quality GetQuality()=>Info.Quality;public bool CanExpire()=>false;}
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
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
}
