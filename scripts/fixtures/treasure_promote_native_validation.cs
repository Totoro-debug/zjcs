using System;
using System.Collections;
using System.Collections.Generic;
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
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.TreasureUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasurePromoteUI;
using Zjcs.Official.TreasureSlotsUI;
using S=Zjcs.Official.TreasureSlots;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Presentation.Home;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TreasurePromoteNativeValidation
{
    private const string Active="Zjcs.TreasurePromoteNative.Active";
    private static int checks,errors;
    private static double heartbeat;
    private static EditorWindow[] gameViews;
    private static HomeFeatureRuntime home;
    private static FontAssetFontService fonts;
    private static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_PROMOTE_OUTPUT");
    private sealed class Runner:MonoBehaviour
    {
        private IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0){var steps=stack.Peek();bool next;try{next=steps.MoveNext();}catch(Exception error){Finish(error);yield break;}
                if(!next){stack.Pop();continue;}if(steps.Current is IEnumerator nested)stack.Push(nested);else yield return steps.Current;}
            Finish(errors==0?null:new Exception("Unexpected Unity errors: "+errors));
        }
    }
    static TreasurePromoteNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=Log;
            gameViews=Resources.FindObjectsOfTypeAll(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Cast<EditorWindow>().ToArray();
            var runner=new GameObject("Treasure detail runtime verification");Object.DontDestroyOnLoad(runner);runner.AddComponent<Runner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Active,false)&&EditorApplication.isPlaying){
                EditorApplication.QueuePlayerLoopUpdate();
                foreach(var view in gameViews??Array.Empty<EditorWindow>())if(view)view.Repaint();
                if(EditorApplication.timeSinceStartup>heartbeat){heartbeat=EditorApplication.timeSinceStartup+5;
                    var runners=Object.FindObjectsOfType<Runner>(true);
                    File.WriteAllText(Path.Combine(Work,"heartbeat.txt"),"frame="+Time.frameCount+" time="+Time.time+" paused="+EditorApplication.isPaused+" background="+Application.runInBackground+" compiling="+EditorApplication.isCompiling+" updating="+EditorApplication.isUpdating+" runners="+string.Join(",",runners.Select(r=>r.isActiveAndEnabled+"/"+r.gameObject.activeInHierarchy)));}}
            if(SessionState.GetBool(Active,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Complete original treasure detail verification timed out."));
        };
    }
    public static void Run()
    {
        string project=Environment.GetEnvironmentVariable("ZJCS_TREASURE_PROMOTE_PROJECT");
        if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))
            throw new InvalidOperationException("Use the explicit independent source project.");
        PlayerSettings.runInBackground=true;
        UnityEditorInternal.ProfilerDriver.enabled=false;
        DockGameViewWithoutFocus();
        typeof(Zjcs.Editor.Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{675,1463});
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)EditorApplication.timeSinceStartup+900);EditorApplication.EnterPlaymode();
    }
    private static void DockGameViewWithoutFocus()
    {
        var assembly=typeof(EditorWindow).Assembly;var type=assembly.GetType("UnityEditor.GameView",true);
        var parent=typeof(EditorWindow).GetField("m_Parent",BindingFlags.Instance|BindingFlags.NonPublic);
        var hierarchy=Resources.FindObjectsOfTypeAll(assembly.GetType("UnityEditor.SceneHierarchyWindow",true)).Cast<EditorWindow>()
            .OrderByDescending(window=>window.position.width*window.position.height).First();
        var dock=parent.GetValue(hierarchy);
        if(dock.GetType()!=assembly.GetType("UnityEditor.DockArea",true))throw new InvalidOperationException("The isolated validation requires the main hierarchy dock.");
        var existing=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().ToArray();
        var game=existing.FirstOrDefault(window=>parent.GetValue(window)==dock);
        if(!game){game=(EditorWindow)ScriptableObject.CreateInstance(type);game.titleContent=new GUIContent("Game");
            dock.GetType().GetMethod("AddTab",new[]{typeof(EditorWindow),typeof(bool)}).Invoke(dock,new object[]{game,false});}
        foreach(var old in existing)if(old!=game)old.Close();
        var behavior=assembly.GetType("UnityEditor.PlayModeView",true).GetProperty("enterPlayModeBehavior");
        behavior.SetValue(game,Enum.Parse(behavior.PropertyType,"PlayUnfocused"));
    }
    private static void Require(bool value,string detail){checks++;if(!value)throw new InvalidOperationException(detail);}
    private static void Log(string text,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||text.Contains("was not found in the ["))errors++;}
    private static T Field<T>(object instance,string name)
    {
        for(Type type=instance.GetType();type!=null;type=type.BaseType)
        {var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null)return (T)field.GetValue(instance);}
        throw new MissingFieldException(instance.GetType().FullName,name);
    }
    private sealed class Pointer:IHomeSceneClickPointer
    {public bool LeftDown=>false;public bool LeftUp=>false;public Vector2 Position=>Vector2.zero;public int Width=>Screen.width;public int Height=>Screen.height;}
    private sealed class Share:ITreasureItemShareServices
    {
        public bool CanShare=>false;public bool IsLocalItem(IProfessionItemData data)=>data!=null;
        public void ShareItem(IProfessionItemData data,int type)=>throw new InvalidOperationException("Confirmed offline mode has no real-player chat.");
    }
    private sealed class ItemContext:IProfessionItemIconContext
    {
        public IProfessionItemIconContext Common;
        public OfflineTreasureDetailConfiguration Data;
        public IProfessionItemPlayer Player {get;set;}
        public ItemIconConfig Config=>Common.Config;public Object GrayConfig=>Common.GrayConfig;public IProfessionItemColors Colors=>Common.Colors;
        public IProfessionItemInfo GetItemInfo(int id)=>Data.GetItem(id);
        public object Compact(long count)=>Common.Compact(count);public object CultureText(string key)=>Data.TextValue(key);
        public string GetItemNameKey(int id)=>"item_"+id+"_name";
        public bool TryGetSourceData(int id,out IReadOnlyList<IProfessionItemSource> rows){rows=null;return false;}
        public bool CanMerge(int id,bool recursion)=>false;public object GetLocationInfos(int id)=>null;
        public void SetUIGray(Object config,GameObject target,bool gray)=>Common.SetUIGray(config,target,gray);
        public void SetSoundSwitch(ProfessionItemSoundSwitch value,GameObject emitter)=>Common.SetSoundSwitch(value,emitter);
        public GameObject Instantiate(GameObject prefab,Transform parent,EUIInstantiateFlag flag=EUIInstantiateFlag.All)=>Common.Instantiate(prefab,parent,flag);
        public void InstantiateAsync(string path,Transform parent,Action<GameObject> complete)=>throw new InvalidOperationException("No asynchronous material icon belongs to the page selector.");
        public void PopupInfoDialog(IProfessionItemData data,IWindowOperator owner,long required,Action<GameObject> callback)=>throw new InvalidOperationException("The selection overlay must consume the icon click.");
        public void PopupInfoDialog(IProfessionItemInfo data,IWindowOperator owner,long required,Action<GameObject> callback)=>throw new InvalidOperationException("The selection overlay must consume the icon click.");
    }
    private static void Click(Button button)
    {
        Require(button&&button.gameObject.activeInHierarchy&&button.interactable,"Original detail button is active: "+(button?button.name:"null"));
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
        var point=RectTransformUtility.WorldToScreenPoint(canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,
            "Actual full-window button wins raycasts: "+button.name+" at "+point+", hits="+string.Join(",",hits.Select(hit=>hit.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Ready(OfflineTreasurePromoteWindowView view)
    {
        float end=Time.realtimeSinceStartup+30;
        while((!view.Window||view.Window.State!=WorldPopupWindow.EState.Show)&&Time.realtimeSinceStartup<end)yield return null;
        Require(view.Panel&&view.Window.State==WorldPopupWindow.EState.Show,"Original show animation and shared native popup preparation complete.");
        yield return null;
    }
    private static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
        Require(texture.GetRawTextureData().Where((v,i)=>i%4<3).Distinct().Count()>32,"Actual full original window renders with the source main city.");
        File.WriteAllBytes(Path.Combine(Work,name+".png"),texture.EncodeToPNG());Object.Destroy(texture);
    }
    private static IEnumerator Close(OfflineTreasurePromoteWindowView view,OfflineHomeCurrencyWindows windows)
    {
        view.Close();float end=Time.realtimeSinceStartup+15;
        while(view.Content&&Time.realtimeSinceStartup<end)yield return null;
        Require(!view.Content&&windows.WindowCount==0,"Original hide animation closes content and blur, releasing the window owner.");
        Object.Destroy(view);yield return null;
    }
    private static IEnumerator Steps()
    {
        float deadline=Time.realtimeSinceStartup+15;while((Screen.width!=675||Screen.height!=1463)&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(Screen.width==675&&Screen.height==1463,"Actual portrait surface in independent source project.");
        var assets=OfflineTreasurePromoteAssets.LoadDefault();var detail=OfflineTreasureDetailAssets.LoadDefault();
        var config=new OfflineTreasureDetailConfiguration(detail.Configuration);var tables=OfflineTreasurePropertyCatalog.LoadDefault();var rules=OfflineTreasureCatalog.Load();
        var slotRules=OfflineTreasureSlotCatalog.LoadDefault();var levelRules=OfflineTreasureSlotLevelCatalog.LoadDefault();
        var sourceLevels=JObject.Parse(Resources.Load<TextAsset>("Treasure/treasure-slot-levels").text);
        var limit=sourceLevels["Limits"].First(row=>(int)row["TreasureLevel"]>=20&&(int)row["PlayerBlessLevel"]==0);
        int ordinary=tables.Treasures.First(row=>row.Effect is TreasureEffectNone&&config.GetItem(row.ClassId).Quality==5&&!rules.GetTreasure(row.ClassId).LimitUseCommonMaterial).ClassId;
        int task=tables.Treasures.First(row=>row.Effect is TreasureEffectTask).ClassId;
        int peer=tables.Treasures.First(row=>row.ClassId!=ordinary&&row.ClassId!=task&&config.GetItem(row.ClassId).ElementType==config.GetItem(ordinary).ElementType).ClassId;
        string saveRoot=Path.Combine(Work,"isolated-saves");var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="古遗物培养";
        Require(creation.TryCreateCharacter(slot,draft,out string error)&&creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated actual role: "+error);
        var profiles=new ProfileSlotCatalog(saveRoot);var profile=OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        Require(profile.ApplyInventoryTransaction("promote-seed",next=>{
            next.Player.SubRank=(int)limit["SubRank"];
            next.Player.CharacterProperties.TreasurePlaneRank=(int)S.EnumExt.ToRank((S.SubRank)next.Player.SubRank);
            foreach(int id in new[]{ordinary,task,peer}){
                next.Player.Inventory.Add(new OfflineItemStack{ItemId=id,Count=1});
                next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=id,Active=true,Star=id==peer?rules.GetTreasure(id).StarMax:2,HitCount=id==task?17:0});
            }
            var budget=new Dictionary<int,long>();
            void Add(int id,long count){if(id>0){budget.TryGetValue(id,out long prior);budget[id]=prior+count;}}
            var costs=new OfflineTreasureCostRules(rules,id=>config.GetItem(id).Quality);
            foreach(int id in new[]{ordinary,task,peer}){
                foreach(var fee in costs.GetUpgradeMaterials(id,0,rules.GetTreasure(id).StarMax))Add(fee.Key,fee.Value);
                Add(rules.GetTreasure(id).TreasurePieceId,16);
            }
            foreach(var key in ((JObject)sourceLevels["LevelCosts"].First()["Materials"]).Properties())Add(int.Parse(key.Name),(long)key.Value);
            foreach(var fee in budget){var existing=next.Player.Inventory.FirstOrDefault(row=>row.ItemId==fee.Key);if(existing==null)next.Player.Inventory.Add(new OfflineItemStack{ItemId=fee.Key,Count=fee.Value});else existing.Count=fee.Value;}
        }).IsApplied,"Seed source costs, active ordinary/task relics and surplus fragments in isolated durable save.");
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();
        File.WriteAllText(Path.Combine(Work,"step.txt"),"opening-home "+Time.realtimeSinceStartup);
        home=HomeFeatureRuntime.BeginWithNativePointer(slot,saveRoot,null,new Pointer());
        // This original-window fixture retains the native rendered city and animation components;
        // its own real role, player services and popup owner drive the complete cultivation flow.
        home.enabled=false;
        File.WriteAllText(Path.Combine(Work,"step.txt"),"home-open "+Time.realtimeSinceStartup);
        home.TutorialAutoStartEnabled=false;
        for(int i=0;i<12;i++){File.WriteAllText(Path.Combine(Work,"step.txt"),"home-frame "+i+" frame="+Time.frameCount);yield return null;}profile=home.Profile;
        File.WriteAllText(Path.Combine(Work,"step.txt"),"home-twelve-frames "+Time.realtimeSinceStartup);
        // Controlled test world uses an actual original limit row; production supplies its common growth/world owner.
        Func<OfflinePlayerProgress,TreasureSlotLevelEnvironment> environment=p=>new TreasureSlotLevelEnvironment{
            BaseLevel=(int)limit["PlayerLevel"],Level=(int)limit["PlayerLevel"],PlayerBlessLevel=0,
            WorldRank=(int)S.EnumExt.ToRank((S.SubRank)p.SubRank),ServerRecordSeason=levelRules.CreateTables().GetSeasonByRank(S.EnumExt.ToRank((S.SubRank)p.SubRank))};
        var slots=new OfflineTreasureSlotService(profile,slotRules,levelRules,environment);
        var tips=new OfflineTreasureTipService(profile,rules,OfflineTreasureTipCatalog.LoadDefault(),slots);
        Require(slots.SetupTreasureSlot(ordinary,0,"promote-equip").IsApplied,"Equip actual relic for native level-up.");
        Require(slots.SetupTreasureSlot(peer,2,"promote-equip-peer").IsApplied,"Equip same-element sparse physical slot.");
        Require(slots.SetupTreasureSlot(task,task==peer?1:3,"promote-equip-task").IsApplied,"Equip task relic for actual preview.");
        var icons=new OfflineTreasureIconServices(profile,tips,slots,id=>config.GetItem(id),home.PartnerGiftBindings.TipDotSystem,
            ()=>tables.GetMaxTreasureBaseLevel(profile.Save.Player.SubRank),(go,gray)=>home.PartnerGiftBindings.SetUIGray(null,go,gray),
            result=>throw new InvalidOperationException(result.Message),_=>false);
        var player=new OfflineTreasurePropertyPlayer(profile,slots,_=>0);
        var imageCatalog=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        imageCatalog.Entries=detail.Images.Entries.Concat(assets.Images.Entries).GroupBy(row=>row.Path).Select(group=>group.First()).ToArray();
        var images=new OfflineProfessionImageResourceLoader(imageCatalog);
        var textRows=config.Texts.Select(row=>new LanguageRow{key=row.Key,text=row.Value})
            .Concat(detail.Suit.Texts.Select(row=>new LanguageRow{key=row.Key,text=row.Value}))
            .Concat(assets.Texts.Select(row=>new LanguageRow{key=row.Key,text=row.Value})).GroupBy(row=>row.key).Select(group=>group.Last()).ToArray();
        var language=new OfflinePartnerLocalizationService(new TextAsset(JsonUtility.ToJson(new LanguageDocument{texts=textRows})));LanguageManager.Initialize(language);
        var properties=new OfflineProfessionPropertyBindings(detail.PropertyConfiguration,images,language,
            (owner,path,name,desc,x)=>throw new InvalidOperationException("Unexpected entry detail popup."),
            (owner,rank,desc,effect,disabled,season)=>{});
        var windows=Field<OfflineHomeCurrencyWindows>(home.Hud,"nativeCurrencyWindows");var common=Field<OfflineHomeCurrencyWindowBindings>(windows,"bindings");
        var info=(IHomePlayerInfoBindings)Field<object>(home.Hud,"nativePlayerServices");
        var itemContext=new ItemContext{Common=home.PartnerGiftBindings,Data=config,Player=icons};
        var attachments=Field<ItemIconAttachmentManager>(home.PartnerGiftBindings,"attachments");
        var pageServices=new OfflineTreasurePageServices(slots,icons);
        var native=JObject.Parse(File.ReadAllText(Path.Combine(Work,"native.json")));
        var audio=ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();audio.Events=((JObject)native["audio"]).Properties().Select(row=>new OfflineProfessionAudioCatalog.Entry{
            EventId=(uint)row.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)row.Value["pointer"]["guid"]))}).ToArray();
        Require(audio.Events.All(row=>row.Clip&&row.Clip.samples>0),"Every original training click/effect resolves actual verified PCM.");
        var sound=new GameObject("Original cultivation sound owner").AddComponent<OfflineProfessionSoundHost>();sound.Bind(audio);
        int lastError=0,failures=0,settingNotifications=0;var messages=new List<string>();
        var settingSubscription=icons.SubscribeSettings(()=>settingNotifications++);
        OfflineTreasurePromoteServices current=null;
        Func<OfflineTreasurePromoteWindowView> openView=()=>common.Canvas.gameObject.AddComponent<OfflineTreasurePromoteWindowView>();
        Func<OfflineTreasurePromoteWindowView,OfflineTreasurePromoteWindowContext> context=owner=>new OfflineTreasurePromoteWindowContext{
            Configuration=config,Properties=properties,Images=images,Items=itemContext,Attachments=attachments,Icons=icons,Pages=pageServices,
            CommonUseItem=home.PartnerGiftBindings,Windows=windows,CurrencyCatalog=Field<Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog>(home.Hud,"nativeCurrencyCatalog"),CurrencyPlayer=Field<OfflineHomeCurrencyPlayer>(home.Hud,"nativeCurrencyPlayer"),Sound=sound,Font=OfflineHomeHudAssets.LoadDefault().defaultFont,PostSound=common.PostSound,
            CreateServices=windowOwner=>current=new OfflineTreasurePromoteServices(profile,tips,slots,rules,config,tables,player,icons,images,windowOwner.Materials,windowOwner.UseItemBindings,windows,
                ()=>0,code=>lastError=code,result=>{failures++;throw new InvalidOperationException(result.Message);},
                result=>{failures++;throw new InvalidOperationException(result.ErrorCode+" "+result.Message);},windowOwner.PopupAwakeMaterials,windowOwner.InstantiateMaterialRow,messages.Add,windowOwner.ScheduleResponse),
            BindNativeTree=root=>{foreach(var button in root.GetComponentsInChildren<HomeIntroductionPopBtn>(true))button.Configure(info);}
        };
        var view=openView();view.Open(context(view),ordinary);
        File.WriteAllText(Path.Combine(Work,"step.txt"),"promotion-open "+Time.realtimeSinceStartup);
        yield return Ready(view);yield return Capture("01-original-levelup");
        var topBar=Field<Zjcs.Official.HomeHudUI.OfficialHomeTopBarAssetCatalog>(home.Hud,"nativeCurrencyCatalog");
        var sand=assets.Currencies.Single(row=>row.type==PlayerItemType.SandsOfTime);
        Require(topBar.ItemId(PlayerItemType.SandsOfTime)==sand.itemId&&topBar.Icon(sand.itemId)==sand.icon,
            "Original promotion top bar borrows the exact selected-source sands identity and icon.");
        var sandUI=common.Canvas.GetComponentsInChildren<MePlayerItemUI>().Single(row=>Field<PlayerItemType>(row,"mItemType")==PlayerItemType.SandsOfTime);
        Require(Field<Image>(sandUI,"mIcon").sprite==sand.icon&&Field<long?>(sandUI,"mCurrentCount")==current.GetItemAmount(sand.itemId),
            "Actual original sands currency is rendered by the main city's live top-bar owner.");
        foreach(var node in view.Content.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"Complete native promotion has no missing scripts: "+node.name);
        Require(Field<Image>(view.Panel,"mItemIcon").sprite&&Field<Image>(view.Panel,"mQualityIcon").sprite,"Original item icon and exact quality background load dynamically.");
        var level=Field<TreasureLevelupPanel>(view.Panel,"mTreasureLevelupPanel");var upgrade=Field<TreasureUpgradePanel>(view.Panel,"mTreasureUpgradePanel");
        Require(level.gameObject.activeInHierarchy&&!upgrade.gameObject.activeSelf,"Original first tab activates level panel only.");
        var use=Field<PlayerUseItemContent>(level,"mUseItemContent");int beforeLevel=current.GetTreasureFinalLevel(ordinary);
        int beforeBase=current.GetTreasureBaseLevel(ordinary);var quoted=current.GetLevelInfo(beforeLevel+1).Materials;
        var beforeCounts=quoted.ToDictionary(row=>row.Key,row=>current.GetItemAmount(row.Key));
        Require(current.CanSlotLevelUp((S.ElementType)icons.GetElement(ordinary),0,false)==0,"Original funded level qualification succeeds.");
        Click(use.UseItemBtn);yield return new WaitForSeconds(.6f);
        Require(lastError==0&&failures==0&&current.GetTreasureFinalLevel(ordinary)==beforeLevel+1,"Actual original level button commits one slot level.");
        foreach(var cost in quoted)Require(current.GetItemAmount(cost.Key)==beforeCounts[cost.Key]-cost.Value,"Level-up debits exact source cost: "+cost.Key);
        Require(Field<long?>(sandUI,"mCurrentCount")==current.GetItemAmount(sand.itemId),"Source sands top bar observes the committed cultivation debit.");
        yield return Capture("02-original-levelup-effect");
        int notifications=settingNotifications;var auto=Field<Toggle>(level,"mAutoMergeToggle");auto.isOn=!auto.isOn;
        yield return null;yield return null;
        Require(current.AutoMergeWhenLevelUp==auto.isOn&&settingNotifications==notifications+1,"Original level toggle saves, then publishes exactly once.");
        current.SetAutoMergeWhenLevelUp(auto.isOn);yield return null;yield return null;Require(settingNotifications==notifications+2,"Original unchanged setting still publishes after successful local callback.");
        var group=Field<WorldTabGroup>(view.Panel,"mToggleGroup");ClickSelectable(group.GetTabItem(1));yield return Capture("03-original-upgrade");
        Require(upgrade.gameObject.activeInHierarchy&&!level.gameObject.activeSelf,"Original second tab switches complete panel and source title.");
        int oldStar=icons.GetState(icons.GetItemById(ordinary)).Star;current.GetUpgradeMaterials(ordinary,true,current.CanUseCommonMaterial(ordinary),out var starCosts);
        var starBefore=starCosts.ToDictionary(row=>row.Key,row=>current.GetItemAmount(row.Key));
        Click(Field<Button>(upgrade,"mUpgradeBtn"));yield return new WaitForSeconds(.6f);
        Require(icons.GetState(icons.GetItemById(ordinary)).Star==oldStar+1&&lastError==0,"Actual ordinary promotion button saves the source star transition.");
        foreach(var cost in starCosts.Where(row=>row.Key>0))Require(current.GetItemAmount(cost.Key)==starBefore[cost.Key]-cost.Value,"Ordinary promotion debits exact fee: "+cost.Key);
        var awakeAuto=Field<Toggle>(upgrade,"mAutoMergeToggle");awakeAuto.isOn=!awakeAuto.isOn;
        yield return null;yield return null;
        Require(current.AwakeAutoMergeEnable==awakeAuto.isOn,"Awakening automatic merge persists through source toggle.");
        var commonToggle=Field<Toggle>(upgrade,"mUseCommonMaterialToggle");notifications=settingNotifications;commonToggle.isOn=!commonToggle.isOn;
        yield return null;yield return null;
        Require(current.UpgradeUseCommonMaterailEnable==commonToggle.isOn&&settingNotifications==notifications+1,"Common material toggle refreshes before source setting publish.");
        current.PopupAwakeMaterials(panel=>{panel.ItemData=icons.GetItemById(ordinary);panel.SelectItems=new Dictionary<int,long>();});
        yield return new WaitForSeconds(.7f);var awake=common.Canvas.GetComponentsInChildren<TreasureAwakeMaterialPanel>(true).Single();
        Require(awake.ItemData.Id==ordinary&&windows.WindowCount==2,"Original nested awakening-material window initializes before OnEnable.");
        yield return Capture("04-original-awake-materials");
        var rows=awake.GetComponentsInChildren<ProfessionUseItemSliderGroup>(true).Where(row=>row.gameObject.activeInHierarchy).ToArray();
        Require(rows.Length>0,"Source surplus fragments generate original live slider rows.");
        foreach(var row in rows){Require(row.ItemId>0&&row.UseCount==0,"Original rows bind actual fragment IDs and initial count.");row.SetCountWithNotify(1);Require(row.UseCount==1,"Original silent slider count assignment preserves requested count.");row.SetCountWithNotify(0);}
        var slider=Field<Slider>(rows[0],"mySlider");var slideArea=(RectTransform)slider.handleRect.parent;
        var slideCanvas=slider.GetComponentInParent<Canvas>();var slidePoint=RectTransformUtility.WorldToScreenPoint(slideCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:slideCanvas.worldCamera,
            slideArea.TransformPoint(new Vector3(slideArea.rect.xMin+slideArea.rect.width*.75f,slideArea.rect.center.y,0)));
        var slideInput=new PointerEventData(EventSystem.current){position=slidePoint,button=PointerEventData.InputButton.Left};
        var slideHits=new List<RaycastResult>();EventSystem.current.RaycastAll(slideInput,slideHits);
        Require(slideHits.Count>0&&ExecuteEvents.GetEventHandler<IDragHandler>(slideHits[0].gameObject)==slider.gameObject,"Original surplus slider wins actual process-local drag raycast.");
        ExecuteEvents.ExecuteHierarchy(slideHits[0].gameObject,slideInput,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(slideHits[0].gameObject,slideInput,ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(slideHits[0].gameObject,slideInput,ExecuteEvents.pointerUpHandler);
        Require(awake.SelectItems.TryGetValue(rows[0].ItemId,out long selected)&&selected==rows[0].UseCount&&selected<=current.GetStarUpCommonPiece(ordinary,icons.GetState(icons.GetItemById(ordinary)).Star),
            "Actual slider input follows original total-cost cap and updates the shared selection dictionary.");
        Click(Field<Button>(awake,"mAutoSelectBtn"));Require(messages.Count==1,"Original auto-select reports its source result.");
        windows.Close(awake.gameObject);yield return new WaitForSeconds(.7f);Require(windows.WindowCount==1,"Nested source window releases its owner while promotion remains open.");
        bool savedLevelAuto=auto.isOn,savedAwakeAuto=awakeAuto.isOn;
        yield return Close(view,windows);
        var restored=OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        Require(restored.Save.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==ordinary).Star==oldStar+1,"Cold role reopen restores successful star promotion.");
        Require(restored.Save.Player.CharacterProperties.TreasureAutoMergeWhenLevelUp==savedLevelAuto,"Cold reopen restores level automatic-merge preference.");
        Require(restored.Save.Player.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable==savedAwakeAuto,"Cold reopen restores awakening preference.");
        int maxStar=rules.GetTreasure(ordinary).StarMax;
        Require(profile.ApplyInventoryTransaction("promote-awake-fixture",next=>next.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==ordinary).Star=maxStar-1).IsApplied,
            "Enter the actual original awakening boundary in the isolated fixture.");
        icons.Synchronize();view=openView();view.Open(context(view),ordinary);yield return Ready(view);
        ClickSelectable(Field<WorldTabGroup>(view.Panel,"mToggleGroup").GetTabItem(1));yield return Capture("05-original-awakening");
        upgrade=Field<TreasureUpgradePanel>(view.Panel,"mTreasureUpgradePanel");
        Require(Field<Button>(upgrade,"mWakeupBtn").gameObject.activeInHierarchy&&!Field<Button>(upgrade,"mUpgradeBtn").gameObject.activeInHierarchy,
            "Original final-star branch exposes awakening only.");
        current.GetUpgradeMaterials(ordinary,true,true,out var awakeCosts);var awakeBefore=awakeCosts.ToDictionary(row=>row.Key,row=>current.GetItemAmount(row.Key));
        Click(Field<Button>(upgrade,"mWakeupBtn"));yield return new WaitForSeconds(.6f);
        Require(icons.GetState(icons.GetItemById(ordinary)).Star==maxStar&&lastError==0,"Actual original awakening button commits the maximum star.");
        foreach(var cost in awakeCosts.Where(row=>row.Key>0))Require(current.GetItemAmount(cost.Key)==awakeBefore[cost.Key]-cost.Value,"Awakening debits original costs: "+cost.Key);
        yield return Capture("06-original-awakening-effect");yield return Close(view,windows);
        Require(OfflineMainlineProgressSession.OpenCharacter(profiles,slot).Save.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==ordinary).Star==maxStar,
            "Cold reopen restores the actual awakening transition.");
        view=openView();view.Open(context(view),task);yield return Ready(view);
        Require(view.Content.GetComponentsInChildren<TMP_Text>().Any(text=>text.text=="触发次数/上限"),"Actual task preview renders the selected official Chinese count label.");
        yield return Capture("05-original-task-preview");
        Require(view.Panel.ItemData.Id==task,"Original task relic preview binds its saved count and effect rows.");
        yield return Close(view,windows);
        view=openView();view.Open(context(view),peer);yield return Ready(view);ClickSelectable(Field<WorldTabGroup>(view.Panel,"mToggleGroup").GetTabItem(1));
        yield return Capture("06-original-max-star");
        upgrade=Field<TreasureUpgradePanel>(view.Panel,"mTreasureUpgradePanel");
        Require(!Field<Button>(upgrade,"mUpgradeBtn").gameObject.activeInHierarchy&&!Field<Button>(upgrade,"mWakeupBtn").gameObject.activeInHierarchy,"Original maximum-star state hides both transaction buttons.");
        yield return Close(view,windows);settingSubscription.Dispose();icons.Dispose();
        bool currencyReleased=false;try{topBar.ItemId(PlayerItemType.SandsOfTime);}catch(ArgumentOutOfRangeException){currencyReleased=true;}
        Require(currencyReleased,"Closing all original cultivation windows releases their currency metadata scope.");
        bool countBindingReleased=false;try{Field<OfflineHomeCurrencyPlayer>(home.Hud,"nativeCurrencyPlayer").CreateCountProvider(PlayerItemType.SandsOfTime,new PlayerItemUIItemIdProvider{ItemId=sand.itemId});}
        catch(InvalidOperationException){countBindingReleased=true;}
        Require(countBindingReleased,"Closing cultivation releases its additional live currency count binding.");
        Object.Destroy(sound.gameObject);Object.Destroy(audio);Object.Destroy(imageCatalog);Object.Destroy(home.gameObject);yield return null;home=null;fonts.Dispose();fonts=null;
        Require(failures==0&&errors==0,"No unexpected Unity, source dependency or durable transaction failures.");
    }
    private static void ClickSelectable(Selectable button)
    {
        Require(button&&button.isActiveAndEnabled&&button.interactable,"Original tab is interactive.");
        Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Original rendered tab wins actual process-local raycast.");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    [Serializable] private sealed class LanguageRow { public string key,text; }
    [Serializable] private sealed class LanguageDocument { public LanguageRow[] texts; }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Application.logMessageReceived-=Log;
        if(home)home.Close();home=null;fonts?.Dispose();fonts=null;
        if(error==null){File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");Debug.Log("TREASURE_PROMOTE_NATIVE_PASSED: "+checks);}
        else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
