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
public static class TreasureIconsNativeValidation
{
    private const string Active = "Zjcs.TreasureIconsNative.Active";
    private static int checks, errors;
    private static Camera camera;
    private static RenderTexture target;
    private static FontAssetFontService fonts;
    private static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_ICONS_OUTPUT");
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
    static TreasureIconsNativeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            Application.runInBackground = true;
            AudioListener.volume = 0;
            Application.logMessageReceived += Log;
            new GameObject("TreasureIconsNativeValidation").AddComponent<Runner>();
        };
        EditorApplication.update += () =>
        {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Treasure star native runtime verification timed out."));
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_TREASURE_ICONS_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets")
            || string.IsNullOrEmpty(Work)) throw new InvalidOperationException("Select the independent source validation project explicitly.");
        ValidationGameView.Portrait();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active+".Deadline", (float)EditorApplication.timeSinceStartup+180);
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
        public ItemInfo Info;public int Clicks;
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
        public void PopupInfoDialog(IProfessionItemData data,IWindowOperator owner,long required,Action<GameObject> callback){Clicks++;}
        public void PopupInfoDialog(IProfessionItemInfo info,IWindowOperator owner,long required,Action<GameObject> callback){Clicks++;}
        public GameObject GetAttachment(ItemIconAttachmentType type,Transform parent,bool active)=>throw new InvalidOperationException("Unexpected nonnative probe attachment: "+type);
        public void RecycleAttachment(ItemIconAttachmentType type,ItemIconAttachmentCell cell)=>throw Unexpected();
        private static Exception Unexpected()=>new InvalidOperationException("Unexpected enclosing-window dependency in the native icon probe.");
    }
    private static IEnumerator Steps()
    {
        checks=errors=0;
        camera=new GameObject("Native treasure render camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=731.5f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,1);
        target=new RenderTexture(675,1463,24);target.Create();camera.targetTexture=target;
        var canvas=new GameObject("Native treasure host",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,2340);
        var host=new GameObject("Inactive binding slot",typeof(RectTransform)).GetComponent<RectTransform>();host.SetParent(canvas.transform,false);
        host.anchorMin=host.anchorMax=new Vector2(.5f,.5f);host.sizeDelta=Vector2.zero;host.gameObject.SetActive(false);
        var probe=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Work,"probe.json")));
        var item=(Newtonsoft.Json.Linq.JObject)probe["item"];
        var info=new ItemInfo{ClassId=(int)item["ClassId"],Quality=(Quality)(int)item["Quality"],Icon=(string)item["RawIcon"]};
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath((string)probe["sprite"]["guid"]));
        Require(sprite!=null&&sprite.name=="treasure_"+info.ClassId,"Exact original treasure sprite imports for its real item metadata.");
        var resources=ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>();
        resources.Entries=new[]{new Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog.Entry{Path="Assets/Image/"+info.Icon,AssetGuid="probe-treasure",Asset=sprite}};
        var loader=new Zjcs.Offline.ProfessionUI.OfflineProfessionImageResourceLoader(resources);
        ProfessionImageResourceBridge.Provider=_=>loader;
        string saveRoot=Path.Combine(Work,"isolated-saves");var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="原生古遗物";
        Require(creation.TryCreateCharacter(slot,draft,out string error),"Create isolated real character: "+error);
        Require(creation.TrySetInitialWeapon(slot,Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Select actual initial weapon: "+error);
        var profiles=new Zjcs.Offline.State.ProfileSlotCatalog(saveRoot);
        var profile=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        var common=Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        Func<Zjcs.Offline.Progression.OfflinePlayerProgress,Zjcs.Offline.Progression.TreasureSlotLevelEnvironment> environment=p=>{
            var source=new Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceEnvironmentSource(()=>p,common,()=>false,_=>0);
            var state=source.Capture();var growth=Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceProjection.Capture(p,common,state);
            return new Zjcs.Offline.Progression.TreasureSlotLevelEnvironment{BaseLevel=growth.BaseLevel,Level=growth.Level,PlayerBlessLevel=growth.GetCurBlessPlayerLevel(),WorldRank=(int)state.PlanesRank,ServerRecordSeason=state.ServerRecordSeason,ActiveRecordSeason=state.CurrentRecordData?.Season};
        };
        var rules=Zjcs.Offline.Progression.OfflineTreasureCatalog.Load();
        var slots=new Zjcs.Offline.Progression.OfflineTreasureSlotService(profile,Zjcs.Offline.Progression.OfflineTreasureSlotCatalog.LoadDefault(),Zjcs.Offline.Progression.OfflineTreasureSlotLevelCatalog.LoadDefault(),environment);
        var tips=new Zjcs.Offline.Progression.OfflineTreasureTipService(profile,rules,Zjcs.Offline.Progression.OfflineTreasureTipCatalog.LoadDefault(),slots);
        Require(profile.ApplyInventoryTransaction("native-icon-seed",next=>{
            next.Player.Inventory.RemoveAll(row=>tips.Catalog.Get(row.ItemId)?.Type==305);
            next.Player.Inventory.Add(new Zjcs.Offline.Progression.OfflineItemStack{ItemId=info.ClassId,Count=1});
            next.Player.CharacterProperties.Treasures.Clear();next.Player.CharacterProperties.Treasures.Add(new Zjcs.Offline.Progression.OfflineCharacterTreasure{ClassId=info.ClassId,Active=false,Star=0});
        }).IsApplied,"Seed real inactive treasure in isolated save.");
        int failures=0,grayCalls=0;var tipDots=new TipDots();
        var services=new Zjcs.Offline.TreasureUI.OfflineTreasureIconServices(profile,tips,slots,id=>{
            if(id==info.ClassId)return info;var meta=tips.Catalog.Get(id);return meta==null?null:new ItemInfo{ClassId=id,Quality=(Quality)meta.Quality};
        },tipDots,()=>{var p=slots.CurrentLevelSnapshot(1);return p.Tables.Limits.GetTreasureBaseLevelLimit(p.SubRank);},(go,gray)=>grayCalls++,_=>failures++,_=>false);
        var context=new Context{Info=info,Player=services,Config=ScriptableObject.CreateInstance<ItemIconConfig>()};
        var prefab=Resources.Load<GameObject>("Treasure/NativeIcons/ItemIconTreasurePanel");Require(prefab!=null,"Original panel item prefab loads.");
        var go=Object.Instantiate(prefab,host,false);
        var texts=((Newtonsoft.Json.Linq.JArray)probe["texts"]).ToDictionary(t=>(string)t["Key"],t=>(string)t["Value"]);
        var hud=Zjcs.Offline.HomeHudUI.OfflineHomeHudAssets.LoadDefault();fonts=hud.CreateFontService();fonts.Initialize();
        Require(hud.defaultFont!=null&&hud.defaultFont.sourceFontFile!=null,"Shared original regional font service supplies its true font file.");
        Zjcs.Offline.TreasureUI.OfflineTreasureIconBinding.Bind(go,context,services,context,k=>texts.TryGetValue(k,out string value)?value:k,()=>hud.defaultFont);
        var icon=go.GetComponent<ItemIconTreasurePanel>();Require(icon!=null,"Native treasure panel controller resolves.");
        foreach(var node in go.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"No missing native controller: "+node.name);
        host.gameObject.SetActive(true);icon.Init(services.GetItemById(info.ClassId));icon.ShowStar().SetLevel().SetRedDot().SetCenterHintContent(true);
        yield return null;yield return null;
        Require(icon.IconSprite==sprite&&icon.IconAnim.clip==icon.UnactivateAnim,"Original inactive branch selects true item art and its native animation.");
        var first=Capture("01-inactive");yield return new WaitForSeconds(.4f);var second=Capture("02-inactive-pulse");
        Require(!first.SequenceEqual(second),"Original inactive animation changes actual rendered pixels.");
        var dots=go.GetComponentsInChildren<TreasureRedDotItem>(true);Require(dots.Length==2&&dots.Count(d=>d.IsShow)==1,"Original red-dot controllers and mutex show only the active priority.");
        Require(tips.Cultivation.ActivateTreasure(info.ClassId,"native-icon-activate",1).IsApplied,"Actual activation commits.");
        yield return null;yield return null;
        Require(icon.IconAnim.clip==icon.NormalAnim,"Committed activation selects original normal animation.");Capture("03-active");
        Require(icon.ClickSound.AkEvent.IsValid,"Original click event reference is preserved with its decoded audio closure.");
        var audioCatalog=ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionAudioCatalog>();
        audioCatalog.Events=((Newtonsoft.Json.Linq.JObject)probe["audio"]).Properties().Select(row=>new Zjcs.Offline.ProfessionUI.OfflineProfessionAudioCatalog.Entry{
            EventId=(uint)row.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)row.Value["pointer"]["guid"]))}).ToArray();
        Require(audioCatalog.Events.All(e=>e.Clip!=null&&e.Clip.samples>0),"Original Wwise event resolves the decoded PCM clip.");
        var audioHost=new GameObject("Owned original click audio").AddComponent<Zjcs.Offline.ProfessionUI.OfflineProfessionSoundHost>();audioHost.Bind(audioCatalog);
        var events=new GameObject("Isolated pointer event host").AddComponent<UnityEngine.EventSystems.EventSystem>();
        icon.ClickSound.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(events){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});
        Require(icon.ClickSound.AkEvent.PlayingId!=0&&Object.FindObjectsOfType<AudioSource>().Any(s=>audioCatalog.Events.Any(e=>e.Clip==s.clip)),"Original click callback posts to the actual local PCM playback host while muted.");
        Object.Destroy(events.gameObject);Object.Destroy(audioHost.gameObject);
        tips.Cultivation.GetTreasureNextLevelUpgradeMaterial(info.ClassId,false,false,out var materials);
        Require(profile.ApplyInventoryTransaction("native-icon-fund-star",next=>{
            foreach(var fee in materials){var stack=next.Player.Inventory.FirstOrDefault(r=>r.ItemId==fee.Key);if(stack==null)next.Player.Inventory.Add(new Zjcs.Offline.Progression.OfflineItemStack{ItemId=fee.Key,Count=fee.Value});else stack.Count+=fee.Value;}
        }).IsApplied,"Fund exact original next-star costs in the isolated role.");
        services.Synchronize();
        Require(tips.Cultivation.UpgradeTreasure(info.ClassId,false,false,"native-icon-star",1).IsApplied,"Actual single-star cultivation commits through the common transaction.");
        yield return null;yield return null;Capture("03b-promoted");
        var starView=go.GetComponentInChildren<TreasureStarInfo>(true);
        Require(services.GetState(icon.myItemData).Star==1&&starView.StarMiniInfo.StarListSlot.GetChild(0).GetChild(0).gameObject.activeSelf,"Committed star refresh reaches the original visible star child.");
        foreach(Quality quality in Enum.GetValues(typeof(Quality))){icon.UpdateQualityImage(quality);if((int)quality>=0&&(int)quality<icon.QualityBgImages.Count)Require(icon.QualityImg.sprite==icon.QualityBgImages[(int)quality],"Exact native quality image index.");}
        icon.InitQualityImage();
        var listener=services.ReplaceListener;var replace=new TreasureReplaceInfo{OriginIndex=0,ElementType=services.GetElement(info.ClassId),TreasureClassId=info.ClassId};
        bool ordering=false;listener.OnTreasureStartReplace+=_=>ordering=listener.GetReplaceInfo()==null;
        listener.FireOnTreasureStartReplace(replace);Require(ordering&&icon.ReplaceBtn.gameObject.activeSelf,"Original replace event fires before storing request; active item exposes button.");
        yield return null;Capture("03c-replace");
        icon.ReplaceBtn.onClick.Invoke();Require(slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==0,"Native replace button commits actual local slot setup.");
        listener.FireOnTreasureStartReplace(replace);icon.ReplaceBtn.onClick.Invoke();Require(listener.GetReplaceInfo()==null&&!icon.ReplaceBtn.gameObject.activeSelf,"Same-slot replacement cancels using original branch.");
        string file=Path.Combine(profiles.GetSlotDirectory(slot),"city-save.json");var beforeBytes=File.ReadAllBytes(file);
        listener.FireOnTreasureStartReplace(new TreasureReplaceInfo{OriginIndex=1,ElementType=services.GetElement(info.ClassId),TreasureClassId=info.ClassId});
        Directory.CreateDirectory(file+".tmp");
        try{icon.ReplaceBtn.onClick.Invoke();Require(failures==1&&slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==0&&beforeBytes.SequenceEqual(File.ReadAllBytes(file)),"Actual save failure preserves equipment and file while reporting failure.");}
        finally{Directory.Delete(file+".tmp");}
        icon.ReplaceBtn.onClick.Invoke();Require(slots.Snapshot().GetTreasureInSlotIndex(info.ClassId)==1,"Retry through the same native button succeeds after the write failure clears.");
        listener.FireOnTreasureCancelReplace();
        Require(profile.ApplyInventoryTransaction("native-icon-slot-level",next=>{
            var trained=next.Player.CharacterProperties.TreasureSlots.Elements.Single(e=>e.ElementType==services.GetElement(info.ClassId)).Slots.Single(s=>s.Index==1);
            trained.BaseLevel=2;
        }).IsApplied,"Prepare an actual saved slot level for original display verification.");
        services.Synchronize();yield return null;
        Require(icon.myTextGo.activeSelf&&icon.myText.Text=="+2","Committed slot state refreshes the original level text.");
        icon.UnhasShow(true);Require(grayCalls==1,"Unowned display dispatches exact icon target to enclosing gray service.");
        int settings=0;using(services.SubscribeSettings(()=>settings++)){Require(tips.SetAutoMergeWhenLevelUp(false,"native-icon-settings").IsApplied,"Setting commits to same role.");services.Synchronize();Require(settings==1,"Setting subscription observes committed preference exactly once.");}
        go.SetActive(false);services.Synchronize();go.SetActive(true);icon.Init(services.GetItemById(info.ClassId));icon.ShowStar().SetLevel().SetRedDot();
        yield return null;Capture("04-reopened");
        var refreshField=typeof(Zjcs.Offline.TreasureUI.OfflineTreasureIconServices).GetField("OnTreasureRefresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        Require(((Delegate)refreshField.GetValue(services)).GetInvocationList().Length==3,"Reopening retains exactly one icon and two original red-dot refresh subscriptions.");
        Require(failures==1,"Native interactions have no hidden transaction failures.");
        icon.Clear();Require(icon.myInfo==null&&icon.myItemData==null&&!icon.ReplaceBtn.gameObject.activeSelf,"Original pool clear releases metadata and replacement mode.");
        icon.Init(services.GetItemById(info.ClassId));icon.ShowStar().SetLevel().SetRedDot();
        yield return null;
        var oldItem=icon.myItemData;var oldState=services.GetState(oldItem);
        Require(profile.ApplyInventoryTransaction("native-icon-remove",next=>{
            next.Player.Inventory.RemoveAll(r=>r.ItemId==info.ClassId);
            next.Player.CharacterProperties.Treasures.RemoveAll(r=>r.ClassId==info.ClassId);
        }).IsApplied,"Remove the actual stack while an icon still owns its original item wrapper.");
        services.Synchronize();yield return null;
        Require(services.GetItemById(info.ClassId)==null&&services.GetState(oldItem)?.Active==oldState.Active,"Removed-stack queries return null while an existing icon retains the original wrapper parameter.");
        Require(profile.ApplyInventoryTransaction("native-icon-restore",next=>{
            next.Player.Inventory.Add(new Zjcs.Offline.Progression.OfflineItemStack{ItemId=info.ClassId,Count=1});
            next.Player.CharacterProperties.Treasures.Add(new Zjcs.Offline.Progression.OfflineCharacterTreasure{ClassId=info.ClassId,Active=oldState.Active,Star=oldState.Star,HitCount=oldState.HitCount});
        }).IsApplied,"Restore only the owned probe stack in the same isolated role.");
        services.Synchronize();yield return null;
        Object.Destroy(go);yield return null;
        Require(refreshField.GetValue(services)==null,"Destroying the native hierarchy releases all three item refresh subscriptions.");
        services.Dispose();
        Require(tipDots.OnTipDotShowChanged==null,"Closing native controls releases all shared tip-dot subscriptions.");
        var cold=Zjcs.Offline.Progression.OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        Require(cold.Save.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==info.ClassId).Active&&!cold.Save.Player.CharacterProperties.TreasureAutoMergeWhenLevelUp,"Native operations survive a cold save reopen.");
        ProfessionImageResourceBridge.Provider=null;
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
            Debug.Log("TREASURE_ICONS_NATIVE_PASSED: "+checks);
        }
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
