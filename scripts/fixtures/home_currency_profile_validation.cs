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
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeAutoIncrease;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeCurrencyProfileValidation
{
    const string Key="Zjcs.HomeCurrencyProfile.Active";
    static HomeCurrencyProfileValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("RealProfileHomeCurrencies").AddComponent<HomeCurrencyProfileRunner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Home currency profile validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error!=null) Debug.LogException(error);
        else Debug.Log("HOME_CURRENCY_PROFILE_PASSED: new-character grant, actual profile transactions and save restoration, official auto-increase rules, original currency coroutine, native pointer input and rendered values. Full production HUD and detail windows remain separate.");
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomeCurrencyProfileRunner:MonoBehaviour,IPlayerItemUIHost
{
    OfflineMainlineProgressSession profile;
    ProfileSlotCatalog slots;
    string slot;
    DateTime now;
    OfflineHomeCurrencyPlayer player;
    OfflineHomeTopBar view;
    OfficialHomeTopBarAssetCatalog assets;
    FontAssetFontService fonts;
    Camera uiCamera;
    RenderTexture target;
    EventSystem events;
    Vector2 pointer;
    int descriptionItem,errors,receipt;
    string Work=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
    void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.logMessageReceived+=OnLog;
        var steps=Exercise();
        for(;;){bool next;object current=null;try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){HomeCurrencyProfileValidation.Finish(error);yield break;}
            if(!next)break;yield return current;}
        HomeCurrencyProfileValidation.Finish(null);
    }
    IEnumerator Exercise()
    {
        VerifyOriginalRules();
        string saveRoot=Path.Combine(Work,"currency-profile");
        var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);
        slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="体力验证";
        DateTime beforeCreation=DateTime.UtcNow;
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        slots=new ProfileSlotCatalog(saveRoot);profile=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        var stamina=Stamina();Require(stamina.Count==300 && stamina.AutoIncrease!=null,"new game initializes the confirmed 300-point grant");
        Require(stamina.AutoIncrease.LastUpdateTimeTicks>=beforeCreation.Ticks && stamina.AutoIncrease.LastUpdateTimeTicks<=DateTime.UtcNow.Ticks,"original OnAddParam records creation time");
        Require(stamina.AutoIncrease.IncreaseSpeed==5 && stamina.AutoIncrease.MaxCount==300 && stamina.AutoIncrease.ItemCountLimit==1200,"selected original natural/hard limits are distinct");
        Require(!creation.TryCreateCharacter(slot,draft,out error),"creation cannot issue the initial grant twice");
        now=DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        Require(profile.ApplyCharacterMutation(p=>p.Inventory.Single(x=>x.ItemId==57).AutoIncrease.LastUpdateTimeTicks=now.Ticks,"fixture-clock").IsApplied,"isolate deterministic clock");
        player=new OfflineHomeCurrencyPlayer(profile,()=>now);
        Transact(p=>Require(OfflineInventoryOperations.Consume(p,57,300,Seconds()).IsApplied,"consume initial grant"));
        Require(Stamina().Count==0 && profile.Save.Player.Inventory.Count(x=>x.ItemId==57)==1,"zero stamina retains its original auto-increase record");
        Reopen();now=now.AddMinutes(11).AddSeconds(59);Require(player.GetItemAmount(57)==0,"no early recovery");
        // Mono TimeSpan.TotalHours * 5 can be just below 1 at exactly 720s.
        // Keep the official double/floor operation and observe the next refresh.
        now=now.AddSeconds(2);Require(player.GetItemAmount(57)==1,"first point on the next refresh after 12 minutes");
        now=now.AddMinutes(5).AddSeconds(59);
        Transact(p=>Require(OfflineInventoryOperations.Consume(p,57,1,Seconds()).IsApplied,"consume recovered point"));
        Require(Stamina().Count==0 && Math.Abs(Stamina().AutoIncrease.Decimal-.5)<1e-10,"spending preserves fractional recovery");
        Reopen();now=now.AddMinutes(6);Require(player.GetItemAmount(57)==1,"reloaded fraction combines with offline time");
        Transact(p=>Require(OfflineInventoryOperations.Grant(p,57,2000,Seconds()).IsApplied,"grant stamina above natural cap"));
        Require(player.GetItemAmount(57)==1200,"official hard count limit clamps grants separately");
        now=now.AddDays(1);Require(player.GetItemAmount(57)==1200,"above-natural-cap quantity survives idle time");
        Transact(p=>Require(OfflineInventoryOperations.Consume(p,57,1000,Seconds()).IsApplied,"spend from overflow"));
        Require(player.GetItemAmount(57)==200 && Stamina().AutoIncrease.Decimal==0,"full-cap idle time does not bank a fraction");
        Transact(p=>{
            Require(OfflineInventoryOperations.Grant(p,1,1234,Seconds()).IsApplied,"gold grant");
            Require(OfflineInventoryOperations.Grant(p,2,567,Seconds()).IsApplied,"diamond grant");});
        assets=OfficialHomeTopBarAssetCatalog.LoadDefault();
        uiCamera=new GameObject("CurrencyCamera",typeof(Camera)).GetComponent<Camera>();
        uiCamera.clearFlags=CameraClearFlags.SolidColor;uiCamera.backgroundColor=new Color(.12f,.2f,.28f);uiCamera.orthographic=true;
        // WorldSafeArea reads native Screen, so the target must use those same
        // dimensions. Portrait Windows Player adaptation is verified separately.
        target=new RenderTexture(Screen.width,Screen.height,24);uiCamera.targetTexture=target;
        var root=new GameObject("CurrencyCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        events=new GameObject("InternalPointer",typeof(EventSystem)).GetComponent<EventSystem>();
        var textAssets=OfficialHomePlayerInfoAssetCatalog.LoadDefault();fonts=textAssets.CreateFontService();fonts.Initialize();
        view=new OfflineHomeTopBar(canvas.transform,this,textAssets.NamePrefab.GetComponent<TMP_Text>().font,key=>key,assets);
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        foreach(var item in Items())
        {
            var corners=new Vector3[4];((RectTransform)item.transform).GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                var point=uiCamera.WorldToViewportPoint(corner);
                Require(point.x>=-.001f && point.x<=1.001f && point.y>=.9f && point.y<=1.001f,
                    "each native currency rectangle is fully visible at the top of the actual screen");
            }
        }
        Require(Items().Select(Text).SequenceEqual(new[]{"1234","567","200/300"}),"native widgets display committed profile values");
        Require(Subscriptions("OnItemCountUpdate")==3 && Subscriptions("OnAutoIncreaseItemDataUpdate")==1,"three count observers and the selected stamina addition observer");
        string savedBefore=File.ReadAllText(slots.GetProgressPath(slot));
        var rejected=profile.ApplyInventoryTransaction("currency-overdraw",save=>{
            if(!OfflineInventoryOperations.Consume(save.Player,57,9999,Seconds()).IsApplied)
                throw new InvalidOperationException("Insufficient stamina.");});
        Require(!rejected.IsApplied && savedBefore==File.ReadAllText(slots.GetProgressPath(slot)),"rejected spending does not advance the saved recovery checkpoint");
        Capture("home-currency-profile.png");
        foreach(var item in Items()) {Click(Field<Button>(item,"mBtn"));Require(descriptionItem==Id(item),"native unobstructed pointer dispatch");}
        Transact(p=>Require(OfflineInventoryOperations.Grant(p,1,10,Seconds()).IsApplied,"actual gold transaction"));
        Require(Text(Items()[0])=="1244","original provider receives committed item updates");
        view.Handler.SetStopUpdateCount(true);
        Transact(p=>Require(OfflineInventoryOperations.Grant(p,1,20,Seconds()).IsApplied,"gold during pause"));
        Require(Text(Items()[0])=="1244","original stop flag defers visible count");
        view.Handler.SetStopUpdateCount(false);Require(Text(Items()[0])=="1264","resume reads actual latest profile");
        now=now.AddMinutes(12);yield return new WaitForSeconds(1.2f);
        Require(Text(Items()[2])=="201/300","original one-second coroutine reads natural recovery without a transaction");
        var identities=Items().Select(x=>x.GetInstanceID()).ToArray();
        var previousProvider=Field<object>(Items()[2],"mCountProvider");
        view.Root.SetActive(false);now=now.AddMinutes(12);yield return new WaitForSeconds(1.2f);
        Require(Subscriptions("OnItemCountUpdate")==0 && Subscriptions("OnAutoIncreaseItemDataUpdate")==0
            && Field<object>(previousProvider,"mCancellationTokenSource")==null,"original release unsubscribes and cancels the recovery coroutine");
        view.Root.SetActive(true);yield return null;
        Require(Text(Items()[2])=="202/300" && Items().Select(x=>x.GetInstanceID()).SequenceEqual(identities),"release/cancel and original pooled rebind");
        Require(Subscriptions("OnItemCountUpdate")==3 && Subscriptions("OnAutoIncreaseItemDataUpdate")==1,"reborrow restores exactly one set of observers");
        Transact(p=>Require(OfflineInventoryOperations.Grant(p,57,200,Seconds()).IsApplied,"overflow presentation"));
        Require(Text(Items()[2])=="402/300","native limit formatting preserves overflow");
        Capture("home-currency-overflow.png");
        view.Dispose();view=null;yield return null;
        Require(Subscriptions("OnItemCountUpdate")==0 && Subscriptions("OnAutoIncreaseItemDataUpdate")==0,"view disposal releases all profile listeners");
        player.Dispose();player=null;fonts.Dispose();fonts=null;
        Require(errors==0,"no Unity runtime errors");
    }
    static void VerifyOriginalRules()
    {
        var ordinary=JsonUtility.FromJson<OfflineItemStack>(JsonUtility.ToJson(new OfflineItemStack{ItemId=1,Count=10}));
        Require(ordinary.AutoIncrease==null && ordinary.Count==10,"ordinary item round trip retains an absent auto-increase parameter");
        var start=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);var time=start;
        var p=new ItemParamAutoIncrease{LastUpdateTime=start,IncreaseSpeed=5,MaxCount=300,Decimal=.25,BonusSeconds=180};
        var item=new HomeAutoIncreaseItem(10,p,1200,()=>time){LogException=error=>throw error};
        time=start.AddMinutes(6);Require(item.Count==11,"original bonus seconds plus decimal floor");
        bool notified=false;
        Action<HomeAutoIncreaseItem,long,long,DateTime,double> onCount=(source,old,next,oldTime,bonus)=>{
            Require(source.StoredCount==10 && old==10 && next==7 && oldTime==start && bonus==180,"original callback precedes stored-count assignment");
            Require(p.LastUpdateTime==time && p.BonusSeconds==0,"time/bonus update precedes callback");notified=true;};
        item.OnAutoIncreaseCountUpdate+=onCount;
        item.SetCount(7);Require(notified && item.StoredCount==7 && p.Decimal==0,"original set sequence");
        item.OnAutoIncreaseCountUpdate-=onCount;
        time=start.AddMinutes(-5);Require(item.Count==7,"clock rewind contributes zero elapsed hours");
        p.IncreaseSpeed=-2;p.LastUpdateTime=start;time=start.AddHours(1);Require(item.Count==5,"negative-speed branch preserved");
        p.IncreaseSpeed=0;p.Decimal=.6;item.SetCount(4);Require(Math.Abs(p.Decimal-.6)<1e-10,"zero-speed branch retains decimal");
        var segmented=new ItemParamAutoIncrease{Decimal=.5,BonusSeconds=360};
        var rows=new List<SegmentedIncreaseData>{new SegmentedIncreaseData{StartTime=start,EndTime=start.AddHours(1),IncreaseSpeed=10,MaxCount=300},new SegmentedIncreaseData{StartTime=start.AddHours(1),EndTime=start.AddHours(2),IncreaseSpeed=5,MaxCount=300}};
        Require(Math.Abs(AutoIncreaseItemUtils.GetActualCountWithAutoIncreaseSegmented(segmented,20,rows)-36.5)<1e-10,"original ordered segmented rates and first-segment bonus");
    }
    OfflineItemStack Stamina()=>profile.Save.Player.Inventory.Single(x=>x.ItemId==57);
    long Seconds()=>new DateTimeOffset(now).ToUnixTimeSeconds();
    void Transact(Action<OfflinePlayerProgress> mutation)
    {
        Require(profile.ApplyInventoryTransaction("currency-fixture:"+(receipt++),save=>mutation(save.Player)).IsApplied,"profile inventory transaction commits");
        player?.SynchronizeCommittedPlayer();
    }
    void Reopen(){player?.Dispose();profile=OfflineMainlineProgressSession.OpenCharacter(slots,slot);player=new OfflineHomeCurrencyPlayer(profile,()=>now);}
    MePlayerItemUI[] Items()=>view.Root.GetComponentsInChildren<MePlayerItemUI>();
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    static string Text(MePlayerItemUI item)=>System.Text.RegularExpressions.Regex.Replace(Field<Component>(item,"mCountText").GetComponent<TMP_Text>().text,"<[^>]*>",string.Empty);
    int Id(MePlayerItemUI item)=>assets.currencies.Single(x=>x.icon==Field<Image>(item,"mIcon").sprite).itemId;
    int Subscriptions(string name)=>((Delegate)typeof(OfflineHomeCurrencyPlayer).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player))?.GetInvocationList().Length??0;
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();pointer=RectTransformUtility.WorldToScreenPoint(uiCamera,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var data=new PointerEventData(events){position=pointer,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"actual first raycast target");
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();uiCamera.Render();var old=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        Require(image.GetPixels32().Count(x=>x.r>100 && x.g>100 && x.b>80)>100,"native top bar draws actual pixels");
        File.WriteAllBytes(Path.Combine(Work,name),image.EncodeToPNG());RenderTexture.active=old;Object.Destroy(image);
    }
    public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type,PlayerItemUIItemIdProvider id)=>player.CreateCountProvider(type,id);
    public IPlayerItemUIHandler CreateHandler(PlayerItemType type,PlayerItemUIItemIdProvider id)=>new Handler(this,id.ItemId);
    public IDisposable LoadIcon(int id,Action<Sprite> loaded){loaded(assets.Icon(id));return new Handle();}
    public Vector2 InputPosition=>pointer;
    public object Compact(long value)=>value.ToString(CultureInfo.InvariantCulture);
    public string ColorFormat(string value,Color color)=>"<color=#"+ColorUtility.ToHtmlStringRGBA(color)+">"+value+"</color>";
    sealed class Handle:IDisposable{public void Dispose(){}}
    sealed class Handler:IPlayerItemUIHandler,IPlayerItemUIWithLimit
    {
        readonly HomeCurrencyProfileRunner owner;public int ItemId{get;set;}public bool ShowAddBtn=>false;
        public Handler(HomeCurrencyProfileRunner owner,int id){this.owner=owner;ItemId=id;}
        public void HandleClick(Vector2 point){owner.descriptionItem=ItemId;}
        public void HandleAddBtn(){throw new InvalidOperationException("Fixture does not open purchase windows.");}
        public bool TryGetMaxCountLimit(out long max){max=ItemId==57?owner.Stamina().AutoIncrease.MaxCount:0;return ItemId==57;}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
