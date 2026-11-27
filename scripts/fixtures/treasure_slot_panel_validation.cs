using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Editor;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TreasureSlotPanelValidation
{
    private const string Active = "Zjcs.TreasureSlotPanel.Active";
    private static int checks, errors;
    private static Camera camera;
    private static RenderTexture target;
    private static FontAssetFontService fonts;
    private static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_SLOT_PANEL_OUTPUT");
    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var steps = Steps();
            while (true)
            {
                bool next;
                try { next = steps.MoveNext(); }
                catch (Exception error) { Finish(error); yield break; }
                if (!next) break;
                yield return steps.Current;
            }
            Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
        }
    }
    static TreasureSlotPanelValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            Application.runInBackground = true;
            AudioListener.volume = 0;
            Application.logMessageReceived += Log;
            new GameObject("TreasureSlotPanelValidation").AddComponent<Runner>();
        };
        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Treasure slot panel native runtime verification timed out."));
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_TREASURE_SLOT_PANEL_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets")
            || string.IsNullOrEmpty(Work)) throw new InvalidOperationException("Select the independent source validation project explicitly.");
        ValidationGameView.Portrait();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active+".Deadline", (float)EditorApplication.timeSinceStartup+300);
        EditorApplication.EnterPlaymode();
    }
    private static void Require(bool value, string detail)
    { checks++; if (!value) throw new InvalidOperationException(detail); }
    private static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || text.Contains("was not found in the [")) errors++; }
    private sealed class ItemInfo:IProfessionItemInfo
    {
        public int ClassId{get;set;}public string Icon{get;set;}public string IconEffect=>"";
        public Quality Quality{get;set;}public bool CanOverlap=>false;
        public object GetFakeIcon()=>throw new InvalidOperationException("The selected original treasure has a real icon.");
    }
    private sealed class TipDots:IProfessionTipDotSystem
    {
        public bool IsShowTipDot{get;set;}=true;public bool TempLimitShowTip=>false;
        public Action OnTipDotShowChanged{get;set;}
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();
        public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>throw new InvalidOperationException("Native icon red dots do not query graphs.");
    }
    private sealed class Context:IProfessionItemIconContext,IProfessionItemColors,IItemIconAttachmentManager
    {
        public ItemIconConfig Config{get;set;}public UnityEngine.Object GrayConfig=>null;
        public IProfessionItemColors Colors=>this;public IProfessionItemPlayer Player{get;set;}
        public ItemInfo Info;public int Clicks;public Action<GameObject> OpenDetail;
        public Color Green=>Color.green;public Color White=>Color.white;public Color LightRed=>Color.red;
        public Color GetTextColor(ThemeColorType theme,ColorType type)=>Color.white;
        public IProfessionItemInfo GetItemInfo(int id)=>Info.ClassId==id?Info:throw new InvalidOperationException("Unexpected probe info lookup: "+id);
        public object Compact(long count)=>count.ToString();public object CultureText(string key)=>key;
        public string GetItemNameKey(int id)=>throw Unexpected();
        public bool TryGetSourceData(int id,out IReadOnlyList<IProfessionItemSource> rows){rows=null;throw Unexpected();}
        public bool CanMerge(int id,bool recursion)=>throw Unexpected();public object GetLocationInfos(int id)=>throw Unexpected();
        public void SetUIGray(UnityEngine.Object config,GameObject target,bool gray)=>throw Unexpected();
        public void SetSoundSwitch(ProfessionItemSoundSwitch value,GameObject emitter)=>throw Unexpected();
        public GameObject Instantiate(GameObject prefab,Transform parent,EUIInstantiateFlag flag=EUIInstantiateFlag.All)=>Object.Instantiate(prefab,parent,false);
        public void InstantiateAsync(string prefab,Transform parent,Action<GameObject> callback)=>throw Unexpected();
        public void PopupInfoDialog(IProfessionItemData data,IWindowOperator owner,long required,Action<GameObject> callback){Clicks++;var go=new GameObject("Fixture detail boundary");var options=go.AddComponent<DetailOptions>();callback?.Invoke(go);OpenDetail?.Invoke(go);Object.Destroy(go);}
        public void PopupInfoDialog(IProfessionItemInfo info,IWindowOperator owner,long required,Action<GameObject> callback){Clicks++;var go=new GameObject("Fixture detail boundary");var options=go.AddComponent<DetailOptions>();callback?.Invoke(go);OpenDetail?.Invoke(go);Object.Destroy(go);}
        public GameObject GetAttachment(ItemIconAttachmentType type,Transform parent,bool active)=>throw new InvalidOperationException("Unexpected nonnative probe attachment: "+type);
        public void RecycleAttachment(ItemIconAttachmentType type,ItemIconAttachmentCell cell)=>throw Unexpected();
        private static Exception Unexpected()=>new InvalidOperationException("Unexpected enclosing-window dependency in the native icon probe.");
    }
    private sealed class DetailOptions:MonoBehaviour,ITreasureDetailOptions
    {
        public bool Operation,Replace,Npc,State=true;
        public void SetCanOperation(bool canOperation)=>Operation=canOperation;
        public void SetCanShowReplace(bool canShow)=>Replace=canShow;
        public void SetCanShowNpcTreasureAndState(bool canShow,bool canShowState){Npc=canShow;State=canShowState;}
    }
    private sealed class Moderation:Zjcs.Official.WorldUI.IWorldContentModerationServices
    {
        private Action<HashSet<long>> changed;
        public int Subscriptions;
        public event Action<HashSet<long>> DisabledUpdated{add{changed+=value;Subscriptions++;}remove{changed-=value;Subscriptions--;}}
        public bool CheckDisabled(int type)=>false;
        public void ShowError(int code)=>throw new InvalidOperationException("Unexpected moderation click: "+code);
    }
    private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(instance);
    private static void Click(Button button)
    {
        Require(button.isActiveAndEnabled&&button.interactable,"Actual native button is enabled: "+button.name);
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();
        Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=new UnityEngine.EventSystems.PointerEventData(events){position=point,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var hits=new List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Actual rendered slot/plan button wins UI raycasts: "+button.name+", point="+point+", rect="+rect.rect+", graphics="+string.Join(";",button.GetComponentsInChildren<Graphic>(true).Select(g=>g.name+":"+g.depth+":"+g.canvasRenderer.cull+":"+g.raycastTarget))+", hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
    private static UnityEngine.EventSystems.EventSystem events;
    private static IEnumerator Steps()
    {
        checks=errors=0;
        var probe=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Work,"probe.json")));
        var row=probe["item"];var info=new ItemInfo{ClassId=(int)row["ClassId"],Icon=(string)row["RawIcon"],Quality=(Quality)(int)row["Quality"]};
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath((string)probe["sprite"]["guid"]));
        Require(sprite!=null,"Actual original treasure icon imports.");
        var resources=ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>();
        resources.Entries=new[]{new Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog.Entry{Path="Assets/Image/"+info.Icon,AssetGuid="probe",Asset=sprite}};
        var imageLoader=new Zjcs.Offline.ProfessionUI.OfflineProfessionImageResourceLoader(resources);ProfessionImageResourceBridge.Provider=_=>imageLoader;
        var assets=Zjcs.Offline.TreasureUI.OfflineTreasureSlotsAssets.LoadDefault();
        Require(assets.Sprites.Length==5&&assets.Sprites.All(s=>s.Sprite!=null),"All five original element background GUIDs resolve actual sprites.");
        var data=Zjcs.Offline.TreasureUI.OfflineBattlePlanUiCatalog.LoadDefault();
        var sourceTexts=((Newtonsoft.Json.Linq.JArray)probe["texts"]).ToDictionary(t=>(string)t["Key"],t=>(string)t["Value"]);
        Func<string,string> text=key=>sourceTexts.TryGetValue(key,out string value)?value:assets.Text(key)==key?data.Text(key):assets.Text(key);
        var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(Path.Combine(Work,"isolated-saves"));
        string profileId=creation.CreateSlot();var draft=creation.OpenDraft(profileId);draft.name="槽位界面";
        Require(creation.TryCreateCharacter(profileId,draft,out string error)&&creation.TrySetInitialWeapon(profileId,Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated actual character: "+error);
        var profiles=new Zjcs.Offline.State.ProfileSlotCatalog(Path.Combine(Work,"isolated-saves"));
        var profile=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(profiles,profileId);
        string file=Path.Combine(profiles.GetSlotDirectory(profileId),"city-save.json");
        var common=Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        Func<Zjcs.Offline.Progression.OfflinePlayerProgress,Zjcs.Offline.Progression.TreasureSlotLevelEnvironment> environment=p=>{
            var source=new Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceEnvironmentSource(()=>p,common,()=>false,_=>0);var current=source.Capture();
            var growth=Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceProjection.Capture(p,common,current);
            return new Zjcs.Offline.Progression.TreasureSlotLevelEnvironment{BaseLevel=growth.BaseLevel,Level=growth.Level,PlayerBlessLevel=growth.GetCurBlessPlayerLevel(),WorldRank=(int)current.PlanesRank,ServerRecordSeason=current.ServerRecordSeason,ActiveRecordSeason=current.CurrentRecordData?.Season};
        };
        var configuration=Zjcs.Offline.Progression.OfflineTreasureCatalog.Load();var slotConfig=Zjcs.Offline.Progression.OfflineTreasureSlotCatalog.LoadDefault();
        var levelConfiguration=Zjcs.Offline.Progression.OfflineTreasureSlotLevelCatalog.LoadDefault();
        var slots=new Zjcs.Offline.Progression.OfflineTreasureSlotService(profile,slotConfig,levelConfiguration,environment);
        var tips=new Zjcs.Offline.Progression.OfflineTreasureTipService(profile,configuration,Zjcs.Offline.Progression.OfflineTreasureTipCatalog.LoadDefault(),slots);
        int element=(int)row["ElementType"];
        Require(profile.ApplyInventoryTransaction("slot-panel-item",next=>{
            next.Player.Inventory.Add(new Zjcs.Offline.Progression.OfflineItemStack{ItemId=info.ClassId,Count=1});
            next.Player.CharacterProperties.Treasures.Add(new Zjcs.Offline.Progression.OfflineCharacterTreasure{ClassId=info.ClassId,Active=true,Star=1});
        }).IsApplied,"Seed owned activated treasure for actual slot transactions.");
        Require(slots.SetupTreasureSlot(info.ClassId,0,"slot-panel-setup").IsApplied,"Initial equipment commits to physical slot zero.");
        var trained=slots.Snapshot();var trainedSlot=trained.GetTreasureSlotData((Zjcs.Official.TreasureSlots.ElementType)element,0);trainedSlot.BaseLevel=8;
        trained.GetTreasureElementSlotData((Zjcs.Official.TreasureSlots.ElementType)element).UpdateTreasureSlotsShareLevel();
        Require(profile.ApplyInventoryTransaction("slot-panel-training",next=>next.Player.CharacterProperties.TreasureSlots=Zjcs.Offline.Progression.OfflineTreasureSlotService.Capture(trained)).IsApplied,"Prepare actual saved training and shared level for rendering.");
        bool disabled=false;int failures=0,gray=0,sounds=0;var confirmations=new Queue<Action>();var messages=new List<string>();
        var names=new Zjcs.Offline.Progression.OfflineTreasurePlanNameService(profile,slotConfig.MaxPlanCount,data.NameMaximum(0));
        var plans=new Zjcs.Offline.TreasureUI.OfflineTreasurePlanWidgetServices(profile,slots,names,data,0,()=>false,()=>disabled,
            (owner,key,accepted)=>{Require(key=="ui_battle_plan_clear_confirm_tip","Original clear confirmation key and request boundary.");confirmations.Enqueue(accepted);},
            (code,args)=>throw new InvalidOperationException("Unexpected plan UI error: "+code),messages.Add,_=>failures++);
        var dots=new TipDots();
        var icons=new Zjcs.Offline.TreasureUI.OfflineTreasureIconServices(profile,tips,slots,id=>{
            if(id==info.ClassId)return info;var original=tips.Catalog.Get(id);return original==null?null:new ItemInfo{ClassId=id,Quality=(Quality)original.Quality};
        },dots,()=>{var p=slots.CurrentLevelSnapshot(1);return p.Tables.Limits.GetTreasureBaseLevelLimit(p.SubRank);},(go,value)=>{Require(!value,"Equipped icons reset original gray state.");gray++;},_=>failures++,_=>false);
        var context=new Context{Info=info,Player=icons,Config=ScriptableObject.CreateInstance<ItemIconConfig>()};
        context.OpenDetail=go=>{var view=go.GetComponent<DetailOptions>();Require(view.Operation&&view.Replace&&view.Npc&&!view.State,"Original equipped-icon callback passes all three detail options and hides NPC state.");};
        var hud=Zjcs.Offline.HomeHudUI.OfflineHomeHudAssets.LoadDefault();fonts=hud.CreateFontService();fonts.Initialize();
        var audio=ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionAudioCatalog>();
        audio.Events=((Newtonsoft.Json.Linq.JObject)probe["audio"]).Properties().Select(p=>new Zjcs.Offline.ProfessionUI.OfflineProfessionAudioCatalog.Entry{EventId=(uint)p.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)p.Value["pointer"]["guid"]))}).ToArray();
        var soundHost=new GameObject("Native slot audio").AddComponent<Zjcs.Offline.ProfessionUI.OfflineProfessionSoundHost>();soundHost.Bind(audio);
        var factory=new Zjcs.Offline.TreasureUI.OfflineTreasureIconFactory(context,icons,context,text,()=>hud.defaultFont,soundHost);
        var requests=new Zjcs.Offline.TreasureUI.OfflineTreasurePlanRequests(slots,_=>failures++);
        var services=new Zjcs.Offline.TreasureUI.OfflineTreasureSlotsPanelServices(icons,plans,requests,slots,factory,configuration.Settings.TreasureSlotPropAddRate);
        camera=new GameObject("Native slots render camera",typeof(AudioListener)).AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=731.5f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,1);target=new RenderTexture(675,1463,24);target.Create();camera.targetTexture=target;
        var canvas=new GameObject("Original slot-panel host",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,2340);
        // The source's parent VerticalLayoutGroup drives this subtree's zero-sized authored RectTransform.
        var parent=new GameObject("Original slot-panel layout host",typeof(RectTransform),typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();parent.SetParent(canvas.transform,false);
        parent.anchorMin=Vector2.zero;parent.anchorMax=Vector2.one;parent.offsetMin=parent.offsetMax=Vector2.zero;
        var layoutData=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Work,"layout.json")))["layout"];
        var layout=parent.GetComponent<VerticalLayoutGroup>();layout.childAlignment=(TextAnchor)(int)layoutData["m_ChildAlignment"];layout.spacing=(float)layoutData["m_Spacing"];
        layout.padding=new RectOffset((int)layoutData["m_Padding"]["m_Left"],(int)layoutData["m_Padding"]["m_Right"],(int)layoutData["m_Padding"]["m_Top"],(int)layoutData["m_Padding"]["m_Bottom"]);
        layout.childForceExpandWidth=(int)layoutData["m_ChildForceExpandWidth"]!=0;layout.childForceExpandHeight=(int)layoutData["m_ChildForceExpandHeight"]!=0;
        layout.childControlWidth=(int)layoutData["m_ChildControlWidth"]!=0;layout.childControlHeight=(int)layoutData["m_ChildControlHeight"]!=0;
        events=new GameObject("Owned synthetic pointer events").AddComponent<UnityEngine.EventSystems.EventSystem>();
        var planAudio=soundHost.gameObject.AddComponent<AudioSource>();planAudio.playOnAwake=false;planAudio.spatialBlend=0;
        var moderation=new Moderation();var panel=new Zjcs.Offline.TreasureUI.OfflineTreasureSlotsPanel(parent,services,assets,moderation,hud.defaultFont,text,(clip,go)=>{Require(clip!=null&&clip.samples>0,"Original plan-button PCM clip exists.");planAudio.PlayOneShot(clip);sounds++;});
        panel.Show((Zjcs.Official.TreasureSlots.ElementType)element);yield return null;yield return null;
        var cells=panel.Root.GetComponentsInChildren<Zjcs.Official.TreasureSlotsUI.TreasureSlotItem>(true);
        Require(cells.Length==slotConfig.MaxSlotCount,"Original serialized slot count agrees with current official settings.");
        foreach(var node in panel.Root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"No missing original slot/plan/icon controller: "+node.name);
        var visible=cells.Single(c=>Field<Transform>(c,"mTreasureIconSlot").gameObject.activeSelf);
        var icon=Field<ItemIconTreasurePanel>(visible,"mItemIcon");
        Require(icon.IconSprite==sprite&&icon.transform.localPosition==Vector3.zero&&icon.transform.localRotation==Quaternion.identity&&icon.transform.localScale==Vector3.one,"Original dynamic icon uses exact art and UIUtils All transform reset.");
        Require(icon.myTextGo.activeSelf&&icon.myText.Text=="+8"&&gray>0,"Original equipped icon reads actual trained slot level and clears gray state.");
        Require(Field<Zjcs.Official.WorldUI.WorldMultiLanguageText>(panel.Content,"mShareLevelText").Text.Contains("1"),"Original shared level remains one until all four physical slots exist.");
        Capture("01-equipped-slots");
        Click(icon.Button);Require(context.Clicks==1,"Original equipped-icon button invokes its existing detail host.");
        byte[] unchanged=File.ReadAllBytes(file);
        foreach(var type in slotConfig.CreateSettings().TreasureSlotElementTypes)
        {
            panel.Content.Init(type);yield return null;
            foreach(var cell in cells)
            {
                var switcher=Field<Zjcs.Official.WorldUI.WorldStateSwitcher<Zjcs.Official.TreasureSlots.ElementType>>(cell,"mElementBgStates");
                Require(switcher.State==type,"Original slot background state follows selected element.");
                var configs=Field<List<Zjcs.Official.WorldUI.IWorldStateConfig>>(switcher,"mStateConfigs");
                foreach(var config in configs.OfType<Zjcs.Official.HomeHudUI.StateSpriteAssetProxyConfig>())
                foreach(var binding in Field<Zjcs.Official.HomeHudUI.StateSpriteAssetProxyData>(config,"mStateProxyData").SpriteRendererConfigs)
                {
                    Require(binding.Proxy.TryGetValue((int)type,out var proxy),"Original selected element has its authored sprite GUID.");
                    using(var handle=assets.LoadAssetByGuid<Sprite>(proxy.Guid))Require(binding.Img.sprite==handle.Result,"Exact original GUID-backed sprite appears on the native slot image.");
                }
            }
            Capture("02-element-"+(int)type);
        }
        Require(unchanged.SequenceEqual(File.ReadAllBytes(file)),"Changing original element tabs never persists lazy slot queries.");
        panel.Content.Init((Zjcs.Official.TreasureSlots.ElementType)element);yield return null;
        var levelTables=levelConfiguration.CreateTables();
        var blessRank=Enum.GetValues(typeof(Zjcs.Official.TreasureSlots.SubRank)).Cast<Zjcs.Official.TreasureSlots.SubRank>().First(rank=>
            levelTables.Limits.GetTreasureBlessLevelLimit(rank)>=2&&levelTables.GetSeasonByRank(Zjcs.Official.TreasureSlots.EnumExt.ToRank(rank))>0);
        int blessCap=levelTables.Limits.GetTreasureBaseLevelLimit(blessRank),season=levelTables.GetSeasonByRank(Zjcs.Official.TreasureSlots.EnumExt.ToRank(blessRank));
        var blessed=slots.Snapshot();
        foreach(var type in slotConfig.CreateSettings().TreasureSlotElementTypes)
        {
            var group=blessed.GetTreasureElementSlotData(type);
            for(int index=0;index<slotConfig.MaxSlotCount;index++)
            {
                if(!group.TreasureSlots.ContainsKey(index))group.InsertTreasureToSlot(0,index);
                group.TreasureSlots[index].BaseLevel=blessCap;group.TreasureSlots[index].BlessLevel=(int)type==element&&index==0?2:0;
            }
            group.UpdateTreasureSlotsShareLevel();
        }
        blessed.TreasureSlotData.SeasonId=season;blessed.BlessRecordFormat=0;blessed.BlessRecords.Clear();
        Require(profile.ApplyInventoryTransaction("slot-panel-blessing",next=>{
            next.Player.SubRank=(int)blessRank;next.Player.SeasonRank=(int)Zjcs.Official.TreasureSlots.EnumExt.ToRank(blessRank);next.Player.HasActiveSeasonRecord=true;
            next.Player.CharacterProperties.TreasurePlaneRank=next.Player.SeasonRank;next.Player.Experience=100000000000L;next.Player.BlessingExperience=100000000000L;
            next.Player.CharacterProperties.TreasureSlots=Zjcs.Offline.Progression.OfflineTreasureSlotService.Capture(blessed);
        }).IsApplied,"Prepare explicit original seasonal rank, all twenty physical slots and the actual blessing cache.");
        Require(slots.SynchronizeTreasureSlotSeason("slot-panel-bless-season",1).IsApplied,"Original season synchronization persists the seeded blessing history.");
        icons.Synchronize();services.Synchronize();yield return null;
        Require(icon.myText.Text=="+"+(blessCap+2)&&icon.myText.GetComponent<TMPro.TMP_Text>().enableVertexGradient,"Native original level text displays real seasonal blessing levels and its source gradient.");Capture("02b-blessed");
        Require(Field<Zjcs.Official.WorldUI.WorldMultiLanguageText>(panel.Content,"mShareLevelText").Text.Contains(blessCap.ToString()),"The full original slot group displays its minimum trained shared level.");
        var replace=new TreasureReplaceInfo{TreasureClassId=info.ClassId,ElementType=element,OriginIndex=0};icons.ReplaceListener.FireOnTreasureStartReplace(replace);
        Require(cells.All(c=>Field<Button>(c,"mReplaceBtn").gameObject.activeSelf),"Original replace event enables every authored slot target.");yield return null;
        Click(Field<Button>(cells[2],"mReplaceBtn"));yield return null;
        Require(slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==2&&Field<Transform>(cells[2],"mTreasureIconSlot").gameObject.activeSelf,"Native slot replacement commits and refreshes the physical target.");
        Require(slots.Snapshot().GetTreasureSlotData((Zjcs.Official.TreasureSlots.ElementType)element,0).BaseLevel==blessCap&&slots.Snapshot().GetTreasureSlotData((Zjcs.Official.TreasureSlots.ElementType)element,0).BlessLevel==2,"Replacing a treasure keeps prior physical base and seasonal blessing cultivation.");
        Click(Field<Button>(cells[2],"mReplaceBtn"));Require(cells.All(c=>!Field<Button>(c,"mReplaceBtn").gameObject.activeSelf),"Original same-target request cancels the shared replace mode.");
        icons.ReplaceListener.FireOnTreasureStartReplace(replace);var before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try{Click(Field<Button>(cells[1],"mReplaceBtn"));Require(failures==1&&slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==2&&before.SequenceEqual(File.ReadAllBytes(file)),"Real failed slot save preserves equipment and reports failure.");}
        finally{Directory.Delete(file+".tmp");}
        Click(Field<Button>(cells[1],"mReplaceBtn"));Require(slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==1,"The same native target retries after storage recovery.");icons.ReplaceListener.FireOnTreasureCancelReplace();
        var next=Field<Button>(panel.PlanWidget,"mNextPlanBtn");var previous=Field<Button>(panel.PlanWidget,"mPrevPlanBtn");
        Click(next);yield return null;Require(panel.PlanWidget.PlanIndex==1&&slots.Snapshot().ActivePlanIndex==1&&!slots.Snapshot().IsTreasureInSlot(info.ClassId),"Original plan navigation applies an empty plan and refreshes all original slots.");
        Capture("03-empty-plan");
        var input=Field<TMPro.TMP_InputField>(panel.PlanWidget,"mPlanNameInput");input.text="槽位方案";input.onEndEdit.Invoke(input.text);Require(names.GetName(1)=="槽位方案","Embedded original name input persists the current plan name.");
        icons.ReplaceListener.FireOnTreasureStartReplace(replace);yield return null;Click(Field<Button>(cells[3],"mReplaceBtn"));icons.ReplaceListener.FireOnTreasureCancelReplace();yield return null;
        Require(slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==3,"Native slot selection writes separate equipment into the named second plan.");
        Click(previous);yield return null;Require(slots.Snapshot().IsTreasureInSlot(info.ClassId)&&slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==1,"Original previous navigation restores the actual equipped plan.");
        before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try{Click(next);Require(panel.PlanWidget.PlanIndex==0&&next.interactable&&failures==2&&before.SequenceEqual(File.ReadAllBytes(file)),"Failed original plan callback rolls the widget back before reporting one error.");}
        finally{Directory.Delete(file+".tmp");}
        disabled=true;services.Synchronize();Require(!panel.PlanWidget.gameObject.activeSelf,"Actual enclosing availability update hides the embedded plan widget.");
        disabled=false;services.Synchronize();yield return null;Require(panel.PlanWidget.gameObject.activeSelf,"Availability restoration reenables the same original widget.");
        Click(next);yield return null;var clear=Field<Button>(panel.PlanWidget,"mClearBtn");Click(clear);
        Require(confirmations.Count==1&&slots.Snapshot().ActivePlanIndex==1&&slots.Snapshot().IsTreasureInSlot(info.ClassId),"Native clear defers the mutation to its original confirmation callback.");
        before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try{confirmations.Dequeue()();Require(failures==3&&slots.Snapshot().IsTreasureInSlot(info.ClassId)&&before.SequenceEqual(File.ReadAllBytes(file)),"Failed confirmed clear reports one local error and preserves the equipped plan and saved file.");}
        finally{Directory.Delete(file+".tmp");}
        Click(clear);confirmations.Dequeue()();Require(!slots.Snapshot().IsTreasureInSlot(info.ClassId),"Confirmed clear retries and removes actual worn content after storage recovers.");
        Require(names.GetName(1)=="槽位方案"&&messages.Last()==data.Text("ui_battle_plan_clear_success_tip"),"Confirmed original clear preserves metadata and posts its source message.");
        panel.Root.SetActive(false);Require(moderation.Subscriptions==0,"Disabling the full native subtree releases moderation listeners.");
        panel.Show((Zjcs.Official.TreasureSlots.ElementType)element);yield return null;
        Require(moderation.Subscriptions==1,"Reopening the same native subtree retains one moderation subscription.");Capture("04-reopened");
        panel.Dispose();yield return null;services.Dispose();factory.Dispose();icons.Dispose();Object.Destroy(soundHost.gameObject);
        Require(moderation.Subscriptions==0&&dots.OnTipDotShowChanged==null&&icons.ReplaceListener.OnTreasureStartReplace==null&&icons.ReplaceListener.OnTreasureCancelReplace==null,"Closing native slots releases plan, background, replacement and icon listeners.");
        var cold=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(profiles,profileId);
        Require(cold.Save.Player.CharacterProperties.TreasureSlots.ActivePlanIndex==1&&new Zjcs.Offline.Progression.OfflineTreasurePlanNameService(cold,slotConfig.MaxPlanCount,data.NameMaximum(0)).GetName(1)=="槽位方案","Native plan state and Chinese name survive a cold role reopen.");
        Require(sounds>0&&failures==3,"Actual plan PCM is dispatched and only the three deliberate storage failures are reported: sounds="+sounds+", failures="+failures);ProfessionImageResourceBridge.Provider=null;
    }
    private static byte[] Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply(); RenderTexture.active = previous;
        var bytes = texture.GetRawTextureData();
        Require(bytes.Where((b,i)=>i%4<3).Distinct().Count()>8, "Actual native star artwork renders into the owned target.");
        File.WriteAllBytes(Path.Combine(Work,name+".png"),texture.EncodeToPNG()); Object.Destroy(texture);
        return bytes;
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false); Application.logMessageReceived -= Log;fonts?.Dispose();
        if (error == null)
        {
            File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");
            Debug.Log("TREASURE_SLOT_PANEL_NATIVE_PASSED: "+checks);
        }
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
