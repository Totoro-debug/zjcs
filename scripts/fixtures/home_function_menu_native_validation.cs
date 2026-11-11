using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

public sealed partial class HomeFunctionMenuNativeRunner
{
    IEnumerator VerifyNativeMenu()
    {
        Require(MenuPrefab,"complete original menu prefab loaded");
        if(!Object.FindObjectOfType<AudioListener>())uiCamera.gameObject.AddComponent<AudioListener>();
        AudioListener.volume=0;
        var host=new MenuServices();var tips=new MenuTips();
        var mount=new GameObject("NativeMenuScope",typeof(RectTransform));mount.SetActive(false);
        mount.transform.SetParent(view.Root.transform.parent,false);
        var rect=mount.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var menu=Object.Instantiate(MenuPrefab,mount.transform,false);host.Menu=menu;
        Require(menu.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x!=null),"all original menu scripts resolved");
        foreach(var text in menu.GetComponentsInChildren<TMP_Text>(true))text.font=Items()[0].GetComponentInChildren<TMP_Text>(true).font;
        foreach(var text in menu.GetComponentsInChildren<WorldMultiLanguageText>(true))
        { var key=text.myKey;var original=text.Text;text.Localizer=value=>value==key&&value!="{0}"?original:value; }
        foreach(var component in menu.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var property=component.GetType().GetProperty("Services",BindingFlags.Instance|BindingFlags.Public);
            if(property!=null&&property.CanWrite&&property.PropertyType.IsInstanceOfType(host))property.SetValue(component,host);
        }
        foreach(var linker in menu.GetComponentsInChildren<TipDotNodeLinker>(true)){linker.Controller=tips;linker.TipDotSystem=tips;}
        foreach(var sound in menu.GetComponentsInChildren<WorldButtonSound>(true))sound.Post=host.Play;
        foreach(var sound in menu.GetComponentsInChildren<WorldPopupWindowSound>(true))sound.Post=host.Play;
        mount.SetActive(true);yield return null;
        foreach(var close in menu.GetComponentsInChildren<CloseWindowBtn>(true))close.Init(host);
        var dialog=menu.GetComponent<FunctionEntryDialog>();var popup=menu.GetComponent<WorldPopupWindow>();
        Require(dialog&&popup,"native menu retains dialog and popup components");
        dialog.Init();popup.Show();
        float deadline=Time.realtimeSinceStartup+3;while(popup.State!=WorldPopupWindow.EState.Show&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(popup.State==WorldPopupWindow.EState.Show,"official show clip completes");
        yield return null;Canvas.ForceUpdateCanvases();
        Require(host.OpenCount==1&&host.CloseCount==1,"dialog registers both original activity notifications");
        Require(!EntryField<GameObject>(dialog,"mActivityRecommondView").activeSelf,"empty activity records hide the recommendation area");
        Require(host.StateSubscribers==1,"predownload observes state updates exactly once");
        Require(!MenuField<Button>(menu.GetComponentInChildren<PredownloadEntry>(true),"mEntryBtn").gameObject.activeSelf,"downloaded local content hides predownload entry");
        Require(tips.Listeners.Count>0,"all active native menu tip selectors bind");
        Capture("home-function-menu-open.png");

        MenuClick(menu.GetComponentInChildren<FunctionEntryTaskBtn>(true),"Button");Require(host.Calls.Last()=="Task:null:null","task click preserves navigation arguments");
        MenuClick(menu.GetComponentInChildren<ShopEntryBtn>(true),"Btn");Require(host.Calls.Last()=="Init:Shop","shop initializes inside popup callback");
        MenuClick(menu.GetComponentInChildren<CollectBookBtn>(true),"Btn");Require(host.Calls.Last()=="Collect:None:null","native collection defaults and data retained");
        MenuClick(menu.GetComponentInChildren<AchievementGroupBtn>(true),"Btn");Require(host.Calls.Last()=="Achievement:2","achievement definitions evaluated in popup callback");
        MenuClick(menu.GetComponentInChildren<SettingsBtn>(true),"Btn");Require(host.Calls.Last()=="Settings:Normal","settings keeps Normal style");
        MenuClick(menu.GetComponentInChildren<FriendBtn>(true),"myBtn");Require(host.Calls.Last()=="Friend","friend popup has no invented Init call");
        MenuClick(menu.GetComponentInChildren<LocalRecordBtn>(true),"myBtn");Require(host.Calls.Last()=="Init:LocalRecord","record popup initialized");
        MenuClick(menu.GetComponentInChildren<TopListBtn>(true),"TopBtn");Require(host.Calls.Last()=="Top:TopPanel_CombatRating:Manual","ranking popup preserves reporting arguments");
        MenuClick(menu.GetComponentInChildren<AnnounceBtn>(true),"AnnounceClickBtn");Require(host.Calls.Last()=="Announce","announcement dispatcher");
        MenuClick(menu.GetComponentInChildren<UserBtn>(true),"Btn");Require(host.Calls.Last()=="User","user dialog dispatcher");
        MenuClick(menu.GetComponentInChildren<SocialBtn>(true),"mSocialClickBtn");Require(host.Calls.TakeLast(2).SequenceEqual(new[]{"RemoveNew:Social","Social"}),"social clears its new marker before popup");
        var planes=menu.GetComponentInChildren<PlanesMergeFunctionEntryBtn>(true);
        host.DisabledPlanes=true;MenuClick(planes,"Btn");Require(host.Calls.Last()=="Error:53883:False","disabled planes branch retains error ID");
        host.DisabledPlanes=false;MenuClick(planes,"Btn");Require(host.Calls.Last()=="Init:Planes","enabled planes branch initializes its panel");
        MenuClick(menu.GetComponentInChildren<HomeSkinBtn>(true),"Button");Require(host.Calls.Contains("Init:HomeSkin")&&host.WindowCloses==1,"decoration button also dispatches original parent-close component");
        MenuClick(menu.GetComponentInChildren<ScreenShotEntry>(true),"mEntryBtn");Require(host.Calls.Contains("Screenshot")&&host.WindowCloses==2,"screenshot also closes original parent window");
        Require(host.AudioPosts>3&&host.PlayingAudio,"verified original popup and button audio reaches the muted source");
        Require(host.LevelSubscribers==1&&host.RankSubscribers==1,"ranking owns exactly one pair of player observers");

        // A state notification exercises the original asynchronous visibility
        // and the two click branches without any external downloader/network.
        host.Downloaded=0;host.State=PredownloadState.Wait;host.NotifyDownload();yield return null;yield return null;Canvas.ForceUpdateCanvases();
        var download=menu.GetComponentInChildren<PredownloadEntry>(true);
        Require(MenuField<Button>(download,"mEntryBtn").gameObject.activeInHierarchy,"incomplete content makes original entry visible");
        MenuClick(download,"mEntryBtn");Require(host.Calls.TakeLast(2).SequenceEqual(new[]{"PredownloadTip","PredownloadClick:True"}),"waiting click opens tip before marking viewed");
        host.State=PredownloadState.Downloading;host.NotifyDownload();yield return null;yield return null;Canvas.ForceUpdateCanvases();
        MenuClick(download,"mEntryBtn");Require(host.Calls.TakeLast(2).SequenceEqual(new[]{"Init:PredownloadProgress","PredownloadClick:True"}),"active download opens and initializes progress before marking viewed");
        host.State=PredownloadState.BlockedByPayd;host.NotifyDownload();yield return null;yield return null;
        Require(!MenuField<Button>(download,"mEntryBtn").gameObject.activeSelf,"blocked publisher variant hides entry");
        host.State=PredownloadState.Downloaded;host.Downloaded=100;host.NotifyDownload();yield return null;yield return null;
        Capture("home-function-menu-after-clicks.png");
        host.ActuallyClose=true;var exit=menu.GetComponentsInChildren<CloseWindowBtn>(true).Single(x=>x.gameObject.name=="CloseBtn");
        Click(exit.myButton);deadline=Time.realtimeSinceStartup+3;while(menu.activeSelf&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(!menu.activeSelf&&popup.State==WorldPopupWindow.EState.Hide,"simulated close click completes original hide clip");
        Require(host.OpenCount==0&&host.CloseCount==0&&host.LevelSubscribers==0&&host.RankSubscribers==0&&tips.Listeners.Count==0,"disable releases activity, player and tip observers");
        Require(host.StateSubscribers==1,"predownload subscription intentionally lasts until destruction");
        Object.Destroy(mount);yield return null;
        Require(host.StateSubscribers==0&&host.DestroyedObservers==0,"destruction releases state and window observers");
        host.Dispose();
        Debug.Log("HOME_FUNCTION_MENU_CONTROLS_PASSED: native menu pointer clicks, original popup animation/audio, popup callback arguments, predownload state branches and lifecycle cleanup. Production feature hosts, recommendation population and formal HUD integration remain separate.");
    }

    void MenuClick(Component component,string field)
    {Require(component,"original component "+field);Click(MenuField<Button>(component,field));}
    static T MenuField<T>(object owner,string name)
    {var type=owner.GetType();while(type!=null){var field=type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(field!=null)return (T)field.GetValue(owner);type=type.BaseType;}throw new MissingFieldException(name);}
    sealed class MenuCondition:IHomeHudCondition { }
    sealed class MenuServices:IHomeFunctionMenuServices,IHomeMenuPlayer,IHomeHudConditionHandler,IHomeMenuActivityServices,
        IHomeMenuWikiServices,IHomeMenuWikiPlayer,IHomeMenuPredownloadServices,IHomeMenuTimeServices,IProfessionWindowService,IDisposable
    {
        public GameObject Menu;public bool DisabledPlanes,ActuallyClose;public int WindowCloses,AudioPosts;
        public readonly List<string> Calls=new List<string>();readonly MenuCondition condition=new MenuCondition();
        Action<int> level,rank;Action<object> open,close;Action<HomeMenuPredownloadStateUpdate> download;
        readonly Dictionary<Action,IDisposable> observers=new Dictionary<Action,IDisposable>();AudioSource source;
        public int LevelSubscribers=>level?.GetInvocationList().Length??0;public int RankSubscribers=>rank?.GetInvocationList().Length??0;
        public int OpenCount=>open?.GetInvocationList().Length??0;public int CloseCount=>close?.GetInvocationList().Length??0;
        public int StateSubscribers=>download?.GetInvocationList().Length??0;public int DestroyedObservers=>observers.Count;
        public bool PlayingAudio=>source&&source.isPlaying;
        public IHomeMenuPlayer Player=>this;IHomeMenuWikiPlayer IHomeMenuWikiServices.Player=>this;
        public IHomeHudConditionHandler ConditionHandler=>this;
        public bool IsFunctionLimit(FunctionType type)=>false;public bool IsHomeScene=>true;
        public event Action<int> OnLevelUpdate{add{level+=value;}remove{level-=value;}}
        public event Action<int> OnSubRankUpdate{add{rank+=value;}remove{rank-=value;}}
        public HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type)=>new HomeMenuFunctionLimitInfo{Condition=condition};
        public bool IsMet(IHomeHudCondition value)=>true;
        public void Register(IHomeHudCondition value,Action refresh){throw new Exception("unlocked menu must not register redundant conditions");}
        public void Deregister(IHomeHudCondition value,Action refresh){throw new Exception("unregistered condition deregistered");}
        public IEnumerable<object> GetAllAchievementGroups(){Calls.Add("AchievementDefinitions");return new object[]{1,2};}
        public void ReportClick(string panel,string eventId){Calls.Add("Report:"+panel+":"+eventId);}
        void Popup(string name,Action<IHomeMenuPanel> initialize){Calls.Add("Popup:"+name);initialize(new Panel(()=>Calls.Add("Init:"+name)));}
        public void PopupAchievementGroups(IWindowOperator owner,Action<IHomeMenuAchievementPanel> initialize){Require(owner!=null,"achievement owner");Calls.Add("Popup:Achievement");initialize(new Achievement(this));}
        public void PopupCollectBook(IWindowOperator owner,Action<IHomeMenuCollectBookPanel> initialize){Require(owner!=null,"collection owner");initialize(new Collect(this));}
        public void PopupSettings(IWindowOperator owner,Action<IHomeMenuSettingsDialog> initialize){Require(owner!=null,"settings owner");initialize(new Settings(this));}
        public void PopupHomeSkin(Action<IHomeMenuPanel> initialize)=>Popup("HomeSkin",initialize);
        public void PopupLocalFightRecord(Action<IHomeMenuPanel> initialize)=>Popup("LocalRecord",initialize);
        public void PopupShopActivityEntry(Action<IHomeMenuPanel> initialize)=>Popup("Shop",initialize);
        public void PopupPlanesMerge(Action<IHomeMenuPanel> initialize)=>Popup("Planes",initialize);
        public void PopupFriend(IWindowOperator owner){Require(owner!=null,"friend owner");Calls.Add("Friend");}
        public void PopupUser()=>Calls.Add("User");public void PopupPersonalTop(string id,string from)=>Calls.Add("Top:"+id+":"+from);
        public void NavigateTaskPanel(object value,object completed){Require(value==null&&completed==null,"task null arguments");Calls.Add("Task:null:null");}
        public void OpenScreenShot()=>Calls.Add("Screenshot");public void PopAnnounce()=>Calls.Add("Announce");public void PopSocial()=>Calls.Add("Social");
        public void RemoveNewFunction(FunctionType type)=>Calls.Add("RemoveNew:"+type);
        public bool CheckFunctionDisabled(string type){Require(type=="PlanesIconEdit","original disabled function type");return DisabledPlanes;}
        public void ShowError(int id,object args,bool translate){Require(args==null,"error arguments");Calls.Add("Error:"+id+":"+translate);}
        public IEnumerable<IHomeMenuActivityEntryData> ActivityEntryDatas=>Array.Empty<IHomeMenuActivityEntryData>();
        public event Action<object> OnActivityOpen{add{open+=value;}remove{open-=value;}}
        public event Action<object> OnActivityClose{add{close+=value;}remove{close-=value;}}
        public bool ActivityDataManagerExists=>true;public bool IsMapScene=>false;
        public HomeMenuActivityRecommendInfo GetRecommend(ActivityEntryType type,int id)=>throw new Exception("empty records must not query recommendation config");
        public IReadOnlyDictionary<int,HomeMenuActivityEntryInfo> GetEntries(ActivityEntryType type)=>throw new Exception("no fixture activity prefab requested");
        public int RandomRange(int min,int max)=>throw new Exception("empty records must not choose an index");
        public IHomeMenuPrefabHandle LoadPrefab(string path)=>throw new Exception("no fixture activity prefab requested");
        public GameObject Instantiate(GameObject prefab,Transform parent)=>Object.Instantiate(prefab,parent,false);
        public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 point){var results=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},results);return results;}
        public void ReportRecommendationClick(string panel,string id,Dictionary<string,string> extend)=>throw new Exception("no recommendation click expected");
        public bool CheckWikiDisabled()=>true;public HomeMenuWikiInfo GetWikiInfo(string type)=>throw new Exception("disabled Wiki must short circuit");
        public string GetUrl(string type)=>throw new Exception("disabled Wiki");public string GetTitleKey(string type)=>throw new Exception("disabled Wiki");
        public IHomeHudCondition GetCondition(int id)=>condition;public string GetText(string key)=>key;
        public void SystemMessage(string text)=>Calls.Add("System:"+text);public void TrySystemMessage(string text)=>Calls.Add("System:"+text);
        public void PublishNewTipClick()=>Calls.Add("WikiTip");
        public UniTask<string> OpenHtml(string url,string style,string orientation,string close,object parameter)=>throw new Exception("fixture never opens an external browser");
        public Dictionary<string,object> CreateEventData(string name,string id,string account)=>new Dictionary<string,object>();
        public void ReportEvent(string name,Dictionary<string,object> values)=>Calls.Add("Event:"+name);
        public string GetShuShuAccountId()=>"fixture";public string Account=>"fixture";public long ServerId=>1;public long Id=>1;public string Name=>"fixture";public int Level=>1;
        public PredownloadState State=PredownloadState.Downloaded;public long Downloaded=100;
        public PredownloadState CurrentDownloadState=>State;public long TotalDownloadedBytes=>100;public long CurrentDownloadedBytes=>Downloaded;public bool TimeSynced=>true;
        public IDisposable SubscribeStateUpdates(Action<HomeMenuPredownloadStateUpdate> value){download+=value;return new Subscription(()=>download-=value);}
        public void NotifyDownload()=>download?.Invoke(default);
        public void PopupTipDialog()=>Calls.Add("PredownloadTip");public void PopupProgressDialog(Action<IHomeMenuPanel> initialize)=>Popup("PredownloadProgress",initialize);
        public void ClickEntry(bool value)=>Calls.Add("PredownloadClick:"+value);
        public IReadOnlyCollection<int> DisplayServerTimeRegionList=>Array.Empty<int>();public int RegionCode=>0;public int TimeZone=>8;
        public DateTime GetDateTime()=>new DateTime(2026,1,1);public DateTime GetClientLocalDateTime()=>GetDateTime();public DateTime GetDateTimeWithTimeZone(DateTime value)=>value.AddHours(8);
        public string GetServerTimeWithZoneTip(int timeZone)=>"ServerTime";
        public void CloseLast()=>Close(Menu);
        public void Close(GameObject window){Require(window==Menu,"original parent window resolved");WindowCloses++;if(ActuallyClose)window.GetComponent<WorldPopupWindow>().Hide(null);}
        public void ObserveDestroyed(WorldPopupWindow window,Action callback){observers.Add(callback,window.OnWindowDestory.Subscribe(callback));}
        public void StopObservingDestroyed(WorldPopupWindow window,Action callback){Require(observers.TryGetValue(callback,out var subscription),"window observer registered");subscription.Dispose();observers.Remove(callback);}
        public void Play(AudioClip clip,GameObject emitter){Require(clip&&emitter,"original audio reference and emitter");if(!source){source=Menu.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=0;}AudioPosts++;source.PlayOneShot(clip);}
        public void Dispose(){if(source)Object.Destroy(source);}
        sealed class Subscription:IDisposable{Action dispose;public Subscription(Action value){dispose=value;}public void Dispose(){var action=dispose;dispose=null;action?.Invoke();}}
        sealed class Panel:IHomeMenuPanel{readonly Action initialize;public Panel(Action value){initialize=value;}public void Init()=>initialize();}
        sealed class Achievement:IHomeMenuAchievementPanel{readonly MenuServices host;public Achievement(MenuServices value){host=value;}public void Init(List<object> values){Require(host.Calls.Last()=="AchievementDefinitions","definitions evaluated inside callback");host.Calls.Add("Achievement:"+values.Count);}}
        sealed class Collect:IHomeMenuCollectBookPanel{readonly MenuServices host;public Collect(MenuServices value){host=value;}public void Init(CollectionType type,CollectBookDataBase data)=>host.Calls.Add("Collect:"+type+":"+(data==null?"null":"data"));}
        sealed class Settings:IHomeMenuSettingsDialog{readonly MenuServices host;public Settings(MenuServices value){host=value;}public void Init(HomeMenuSettingStyle style)=>host.Calls.Add("Settings:"+style);}
    }
    sealed class MenuTips:IProfessionTipDotSystem,IProfessionTipDotSystemController
    {
        public readonly Dictionary<Action<TipDotStateInfo>,TipDotNodeKey> Listeners=new Dictionary<Action<TipDotStateInfo>,TipDotNodeKey>();
        public bool IsShowTipDot=>true;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>TipDotGraphType.Static;
        public bool LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> changed){Require(key.IsValid(),"native menu selector address");Listeners.Add(changed,key);changed(default);return true;}
        public bool UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> changed){Require(Listeners[changed]==key,"same original selector on unlink");return Listeners.Remove(changed);}
    }
}
