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
using Zjcs.Official.HomeCurrencyUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
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
        if(Application.isBatchMode)throw new InvalidOperationException("Original confirmation text waits for end of frame; run this fixture without -batchmode through the internal-display launcher.");
        var work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(!Application.isBatchMode)EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"),false,"Game",false).Repaint();
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);if(error!=null)Debug.LogException(error);
        else Debug.Log("HOME_CURRENCY_WINDOWS_NATIVE_PASSED: complete original windows and pointer input; real-profile money quick-buy/exchange, original count observers, disk restoration, replay/overdraw rejection and disk-failure rollback; popup clips and subscription disposal. Other quick-buy entries, award/secondary-window presentation ports remain controlled; production HUD and Player remain pending.");
        EditorApplication.Exit(error==null?0:1);
    }
}
public sealed class HomeCurrencyWindowsNativeRunner:MonoBehaviour,IHomeCurrencyWindowServices,IHomeCurrencyWindowPlayer,
    IProfessionItemIconContext,IProfessionItemColors,IProfessionItemPlayer,IHomeCountColors,IWorldWindowBlurManager,IPlayerItemUIHost,IOfflineHomeCurrencyWindowPresentation
{
    Camera cameraUi;RenderTexture target;Canvas canvas;EventSystem events;Vector2 pointer;int errors,sounds,borrowedIcons,subscribers,details;
    OfflineHomeCurrencyAssets assets;OfflineHomeCurrencyWindows windows;
    Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog topBarAssets;
    OfflineMainlineProgressSession profile;ProfileSlotCatalog slots;string profileSlot,lastExchangeRequest;
    OfflineHomeCurrencyPlayer profilePlayer;OfflineHomeCurrencyExchange exchange;
    OfflineHomeQuickBuyCounters profileQuickCounters;DateTime quickCounterTime;
    OfflineHomeMoneyQuickBuy moneyQuickBuy;string lastMoneyBuyRequest;int moneyBuys;
    OfflineHomeCurrencyWindowPlayer windowPlayer;
    OfflineHomeCurrencyActions currencyActions;
    FontAssetFontService fonts;ScreenAdaptService screen;OfflineHomeTopBar topBar;OfflinePartnerUiSoundScope soundScope;IDisposable metrics;
    GameObject active;WorldPopupWindow popup;WorldConfirmDialog confirmation;Material blur;
    readonly Dictionary<int,long> counts=new Dictionary<int,long>{{1,100},{2,500},{15,5}};
    readonly List<string> calls=new List<string>();Action<IProfessionItemData,long,object> changed;
    Action<HomeCurrencyEnterNewDay> day;Action<HomeCurrencyAutoIncreaseEvent> increase;readonly List<Func<HomeCurrencyAwardMessage,bool>> awardHandlers=new List<Func<HomeCurrencyAwardMessage,bool>>();
    bool banCost,allowLimit=true,payAward;long alreadyBought;int lack,confirm,directPay,exchanges,awards;JObject proof;
    IEnumerator Start()
    {
        Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=(text,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;};
        var steps=new Stack<IEnumerator>();steps.Push(Exercise());
        while(steps.Count>0){bool next;object current=null;try{next=steps.Peek().MoveNext();if(next)current=steps.Peek().Current;}catch(Exception e){HomeCurrencyWindowsNativeValidation.Finish(e);yield break;}if(!next){steps.Pop();continue;}if(current is IEnumerator nested){steps.Push(nested);continue;}yield return current;}
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
        soundScope=new OfflinePartnerUiSoundScope(id=>((JObject)proof["audio"]).Properties().Select(p=>p.Value).Where(row=>(uint)row["event"]["id"]==id).Select(row=>Load<AudioClip>(row["pointer"])).SingleOrDefault());
        var dictionary=((JArray)JObject.Parse(assets.texts.text)["texts"]).ToDictionary(row=>(string)row["key"],row=>(string)row["text"]);
        LanguageManager.Initialize(new Language(dictionary));fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();
        screen=new ScreenAdaptService();screen.Initialize();metrics=ScreenMetrics.Bind(new Metrics());
        cameraUi=new GameObject("MutedCurrencyWindowCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cameraUi.orthographic=true;cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        target=new RenderTexture(608,1080,24);target.Create();cameraUi.targetTexture=target;
        canvas=new GameObject("NativeWindowCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalWindowPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var maskAssets=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();blur=new Material(maskAssets.BlurMaterial);
        topBarAssets=assets.CreateTopBarCatalog();topBar=new OfflineHomeTopBar(canvas.transform,this,OfflineHomeHudAssets.LoadDefault().defaultFont,assets.Text,topBarAssets);topBar.Root.SetActive(false);
        windows=new OfflineHomeCurrencyWindows(new OfflineHomeCurrencyWindowBindings{
            Canvas=canvas,CoroutineHost=this,TopBar=topBar.Manager,BlurMaskPrefab=maskAssets.BlurMaskPrefab,
            BlurManager=this,Fonts=fonts,Screen=screen,Icons=this,PostSound=Post},this,assets);
        windows.WindowOpened+=root=>{foreach(var touch in root.GetComponentsInChildren<LongTouch>(true))touch.PointerPositionOverride=()=>pointer;};
        windows.WindowClosed+=root=>{if(root==active)active=null;};
        var info=assets.QuickBuy(1);Require(info.PriceList.SequenceEqual(new long[]{30,50,100})&&info.PriceBuyCount.SequenceEqual(new long[]{5,5,-1}),"selected official price tiers");
        Require(info.GetPrice(4,3,out var price)&&price==130,"price crosses original tiers");Require(info.GetMaxBuyCount(0,500)==11,"original multi-tier affordable count");
        long award=info.GetAwardItemCount(Level,SubRank);Require(award>0,"original award coefficient");
        yield return Open(windows.ShowQuickBuy(1,award+1,true,null,true).gameObject);
        var quick=active.GetComponent<QuickBuyDialog>();var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");var group=Field<SetCountButtonGroup>(quick,"mSetCountButtonGroup");
        Require(Field<long>(quick,"mCurrentValue")==2&&cost.CostItemCount==60,"original ceiling of required count and cost");
        Require(Field<ItemIcon>(quick,"mIcon").IconSprite==Load<Sprite>(proof["currency_images"]["1"]["pointer"]),"actual original money icon");
        var firstIcon=Field<ItemIcon>(quick,"mIcon");int firstIconId=firstIcon.GetInstanceID();
        Capture("home-quick-buy-native.png");Click(Field<Button>(group,"mAddBtn"));Require(cost.CostItemCount==90,"native add updates original tier price");
        Click(Field<Button>(group,"mMaxBtn"));Require(cost.CostItemCount==500&&Field<long>(quick,"mCurrentValue")==11,"native max includes all affordable tiers");
        counts[2]=0;Notify(2);Require(Field<long>(quick,"mCurrentValue")==1&&cost.CostItemCount==500,"original balance change clamps quantity while preserving displayed cost until next refresh");
        day(default);Require(cost.CostItemCount==30,"original new-day event refreshes selected price");
        Click(cost.Btn);Require(lack==1&&calls.Last()=="lack:2:30","original insufficient-cost callback");
        counts[2]=500;Notify(2);banCost=true;Click(cost.Btn);Require(calls.Last()=="error:1659","original ban-cost branch");banCost=false;
        cost.SetConditionChecker(()=>false);int before=calls.Count;Click(cost.Btn);Require(calls.Count==before,"original cost condition checker prevents action");cost.SetConditionChecker(null);
        allowLimit=false;Click(cost.Btn);yield return null;Require(active&&calls.Last()=="limit:1:"+award,"original limit check receives one award rather than total purchase");
        allowLimit=true;Click(cost.Btn);yield return WaitClosed();Require(calls.IndexOf("buy:1:1")<calls.LastIndexOf("close"),"request precedes immediate close");Require(borrowedIcons==0&&subscribers==0&&day==null&&increase==null,"quick-buy releases icon and all subscriptions");
        int cancellations=0;yield return Open(windows.ShowQuickBuy(1,0,true,()=>cancellations++).gameObject);
        quick=active.GetComponent<QuickBuyDialog>();Require(Field<ItemIcon>(quick,"mIcon").GetInstanceID()==firstIconId,"original icon pool reuses the returned icon");Click(Field<Button>(quick,"mCancelBtn"));yield return WaitClosed();Require(cancellations==1,"native cancel callback once");
        Require(!HomeCurrencyBuyLimit.CheckBuyLimitCountItem(-1,1,this,assets.Text,PopupLimitConfirm).GetAwaiter().GetResult(),"original missing item rejects buy-limit check");
        Require(HomeCurrencyBuyLimit.CheckBuyLimitCountItem(1,long.MaxValue,this,assets.Text,PopupLimitConfirm).GetAwaiter().GetResult(),"unlimited item skips held-count addition");
        counts[109]=29;Require(HomeCurrencyBuyLimit.CheckBuyLimitCountItem(109,1,this,assets.Text,PopupLimitConfirm).GetAwaiter().GetResult(),"original exact ownership limit needs no confirmation");
        counts[109]=30;var limited=HomeCurrencyBuyLimit.CheckBuyLimitCountItem(109,1,this,assets.Text,PopupLimitConfirm);Require(limited.Status==UniTaskStatus.Pending,"original excess ownership waits for confirmation");
        yield return WaitConfirmation();Capture("home-currency-limit-confirm-native.png");Require(confirmation.ContentText.GetComponent<TMP_Text>().text.Contains(assets.Item(109).Name),"original item name inserted into limit confirmation");Click(confirmation.CancelBtn);yield return WaitAllClosed();Require(!limited.GetAwaiter().GetResult(),"native cancel rejects excess ownership purchase");
        limited=HomeCurrencyBuyLimit.CheckBuyLimitCountItem(109,1,this,assets.Text,PopupLimitConfirm);yield return WaitConfirmation();Click(confirmation.ConfirmBtn);yield return WaitAllClosed();Require(limited.GetAwaiter().GetResult(),"native confirm allows original excess ownership branch");
        limited=HomeCurrencyBuyLimit.CheckBuyLimitCountItem(109,1,this,assets.Text,PopupLimitConfirm);yield return WaitConfirmation();var confirmMask=canvas.GetComponentsInChildren<WorldWindowBlurMask>().Single(mask=>mask.AttachWindow==confirmation.PopupWindow);Click(confirmMask.Button,new Vector2(5,5));Require(windows.WindowCount==1&&limited.Status==UniTaskStatus.Pending,"original mask guard ignores clicks during the first half second");yield return new WaitForSeconds(.55f);Click(confirmMask.Button,new Vector2(5,5));yield return WaitAllClosed();Require(!limited.GetAwaiter().GetResult(),"original mask-close automatically runs the cancel callback");
        InitializeExchangeProfile();
        CheckQuickBuyCounters();
        profileQuickCounters=new OfflineHomeQuickBuyCounters(assets.CreateQuickBuyCalendar(8));
        quickCounterTime=new DateTime(2026,11,9,0,0,0,DateTimeKind.Utc);
        windowPlayer=new OfflineHomeCurrencyWindowPlayer(profile,profilePlayer,profileQuickCounters,
            (id,count)=>new ItemData(id,new OfflinePartnerItemInfo(OfficialPartnerAssetCatalog.LoadDefault(),id),count),IsBanCostItem,()=>quickCounterTime);
        CheckWindowPlayer();
        Require(profile.ApplyInventoryTransaction("quick-counter-tier:"+Guid.NewGuid().ToString("N"),save=>profileQuickCounters.AddBuyCount(save.Player,EQuickBuyRefreshType.Daily,1,4,quickCounterTime)).IsApplied,"persist original second price tier");
        yield return Open(windows.ShowQuickBuy(1).gameObject);
        quick=active.GetComponent<QuickBuyDialog>();cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");
        Require(cost.CostItemCount==50,"native quick-buy reads real saved daily purchases for second price tier");Capture("home-quick-buy-saved-counter-native.png");
        quickCounterTime=quickCounterTime.AddDays(1);day(default);Require(cost.CostItemCount==30,"native new-day handler reads expired saved count and restores original first price tier");
        Click(Field<Button>(quick,"mCancelBtn"));yield return WaitClosed();
        yield return ExerciseMoneyQuickBuy();
        yield return Open(windows.ShowExchange().gameObject);
        var pay=active.GetComponent<PayMoneyChangeDialog>();Require(pay.SetCountSlider.CurrentCount==1&&pay.CountSliderGo.activeSelf,"original pay default selection");
        Click(pay.SetCountSlider.AddBtn);Require(Field<long>(pay,"mCurrentCount")==2,"native exchange add");Click(pay.SetCountSlider.MaxBtn);Require(Field<long>(pay,"mCurrentCount")==5,"native exchange maximum");Capture("home-pay-change-native.png");
        Click(pay.ConfirmBtn);Require(GetItemAmount(15)==0&&GetItemAmount(2)==505,"actual exchange balances before original success close");Require(canvas.GetComponentsInChildren<MePlayerItemUI>(true).Any(item=>item.GetComponentsInChildren<TMP_Text>(true).Any(text=>text.text=="505")),"original count observer updates before the top bar hides at exit-animation start");yield return WaitClosed();Require(exchanges==1&&calls.Contains("exchange:5"),"original exchange request and success close");Require(awardHandlers.Count==0&&subscribers==0,"pay releases award and item handlers");
        Require(GetItemAmount(15)==0&&GetItemAmount(2)==505,"native confirm commits both actual inventory changes");
        var restored=OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);Require(restored.InventoryCount(15)==0&&restored.InventoryCount(2)==505,"exchange survives reopening the real profile");
        string committed=File.ReadAllText(slots.GetProgressPath(profileSlot));Require(exchange.Exchange(lastExchangeRequest,5).Status==ProgressionResultStatus.Unchanged&&committed==File.ReadAllText(slots.GetProgressPath(profileSlot)),"replayed exchange request never spends or awards twice");
        Require(!exchange.Exchange("overdraw",1).IsApplied&&!exchange.Exchange("zero",0).IsApplied&&committed==File.ReadAllText(slots.GetProgressPath(profileSlot)),"invalid and insufficient exchanges leave the saved profile unchanged");
        SetBalance(15,2);yield return Open(windows.ShowExchange(3).gameObject);
        pay=active.GetComponent<PayMoneyChangeDialog>();Require(!pay.CountSliderGo.activeSelf&&pay.ExchangeFixedTxt.gameObject.activeSelf&&Field<long>(pay,"mCurrentCount")==3,"original explicit quantity keeps fixed mode");
        Click(pay.ConfirmBtn);Require(confirm==1&&exchanges==1,"insufficient fixed amount asks before direct pay");yield return WaitConfirmation();Capture("home-currency-shortage-confirm-native.png");
        Require(windows.WindowCount==2&&pay.gameObject.activeInHierarchy,"original shortage confirmation stacks above the exchange window");
        Click(confirmation.CancelBtn);yield return WaitConfirmationClosed();Require(active&&directPay==0&&windows.WindowCount==1,"native shortage cancellation keeps the exchange window open");
        Click(pay.ConfirmBtn);yield return WaitConfirmation();
        payAward=false;Require(!awardHandlers.Single()(new HomeCurrencyAwardMessage()),"non-pay award continues propagation");payAward=true;Require(awardHandlers.Single()(new HomeCurrencyAwardMessage())&&awards==1,"pay award original interception");
        Click(confirmation.ConfirmBtn);yield return WaitAllClosed();Require(directPay==1,"confirmed shortage initializes direct-pay port then closes");
        SetBalance(15,0);yield return Open(windows.ShowExchange().gameObject);pay=active.GetComponent<PayMoneyChangeDialog>();Require(pay.SetCountSlider.CurrentCount==0&&Field<long>(pay,"mCurrentCount")==0,"zero balance original disabled range");Click(pay.CancelBtn);yield return WaitClosed();
        SetBalance(15,2);yield return Open(windows.ShowExchange().gameObject);pay=active.GetComponent<PayMoneyChangeDialog>();
        string primary=slots.GetProgressPath(profileSlot);string beforeFailure=File.ReadAllText(primary);
        using(var locked=new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read)){Click(pay.ConfirmBtn);Require(active&&calls.Last()=="exchange-rejected","failed disk commit keeps original exchange window open");}
        Require(File.ReadAllText(primary)==beforeFailure&&GetItemAmount(15)==2&&GetItemAmount(2)==505,"failed commit leaves both actual inventory amounts and primary save unchanged");
        Click(pay.CancelBtn);yield return WaitClosed();
        active=windows.ShowExchange().gameObject;windows.CloseExchange();windows.CloseExchange();Require(!active&&windows.WindowCount==0,"closing a preparing window cancels its mask coroutine once");
        yield return Open(windows.ShowExchange().gameObject);windows.Dispose();windows.Dispose();Require(!active&&windows.WindowCount==0&&subscribers==0&&awardHandlers.Count==0,"disposing active native windows releases all window observers");
        yield return ExerciseComposedServices();
        Require(Field<Delegate>(windowPlayer,"OnItemCountUpdate")==null&&Field<Delegate>(windowPlayer,"OnAutoIncrease")==null,"native windows release shared player event projections");windowPlayer.Dispose();windowPlayer.Dispose();
        bool disposedRead=false;try{windowPlayer.GetItemAmount(1);}catch(ObjectDisposedException){disposedRead=true;}Require(disposedRead&&profilePlayer.GetItemAmount(1)==GetItemAmount(1),"disposed window-player adapter leaves external shared quantities alive");
        Require(Field<Delegate>(profilePlayer,"OnItemCountUpdate").GetInvocationList().Length==1&&Field<Delegate>(profilePlayer,"OnAutoIncreaseItemDataUpdate")==null,"actual profile observers release before adapter disposal");profilePlayer.Dispose();profilePlayer=null;
        windows.Dispose();windows.Dispose();topBar.Dispose();Object.Destroy(topBarAssets);yield return null;fonts.Dispose();soundScope.Dispose();screen.Dispose();metrics.Dispose();Require(errors==0&&sounds>0&&subscribers==0,"runtime, audio and disposal checks");
    }
    void InitializeExchangeProfile()
    {
        string root=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"currency-exchange-profile-"+Guid.NewGuid().ToString("N"));
        var creation=CharacterCreationOfflineService.OpenDefault(root);profileSlot=creation.CreateSlot();var draft=creation.OpenDraft(profileSlot);draft.name="兑换验证";
        Require(creation.TryCreateCharacter(profileSlot,draft,out string error),error);slots=new ProfileSlotCatalog(root);profile=OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);
        SetBalance(1,100);SetBalance(2,500);SetBalance(15,5);
        profilePlayer=new OfflineHomeCurrencyPlayer(profile);profilePlayer.OnItemCountUpdate+=(item,previous,source)=>changed?.Invoke(new ItemData(item.Id,new IconInfo(assets.ItemRow(item.Id)),item.Count),previous,source);
        exchange=new OfflineHomeCurrencyExchange(profile,assets);
    }
    void SetBalance(int id,long count)
    {
        profile.Reload();Require(profile.ApplyInventoryTransaction("exchange-fixture-balance:"+Guid.NewGuid().ToString("N"),save=>{
            long held=OfflineItemAutoIncrease.Count(save.Player.Inventory.FirstOrDefault(row=>row.ItemId==id),DateTime.UtcNow);long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(count>held)Require(OfflineInventoryOperations.Grant(save.Player,id,count-held,now).IsApplied,"seed real exchange balance");
            else if(count<held)Require(OfflineInventoryOperations.Consume(save.Player,id,held-count,now).IsApplied,"set real exchange balance");
        }).IsApplied,"commit isolated exchange balance");profilePlayer?.SynchronizeCommittedPlayer();
    }
    void CheckQuickBuyCounters()
    {
        // UTC+8/UTC/UTC-5/UTC+9 are test inputs, not a production timezone choice.
        var calendar=assets.CreateQuickBuyCalendar(8);var counters=new OfflineHomeQuickBuyCounters(calendar);
        var sunday=new DateTime(2026,11,8,23,59,59,DateTimeKind.Utc).AddTicks(9999999);
        var monday=sunday.AddTicks(1);var tuesday=monday.AddDays(1);
        Require(calendar.GetDailyResetTimeOfDay()==(0,0,0)&&calendar.WeeklyStartDayOfWeek==DayOfWeek.Monday,"decoded original reset settings with explicit UTC+8 test input");
        foreach(int zone in new[]{0,-5,9}){
            var c=assets.CreateQuickBuyCalendar(zone);
            var boundary=new DateTime(2026,11,9,8,0,0,DateTimeKind.Utc).AddHours(-zone);
            Require(!c.IsSameDay(boundary.AddTicks(-1),boundary)&&c.IsSameDay(boundary,boundary.AddHours(23)),"exact daily reset boundary at test timezone "+zone);
            Require(!c.IsSameWeek(boundary.AddTicks(-1),boundary)&&c.IsSameWeek(boundary,boundary.AddDays(6)),"exact weekly reset boundary at test timezone "+zone);
        }
        Require(counters.GetBuyCount(profile.Save.Player,1,EQuickBuyRefreshType.Daily,sunday)==0,"new character has no purchases");
        long money=profile.InventoryCount(1),diamonds=profile.InventoryCount(2);
        string receipt="quick-buy-counter-fixture:"+Guid.NewGuid().ToString("N");
        Action<OfflineMainlineSave> purchase=save=>{
            Require(OfflineInventoryOperations.Consume(save.Player,2,30,0).IsApplied,"atomic counter test cost");
            Require(OfflineInventoryOperations.Grant(save.Player,1,1500,0).IsApplied,"atomic counter test award");
            counters.AddBuyCount(save.Player,EQuickBuyRefreshType.Daily,1,4,sunday);
            counters.AddBuyCount(save.Player,EQuickBuyRefreshType.Daily,109,2,sunday);
            counters.AddBuyCount(save.Player,EQuickBuyRefreshType.Weekly,108,3,sunday);
            counters.AddBuyCount(save.Player,EQuickBuyRefreshType.None,2,7,sunday);
        };
        Require(profile.ApplyInventoryTransaction(receipt,purchase).IsApplied,"commit inventory and counters together");
        string path=slots.GetProgressPath(profileSlot),committed=File.ReadAllText(path);
        Require(profile.ApplyInventoryTransaction(receipt,purchase).Status==ProgressionResultStatus.Unchanged&&File.ReadAllText(path)==committed,"replayed counter transaction does not spend or increment again");
        var restored=OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);
        Require(restored.InventoryCount(1)==money+1500&&restored.InventoryCount(2)==diamonds-30&&counters.GetBuyCount(restored.Save.Player,1,EQuickBuyRefreshType.Daily,sunday)==4,"inventory and original counts survive actual profile reopening");
        Require(counters.GetBuyCount(restored.Save.Player,1,EQuickBuyRefreshType.Daily,monday)==0&&counters.GetBuyCount(restored.Save.Player,108,EQuickBuyRefreshType.Weekly,monday)==0&&counters.GetBuyCount(restored.Save.Player,2,EQuickBuyRefreshType.None,monday)==7,"daily and weekly expire independently of permanent counts");
        Require(File.ReadAllText(path)==committed&&counters.GetBuyCount(restored.Save.Player,109,EQuickBuyRefreshType.Daily,sunday)==2,"reading an expired period does not clear or write old data");
        Require(profile.ApplyInventoryTransaction("quick-counter-next:"+Guid.NewGuid().ToString("N"),save=>counters.AddBuyCount(save.Player,EQuickBuyRefreshType.Daily,1,1,monday)).IsApplied,"first purchase in new period commits original reset");
        Require(counters.GetBuyCount(profile.Save.Player,1,EQuickBuyRefreshType.Daily,monday)==1&&counters.GetBuyCount(profile.Save.Player,109,EQuickBuyRefreshType.Daily,monday)==0&&counters.GetBuyCount(profile.Save.Player,2,EQuickBuyRefreshType.None,monday)==7,"first new-period addition clears all items in that refresh bucket only");
        committed=File.ReadAllText(path);bool failed=false;
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
            try{profile.ApplyInventoryTransaction("quick-counter-locked:"+Guid.NewGuid().ToString("N"),save=>{
                Require(OfflineInventoryOperations.Consume(save.Player,2,30,0).IsApplied,"locked transaction cost");
                counters.AddBuyCount(save.Player,EQuickBuyRefreshType.Daily,1,1,tuesday);
            });}catch(IOException){failed=true;}
        }
        Require(failed&&File.ReadAllText(path)==committed&&profile.InventoryCount(2)==diamonds-30&&counters.GetBuyCount(profile.Save.Player,1,EQuickBuyRefreshType.Daily,monday)==1,"failed disk commit retains inventory and previous counter period");
        var invalid=new OfflineQuickBuyProgress();invalid.Counters.Add(new OfflineQuickBuyCounterProgress{RefreshType=3});Require(!OfflineQuickBuyProgress.IsValid(invalid),"unknown persisted counter type rejects");
        SetBalance(1,money);SetBalance(2,diamonds);
        Debug.Log("HOME_QUICK_BUY_COUNTERS_PASSED: original periods, exact reset boundaries, nonmutating reads, whole-bucket clearing, real-profile restoration, atomic receipt and disk-failure rollback. Production timezone and purchase endpoint remain pending.");
    }
    void CheckWindowPlayer()
    {
        Require(windowPlayer.Level==profile.Save.Player.Level&&windowPlayer.SubRank==profile.Save.Player.SubRank&&windowPlayer.GetItemAmount(57)==profilePlayer.GetItemAmount(57),"window player reads actual progression and shared natural recovery");
        Require(windowPlayer.HasItem(1,100)&&!windowPlayer.HasItem(1,101)&&windowPlayer.HasItem(-1,0),"selected original HasItem comparison");
        banCost=true;Require(windowPlayer.IsBanCostItem(2),"window-player ban-cost dependency");banCost=false;
        int updates=0;Action<IProfessionItemData,long,object> changed=(item,previous,source)=>{Require(item.Id==1&&item.Count==101&&item.Info.ClassId==1&&previous==100&&source==null,"committed quantity event projection");updates++;};
        windowPlayer.OnItemCountUpdate+=changed;SetBalance(1,101);Require(updates==1,"one real inventory notification");windowPlayer.OnItemCountUpdate-=changed;SetBalance(1,100);
        int parameters=0;Action<HomeCurrencyAutoIncreaseEvent> increase=item=>{Require(item.ItemId==57,"actual auto-increase parameter event identity");parameters++;};
        windowPlayer.OnAutoIncrease+=increase;double speed=profile.Save.Player.Inventory.Single(row=>row.ItemId==57).AutoIncrease.IncreaseSpeed;
        Require(profile.ApplyInventoryTransaction("window-player-parameter:"+Guid.NewGuid().ToString("N"),save=>save.Player.Inventory.Single(row=>row.ItemId==57).AutoIncrease.IncreaseSpeed=speed+1).IsApplied,"change isolated actual stamina parameter");profilePlayer.SynchronizeCommittedPlayer();Require(parameters==1,"one actual parameter notification");windowPlayer.OnAutoIncrease-=increase;
        Require(profile.ApplyInventoryTransaction("window-player-parameter-restore:"+Guid.NewGuid().ToString("N"),save=>save.Player.Inventory.Single(row=>row.ItemId==57).AutoIncrease.IncreaseSpeed=speed).IsApplied,"restore isolated stamina parameter");profilePlayer.SynchronizeCommittedPlayer();
        Debug.Log("HOME_CURRENCY_WINDOW_PLAYER_PASSED: real progression, shared quantity/natural recovery, original ownership comparison, committed item/parameter notifications and explicit ban-cost dependency.");
    }
    IEnumerator ExerciseMoneyQuickBuy()
    {
        var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        var conditions=OfficialPartnerAssetCatalog.LoadDefault().Conditions;
        moneyQuickBuy=new OfflineHomeMoneyQuickBuy(profile,assets,assets.CreateQuickBuyCalendar(8),
            (node,save)=>conditions.IsMet(node.ToObject<Dictionary<string,object>>(),new OfflineHomeConditionPlayer(configuration,()=>save,_=>false)),
            (_,id)=>IsBanCostItem(id),()=>quickCounterTime);
        string path=slots.GetProgressPath(profileSlot),before=File.ReadAllText(path);
        Require(!moneyQuickBuy.Buy("level-not-met",1).IsApplied&&File.ReadAllText(path)==before,"actual original base-level condition rejects new character without writing");
        Require(profile.ApplyInventoryTransaction("quick-buy-fixture-level:"+Guid.NewGuid().ToString("N"),save=>save.Player.Level=10).IsApplied,"prepare level-ten purchase fixture");
        quickCounterTime=new DateTime(2026,11,9,0,0,0,DateTimeKind.Utc);
        long money=profile.InventoryCount(1),diamonds=profile.InventoryCount(2);
        long award=assets.QuickBuy(1).GetAwardItemCount(Level,SubRank);Require(award==1500,"selected original money reward");
        before=File.ReadAllText(path);banCost=true;Require(!moneyQuickBuy.Buy("cost-banned",1).IsApplied&&File.ReadAllText(path)==before,"original ban-cost port rejects without spending");banCost=false;
        yield return Open(windows.ShowQuickBuy(1).gameObject);
        var quick=active.GetComponent<QuickBuyDialog>();var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");Require(cost.CostItemCount==50,"real native purchase begins in saved second tier");
        Click(cost.Btn);Require(profile.InventoryCount(1)==money+award&&profile.InventoryCount(2)==diamonds-50&&QuickBuyGetItemBuyCount(1,EQuickBuyRefreshType.Daily)==6,"native buy commits original award, cost and count together");yield return WaitClosed();
        string request=lastMoneyBuyRequest,committed=File.ReadAllText(path);
        var restored=OfflineMainlineProgressSession.OpenCharacter(slots,profileSlot);
        Require(restored.InventoryCount(1)==money+award&&restored.InventoryCount(2)==diamonds-50&&profileQuickCounters.GetBuyCount(restored.Save.Player,1,EQuickBuyRefreshType.Daily,quickCounterTime)==6,"real native money purchase survives profile reopening");
        yield return Open(windows.ShowQuickBuy(1).gameObject);Capture("home-money-quick-buy-committed-native.png");quick=active.GetComponent<QuickBuyDialog>();Click(Field<Button>(quick,"mCancelBtn"));yield return WaitClosed();
        Require(moneyQuickBuy.Buy(request,1).Status==ProgressionResultStatus.Unchanged&&File.ReadAllText(path)==committed,"real money purchase receipt prevents double award and count");
        Require(moneyQuickBuy.Buy("cross-tier",5).IsApplied&&profile.InventoryCount(2)==diamonds-350&&profile.InventoryCount(1)==money+award*6&&QuickBuyGetItemBuyCount(1,EQuickBuyRefreshType.Daily)==11,"real batch purchase crosses original fifty/hundred price tiers");
        quickCounterTime=quickCounterTime.AddDays(1);
        Require(moneyQuickBuy.Buy("next-day",1).IsApplied&&profile.InventoryCount(2)==diamonds-380&&QuickBuyGetItemBuyCount(1,EQuickBuyRefreshType.Daily)==1,"new-period purchase spends first-tier cost and commits reset count");
        committed=File.ReadAllText(path);
        Require(!moneyQuickBuy.Buy("overdraw",long.MaxValue).IsApplied&&!moneyQuickBuy.Buy("zero",0).IsApplied&&File.ReadAllText(path)==committed,"invalid or unaffordable actual money purchase writes nothing");
        yield return Open(windows.ShowQuickBuy(1).gameObject);quick=active.GetComponent<QuickBuyDialog>();cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){Click(cost.Btn);Require(calls.Contains("money-buy-rejected"),"actual money purchase reports failed disk commit");}
        yield return WaitClosed();Require(File.ReadAllText(path)==committed&&profile.InventoryCount(2)==diamonds-380&&QuickBuyGetItemBuyCount(1,EQuickBuyRefreshType.Daily)==1,"original immediate close is retained while failed purchase leaves inventory and count unchanged");
        long heldMoney=profile.InventoryCount(1);SetBalance(1,long.MaxValue);committed=File.ReadAllText(path);
        Require(!moneyQuickBuy.Buy("award-overflow",1).IsApplied&&File.ReadAllText(path)==committed&&profile.InventoryCount(1)==long.MaxValue&&profile.InventoryCount(2)==diamonds-380,"award overflow rolls back the staged currency debit and counter update");
        SetBalance(1,heldMoney);SetBalance(1,money);SetBalance(2,diamonds);
        Debug.Log("HOME_MONEY_QUICK_BUY_PASSED: original base-level condition, native buy pointer, actual award/cost/count, tier crossing, new-day reset, profile reopening, receipt replay and debit/award/disk-failure rollback. Production timezone, error/award presentation and HUD remain pending.");
    }
    IEnumerator ExerciseComposedServices()
    {
        SetBalance(1,100);SetBalance(2,500);SetBalance(15,3);
        var service=new OfflineHomeCurrencyWindowServices(assets,windowPlayer,profilePlayer,moneyQuickBuy,exchange,this,
            kind=>"composed-"+kind+":"+Guid.NewGuid().ToString("N"));
        Require(!service.IconServiceAvailable&&ReferenceEquals(service.Player,windowPlayer),"composed service uses the actual shared player before attaching windows");
        var maskAssets=OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
        windows=new OfflineHomeCurrencyWindows(new OfflineHomeCurrencyWindowBindings{
            Canvas=canvas,CoroutineHost=this,TopBar=topBar.Manager,BlurMaskPrefab=maskAssets.BlurMaskPrefab,
            BlurManager=this,Fonts=fonts,Screen=screen,Icons=this,PostSound=Post},service,assets);
        windows.WindowOpened+=root=>{foreach(var touch in root.GetComponentsInChildren<LongTouch>(true))touch.PointerPositionOverride=()=>pointer;var dialog=root.GetComponent<WorldConfirmDialog>();if(dialog)confirmation=dialog;};
        windows.WindowClosed+=root=>{if(root==active)active=null;};
        service.AttachWindows(windows);Require(service.IconServiceAvailable,"composed service resolves original icon ownership after attachment");
        bool duplicate=false;try{service.AttachWindows(windows);}catch(InvalidOperationException){duplicate=true;}Require(duplicate,"window attachment cannot replace a live owner");
        Require(service.MoneyId==1&&service.DiamonId==2&&service.FlareStarId==15&&service.Pay.DiamonChangeScale==1&&service.GetItemCountLimit(109)==30,"composed service projects selected original configuration");
        var configuration=OfficialCurrentHomeRuleAssetCatalog.LoadDefault();var partnerConditions=OfficialPartnerAssetCatalog.LoadDefault().Conditions;
        var conditionPlayer=new OfflineHomeConditionPlayer(configuration,()=>profile.Save,_=>false);var homeConditions=configuration.CreateConditions(partnerConditions);
        currencyActions=new OfflineHomeCurrencyActions(profile,profilePlayer,assets,windows,
            node=>partnerConditions.IsMet(node.ToObject<Dictionary<string,object>>(),conditionPlayer),
            ()=>configuration.Rows("function_limit").Where(row=>Convert.ToInt32(row["Type"])==49&&Convert.ToInt32(row["Key0"])==0&&Convert.ToInt32(row["Key1"])==0).Any(row=>!homeConditions.IsMet((Dictionary<string,object>)row["Condition"],conditionPlayer)),
            (id,count)=>{Require(count==0,"original home description requests zero required count");details++;calls.Add("home-detail:"+id);});
        topBar.Root.SetActive(true);yield return null;var homeItems=topBar.Root.GetComponentsInChildren<MePlayerItemUI>();
        Require(homeItems.Length==3&&Field<Button>(homeItems[0],"mAddBtn").gameObject.activeSelf&&!Field<Button>(homeItems[1],"mAddBtn").gameObject.activeSelf&&!Field<Button>(homeItems[2],"mAddBtn").gameObject.activeSelf,"actual level-ten money condition and locked-map payment condition control original home plus buttons");
        Require(currencyActions.GetAutoIncreaseMaxCount(57)==300&&System.Text.RegularExpressions.Regex.Replace(Field<Component>(homeItems[2],"mCountText").GetComponent<TMP_Text>().text,"<[^>]*>",string.Empty).Contains("300/"),"original stamina handler reads actual parameter maximum");
        foreach(var item in homeItems){Click(Field<Button>(item,"mBtn"));Require(calls.Last()=="home-detail:"+Field<PlayerItemUIItemIdProvider>(item,"mPlayerItemUIItemIdProvider").ItemId,"original home description pointer reaches explicit presentation port");}
        topBar.Root.SetActive(false);Require(profile.ApplyInventoryTransaction("home-actions-level-one:"+Guid.NewGuid().ToString("N"),save=>save.Player.Level=1).IsApplied,"seed actual lower level for original money visibility");profilePlayer.SynchronizeCommittedPlayer();topBar.Root.SetActive(true);yield return null;
        homeItems=topBar.Root.GetComponentsInChildren<MePlayerItemUI>();Require(!Field<Button>(homeItems[0],"mAddBtn").gameObject.activeSelf,"actual new-game level hides original money plus button");topBar.Root.SetActive(false);
        Require(profile.ApplyInventoryTransaction("home-actions-unlock:"+Guid.NewGuid().ToString("N"),save=>{save.Player.Level=10;save.Player.UnlockedMapClassIds.Add(11);}).IsApplied,"seed actual level/map state for original payment condition");profilePlayer.SynchronizeCommittedPlayer();topBar.Root.SetActive(true);yield return null;homeItems=topBar.Root.GetComponentsInChildren<MePlayerItemUI>();
        Require(Field<Button>(homeItems[0],"mAddBtn").gameObject.activeSelf&&Field<Button>(homeItems[1],"mAddBtn").gameObject.activeSelf,"actual original level and map conditions unlock both currency plus buttons");
        Click(Field<Button>(homeItems[0],"mAddBtn"));yield return Open(canvas.GetComponentsInChildren<QuickBuyDialog>(true).Single().gameObject);var originalQuick=active.GetComponent<QuickBuyDialog>();Click(Field<Button>(originalQuick,"mCancelBtn"));yield return WaitClosed();
        Click(Field<Button>(homeItems[1],"mAddBtn"));yield return Open(canvas.GetComponentsInChildren<PayMoneyChangeDialog>(true).Single().gameObject);
        var pay=active.GetComponent<PayMoneyChangeDialog>();Click(pay.SetCountSlider.MaxBtn);Click(pay.ConfirmBtn);
        Require(profilePlayer.GetItemAmount(15)==0&&profilePlayer.GetItemAmount(2)==503,"composed exchange commits and notifies before original success close");yield return WaitClosed();
        long purchased=profileQuickCounters.GetBuyCount(profile.Save.Player,1,EQuickBuyRefreshType.Daily,quickCounterTime);
        Click(Field<Button>(homeItems[0],"mAddBtn"));yield return Open(canvas.GetComponentsInChildren<QuickBuyDialog>(true).Single().gameObject);var quick=active.GetComponent<QuickBuyDialog>();var cost=Field<PlayerCostMoneyContent>(quick,"mCostMoneyContent");long price=cost.CostItemCount;
        Click(cost.Btn);Require(profilePlayer.GetItemAmount(1)==1600&&profilePlayer.GetItemAmount(2)==503-price&&profileQuickCounters.GetBuyCount(profile.Save.Player,1,EQuickBuyRefreshType.Daily,quickCounterTime)==purchased+1,"composed native purchase uses actual atomic award/cost/counter and shared notifications");yield return WaitClosed();
        Capture("home-currency-composed-home.png");yield return Open(windows.ShowQuickBuy(1).gameObject);
        var currentMask=canvas.GetComponentsInChildren<WorldWindowBlurMask>().Single();
        Require(popup.Canvas.overrideSorting&&popup.Canvas.sortingOrder>canvas.sortingOrder&&currentMask.Canvas.sortingOrder==popup.Canvas.sortingOrder-1&&currentMask.Canvas.sortingOrder>canvas.sortingOrder,"original OnWindowShowing assigns popup and mask above underlying native home bar after first activation");
        var visibleCurrencies=canvas.GetComponentsInChildren<MePlayerItemUI>();Require(visibleCurrencies.Length==4&&visibleCurrencies.Count(item=>item.transform.IsChildOf(topBar.Root.transform))==3&&visibleCurrencies.Single(item=>!item.transform.IsChildOf(topBar.Root.transform)).GetComponentInParent<Canvas>()==popup.Canvas,"native popup has one foreground currency and keeps original home pool intact behind blur mask");
        Capture("home-currency-composed-native.png");quick=active.GetComponent<QuickBuyDialog>();Click(Field<Button>(quick,"mCancelBtn"));yield return WaitClosed();
        SetBalance(109,31);var limit=service.CheckBuyLimitCountItem(109,1);confirmation=canvas.GetComponentsInChildren<WorldConfirmDialog>(true).Single();Require(limit.Status==UniTaskStatus.Pending,"composed original limit check opens original confirmation for actual held count");yield return WaitConfirmation();Click(confirmation.CancelBtn);yield return WaitAllClosed();Require(!limit.GetAwaiter().GetResult(),"composed limit cancellation resumes original awaiting branch");
        limit=service.CheckBuyLimitCountItem(109,1);confirmation=canvas.GetComponentsInChildren<WorldConfirmDialog>(true).Single();yield return WaitConfirmation();Click(confirmation.ConfirmBtn);yield return WaitAllClosed();Require(limit.GetAwaiter().GetResult(),"composed limit confirmation resumes original awaiting branch");
        SetBalance(15,2);yield return Open(windows.ShowExchange().gameObject);pay=active.GetComponent<PayMoneyChangeDialog>();string path=slots.GetProgressPath(profileSlot),saved=File.ReadAllText(path);
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){Click(pay.ConfirmBtn);Require(active&&calls.Last()=="composed-failure:2","composed exchange reports failed persistence without closing");}
        Require(File.ReadAllText(path)==saved&&profilePlayer.GetItemAmount(15)==2,"composed failed exchange leaves actual save and held balance unchanged");Click(pay.CancelBtn);yield return WaitClosed();
        int days=0,parameters=0;var dayToken=service.SubscribeEnterNewDay(_=>days++);var parameterToken=service.SubscribeAutoIncrease(_=>parameters++);day(default);Require(days==1,"composed calendar subscription forwards original day message");
        double speed=profile.Save.Player.Inventory.Single(row=>row.ItemId==57).AutoIncrease.IncreaseSpeed;
        Require(profile.ApplyInventoryTransaction("composed-parameter:"+Guid.NewGuid().ToString("N"),save=>save.Player.Inventory.Single(row=>row.ItemId==57).AutoIncrease.IncreaseSpeed=speed+1).IsApplied,"commit composed actual parameter change");profilePlayer.SynchronizeCommittedPlayer();Require(parameters==1,"composed service forwards actual stamina parameter notification");
        topBar.Root.SetActive(false);currencyActions=null;windows.Dispose();service.Dispose();service.Dispose();dayToken.Dispose();parameterToken.Dispose();Require(day==null&&Field<Delegate>(windowPlayer,"OnAutoIncrease")==null,"composed service releases outstanding calendar and parameter tokens idempotently");
        bool disposed=false;try{service.PlayerDiamonChange(1,null);}catch(ObjectDisposedException){disposed=true;}Require(disposed&&profilePlayer.GetItemAmount(15)==2,"disposed composed service rejects transactions and leaves externally owned profile source alive");
        Debug.Log("HOME_CURRENCY_WINDOW_SERVICES_PASSED: selected configuration, actual profile transaction composition, native exchange/quick-buy/limit confirmations, shared notifications, failed persistence and scope disposal. Enclosing secondary-window/navigation/calendar services remain explicit dependencies.");
    }
    IEnumerator Open(GameObject root)
    {
        active=root;popup=root.GetComponent<WorldPopupWindow>();
        Require(active.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"all original window scripts resolve");
        float deadline=Time.time+5;
        while(!root.activeInHierarchy){Require(Time.time<deadline,"original mask preparation completes");yield return null;}
        yield return new WaitForSeconds(popup.mShowClip.length+.2f);
        Require(popup.State==WorldPopupWindow.EState.Show,"original popup show clip finishes");
        var mask=canvas.GetComponentsInChildren<WorldWindowBlurMask>().Single(value=>value.AttachWindow==popup);
        Require(popup.Canvas.overrideSorting&&popup.Canvas.sortingOrder>canvas.sortingOrder&&mask.Canvas.overrideSorting&&mask.Canvas.sortingOrder==popup.Canvas.sortingOrder-1,"original popup and blur mask retain showing-time canvas order");
    }
    void Post(AudioClip clip,GameObject owner){Require(clip&&owner,"original window PCM binding");sounds++;var source=owner.GetComponent<AudioSource>();if(!source)source=owner.AddComponent<AudioSource>();source.volume=0;source.PlayOneShot(clip);}
    public void Close(GameObject window){Require(window==active,"close current original window");calls.Add("close");windows.Close(window);}
    IEnumerator WaitClosed(){var deadline=Time.time+5;while(active){Require(Time.time<deadline,"original hide completes");yield return null;}yield return null;}
    void Notify(int id)=>changed?.Invoke(new ItemData(id,new IconInfo(assets.ItemRow(id)),counts[id]),0,null);
    void Click(Button button,Vector2? position=null)
    {
        Canvas.ForceUpdateCanvases();Require(button&&button.isActiveAndEnabled&&button.interactable,"native enabled button");var rect=(RectTransform)button.transform;pointer=position??RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center));var data=new PointerEventData(events){position=pointer,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"native unobscured first pointer: "+button.name);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name){Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);}
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static T Load<T>(JToken reference) where T:Object{var value=AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath((string)reference["guid"]));Require(value,"original reference "+reference);return value;}
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public int Level=>profile?.Save.Player.Level??10;public int SubRank=>profile?.Save.Player.SubRank??0;public long GetItemAmount(int id)=>profilePlayer!=null?profilePlayer.GetItemAmount(id):counts.TryGetValue(id,out var count)?count:0;
    public bool HasItem(int id,long count)=>GetItemAmount(id)>=count;public bool IsBanCostItem(int id)=>banCost;
    public long QuickBuyGetItemBuyCount(int id,EQuickBuyRefreshType type)=>profileQuickCounters==null?alreadyBought:profileQuickCounters.GetBuyCount(profile.Save.Player,id,type,quickCounterTime);
    public event Action<IProfessionItemData,long,object> OnItemCountUpdate {add{changed+=value;subscribers++;}remove{changed-=value;subscribers--;}}
    IHomeCurrencyWindowPlayer IHomeCurrencyWindowServices.Player=>(IHomeCurrencyWindowPlayer)windowPlayer??this;IProfessionItemPlayer IProfessionItemIconContext.Player=>this;
    public int MoneyId=>assets.MoneyId;public int DiamonId=>assets.DiamonId;public int FlareStarId=>assets.FlareStarId;public HomeCurrencyPaySettings Pay=>assets.Pay;
    public HomeCurrencyWindowItemInfo GetItemInfo(int id)=>assets.Item(id);public QuickBuyInfo GetQuickBuyInfo(int id)=>assets.QuickBuy(id);
    public long GetItemCountLimit(int id)=>assets.CountLimit(id);public string GetItemNameKey(int id)=>"item_"+id+"_name";
    public string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type)=>"ui_quick_buy_"+type+"_limited";
    public IProfessionAssetOperation<Sprite> LoadSprite(string path)=>windows.LoadSprite(path);
    public bool IconServiceAvailable=>true;
    public ItemIcon GetIcon(HomeCurrencyWindowItemInfo item,Transform slot){var icon=windows.GetIcon(item,slot);borrowedIcons++;return icon;}
    public void RecycleIcon(ItemIcon icon){windows.RecycleIcon(icon);borrowedIcons--;}
    public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> callback){day+=callback;return new Release(()=>day-=callback);}
    public IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> callback){var player=windowPlayer;if(player!=null){player.OnAutoIncrease+=callback;return new Release(()=>player.OnAutoIncrease-=callback);}increase+=callback;return new Release(()=>increase-=callback);}
    public UniTask<bool> CheckBuyLimitCountItem(int id,long count){calls.Add("limit:"+id+":"+count);return UniTask.FromResult(allowLimit);}
    public void PlayerQuickBuy(int id,long count){calls.Add("buy:"+id+":"+count);if(moneyQuickBuy==null)return;Require(id==assets.MoneyId,"bound home money purchase");lastMoneyBuyRequest="native-money-"+(++moneyBuys);var result=moneyQuickBuy.Buy(lastMoneyBuyRequest,count);if(result.IsApplied)profilePlayer.SynchronizeCommittedPlayer();else calls.Add("money-buy-rejected");}
    public void PlayerDiamonChange(long count,Action success){exchanges++;calls.Add("exchange:"+count);lastExchangeRequest="native-confirm-"+exchanges;var result=exchange.Exchange(lastExchangeRequest,count);if(result.IsApplied){profilePlayer.SynchronizeCommittedPlayer();success();}else calls.Add("exchange-rejected");}
    public void ShowLackOfItem(int id,long count,IWindowOperator owner){lack++;calls.Add("lack:"+id+":"+count);}
    public void TransactionFailed(int itemId,ProgressionResult result)=>calls.Add("composed-failure:"+itemId);
    public void ShowError(int code,string name)=>calls.Add("error:"+code);public void SystemMessage(string text)=>calls.Add("message:"+text);
    public void PopupItemDetail(IWindowOperator owner,int id,long count){details++;Require(count==0,"original details initial count");}
    public void PopupConfirm(IWindowOperator owner,string key,Action accept){Require(key=="UI_Pay_Change_Item_Not_Enough_Confirm","original confirmation key");confirm++;confirmation=windows.ShowConfirm(key,null,accept);}
    void PopupLimitConfirm(Action<WorldConfirmDialog> initialize)=>windows.PopupConfirm(dialog=>{confirmation=dialog;initialize(dialog);});
    IEnumerator WaitConfirmation(){Require(confirmation,"original native confirmation created");float deadline=Time.time+5;while(!confirmation.gameObject.activeInHierarchy||confirmation.PopupWindow.State!=WorldPopupWindow.EState.Show){Require(Time.time<deadline,"original confirmation show completes");yield return null;}}
    IEnumerator WaitConfirmationClosed(){float deadline=Time.time+5;while(confirmation){Require(Time.time<deadline,"original confirmation hide completes");yield return null;}yield return null;}
    IEnumerator WaitAllClosed(){float deadline=Time.time+5;while(windows.WindowCount>0){Require(Time.time<deadline,"all original popups close");yield return null;}yield return null;}
    public void PopupDirectPay(IWindowOperator owner,Action initialized){directPay++;initialized();}
    public void ClosePayMoneyChangeDialog()=>Close(active);public bool AwardServiceAvailable=>true;
    public void RegisterAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler)=>awardHandlers.Add(handler);public void RemoveAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler)=>Require(awardHandlers.Remove(handler),"original award handler removed");
    public bool IsPayAward(HomeCurrencyAwardMessage message)=>payAward;public void PopupAward(HomeCurrencyAwardMessage message)=>awards++;
    public ItemIconConfig Config=>assets.iconConfig;public Object GrayConfig=>null;public IProfessionItemColors Colors=>this;
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
    public IPlayerItemUIHandler CreateHandler(PlayerItemType type,PlayerItemUIItemIdProvider provider)=>currencyActions!=null&&(type==PlayerItemType.Money||type==PlayerItemType.Diamon||type==PlayerItemType.TiliId)?currencyActions.CreateHandler(type,provider):new CurrencyHandler();
    public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type,PlayerItemUIItemIdProvider provider)=>profilePlayer!=null?profilePlayer.CreateCountProvider(type,provider):new Counter(this,provider.ItemId);
    public IDisposable LoadIcon(int id,Action<Sprite> callback){var row=proof["currency_images"][id.ToString()];callback(row!=null?Load<Sprite>(row["pointer"]):Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog.LoadDefault().Icon(id));return new Release(()=>{});}
    public Vector2 InputPosition=>pointer;public string ColorFormat(string value,Color color)=>WrapColor(value,color);
    sealed class CurrencyHandler:IPlayerItemUIHandler{public int ItemId{get;set;}public bool ShowAddBtn=>false;public void HandleAddBtn(){}public void HandleClick(Vector2 point){}}
    sealed class Counter:IPlayerItemUICountProvider {readonly HomeCurrencyWindowsNativeRunner owner;readonly int id;public Counter(HomeCurrencyWindowsNativeRunner owner,int id){this.owner=owner;this.id=id;}public long Count=>owner.GetItemAmount(id);public event Action<long,bool> OnItemCountUpdate;public void Init(){}public void Release(){}}
    sealed class Release:IDisposable{Action action;public Release(Action value){action=value;}public void Dispose(){var value=action;action=null;value?.Invoke();}}
    sealed class IconInfo:IProfessionItemInfo {readonly JObject row;public IconInfo(JObject value){row=value;}public int ClassId=>(int)row["ClassId"];public string Icon=>(string)row["RawIcon"];public string IconEffect=>(string)row["IconEffect"];public Quality Quality=>(Quality)(int)row["Quality"];public bool CanOverlap=>(bool)row["CanOverlap"];public object GetFakeIcon()=>null;}
    sealed class ItemData:IProfessionItemData{public ItemData(int id,IProfessionItemInfo info,long count){Id=id;Info=info;Count=count;}public int Id{get;}public long Count{get;}public IProfessionItemInfo Info{get;}public Quality GetQuality()=>Info.Quality;public bool CanExpire()=>false;}
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
