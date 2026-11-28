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
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.TreasureUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using S=Zjcs.Official.TreasureSlots;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TreasurePageNativeValidation
{
    private const string Active="Zjcs.TreasurePageNative.Active";
    private static int checks,errors;
    private static HomeFeatureRuntime home;
    private static FontAssetFontService fonts;
    private static Func<Image,IProfessionImageResourceLoader> previousImages;
    private static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_PAGE_OUTPUT");
    private sealed class Runner:MonoBehaviour
    {
        private IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0){var step=stack.Peek();bool next;try{next=step.MoveNext();}catch(Exception error){Finish(error);yield break;}
                if(!next){stack.Pop();continue;}if(step.Current is IEnumerator nested)stack.Push(nested);else yield return step.Current;}
            Finish(errors==0?null:new Exception("Unexpected Unity errors: "+errors));
        }
    }
    static TreasurePageNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=Log;
            var runner=new GameObject("Original treasure page verification");Object.DontDestroyOnLoad(runner);runner.AddComponent<Runner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Active,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Native treasure page verification timed out."));
        };
    }
    public static void Run()
    {
        string project=Environment.GetEnvironmentVariable("ZJCS_TREASURE_PAGE_PROJECT");
        if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))
            throw new InvalidOperationException("Use the explicit independent source project.");
        typeof(Zjcs.Editor.Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{675,1463});
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)EditorApplication.timeSinceStartup+600);EditorApplication.EnterPlaymode();
    }
    private static void Require(bool value,string detail){checks++;if(!value)throw new InvalidOperationException(detail);}
    private static void Log(string text,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||text.Contains("was not found in the ["))errors++;}
    private static T Field<T>(object instance,string name)
    {
        for(Type type=instance.GetType();type!=null;type=type.BaseType){var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null)return (T)field.GetValue(instance);}
        throw new MissingFieldException(instance.GetType().FullName,name);
    }
    private sealed class Pointer:IHomeSceneClickPointer
    {public bool LeftDown=>false;public bool LeftUp=>false;public Vector2 Position=>Vector2.zero;public int Width=>Screen.width;public int Height=>Screen.height;}
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
        Require(button&&button.isActiveAndEnabled&&button.interactable,"Original selector button is active: "+(button?button.name:"null"));
        Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,
            "Original selector button wins rendered raycasts: "+button.name+", point="+point+", graphics="+string.Join(";",button.GetComponentsInChildren<Graphic>(true).Select(g=>g.name+":"+g.depth+":"+g.canvasRenderer.cull+":"+g.raycastTarget+":"+g.isActiveAndEnabled))+", hits="+string.Join(",",hits.Select(hit=>hit.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();var pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
        pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);pixels.Apply();
        File.WriteAllBytes(Path.Combine(Work,name+".png"),pixels.EncodeToPNG());Object.Destroy(pixels);
    }
    private static Vector2 Vec(JToken row)=>new Vector2((float)row["x"],(float)row["y"]);
    private static void CheckCells(OfflineTreasurePageView view,OfflineTreasureSlotService slots,int element,int selected)
    {
        var page=slots.Snapshot().GetTreasureElementSlotData((S.ElementType)element);var cells=Field<List<ItemIconTreasureSelectableItem>>(view.Content,"mItemList");
        for(int i=0;i<cells.Count;i++){
            int expected=page.TreasureSlots.TryGetValue(i,out var slot)?slot.TreasureClassId:0;
            Require(Field<int>(cells[i],"mTreasureClassId")==expected,"Native physical cell preserves slot index "+i);
            Require(Field<WorldStateSwitcher<bool>>(cells[i],"mEmptyStates").State==(expected<=0),"Native empty state follows actual saved equipment.");
            Require(Field<WorldStateSwitcher<bool>>(cells[i],"mSelectStates").State==(expected==selected),"Native selected frame follows source selection.");
            if(expected>0)Require(cells[i].ItemIcon.myInfo.ClassId==expected&&cells[i].ItemIcon.IconSprite,"Original selected item loads its actual sprite.");
        }
    }
    private static IEnumerator Steps()
    {
        float deadline=Time.realtimeSinceStartup+15;while((Screen.width!=675||Screen.height!=1463)&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(Screen.width==675&&Screen.height==1463,"Independent source process uses the actual portrait surface.");
        var native=JObject.Parse(File.ReadAllText(Path.Combine(Work,"native.json")));int[] order=native["element_order"].Values<int>().ToArray();
        var assets=OfflineTreasureDetailAssets.LoadDefault();var config=new OfflineTreasureDetailConfiguration(assets.Configuration);var rules=OfflineTreasureCatalog.Load();
        var slotConfig=OfflineTreasureSlotCatalog.LoadDefault();
        var picks=order.ToDictionary(e=>e,e=>config.Items.Where(i=>slotConfig.TryGetElement(i.ClassId,out int element)&&element==e).Take(4).Select(i=>i.ClassId).ToArray());
        Require(order.Length==5&&picks.Values.All(ids=>ids.Length==4),"Original authored element order and four real treasures per element exist.");
        string saveRoot=Path.Combine(Work,"isolated-saves");var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);string id=creation.CreateSlot();var draft=creation.OpenDraft(id);draft.name="古遗物分页";
        Require(creation.TryCreateCharacter(id,draft,out string error)&&creation.TrySetInitialWeapon(id,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated real character: "+error);
        var profiles=new ProfileSlotCatalog(saveRoot);var profile=OfflineMainlineProgressSession.OpenCharacter(profiles,id);
        Require(profile.ApplyInventoryTransaction("page-owned-items",next=>{
            foreach(int treasure in picks.Values.SelectMany(x=>x)){
                next.Player.Inventory.Add(new OfflineItemStack{ItemId=treasure,Count=1});next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=treasure,Active=true,Star=1});
            }
        }).IsApplied,"Seed actual owned treasure state in the isolated save.");
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();home=HomeFeatureRuntime.BeginWithNativePointer(id,saveRoot,null,new Pointer());home.TutorialAutoStartEnabled=false;
        for(int i=0;i<12;i++)yield return null;profile=home.Profile;
        var growthTables=OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        Func<OfflinePlayerProgress,TreasureSlotLevelEnvironment> environment=p=>{
            var source=new OfflineHomeExperienceEnvironmentSource(()=>p,growthTables,()=>false,_=>0);var current=source.Capture();
            var growth=OfflineHomeExperienceProjection.Capture(p,growthTables,current);
            return new TreasureSlotLevelEnvironment{BaseLevel=growth.BaseLevel,Level=growth.Level,PlayerBlessLevel=growth.GetCurBlessPlayerLevel(),WorldRank=(int)current.PlanesRank,ServerRecordSeason=current.ServerRecordSeason,ActiveRecordSeason=current.CurrentRecordData?.Season};
        };
        var slots=new OfflineTreasureSlotService(profile,slotConfig,OfflineTreasureSlotLevelCatalog.LoadDefault(),environment);var tips=new OfflineTreasureTipService(profile,rules,OfflineTreasureTipCatalog.LoadDefault(),slots);
        Require(slots.SetupTreasureSlot(picks[order[0]][0],2,"page-initial-slot-two").IsApplied,"Start at actual physical slot two, preserving sparse saved slot order.");
        var icons=new OfflineTreasureIconServices(profile,tips,slots,item=>config.GetItem(item),home.PartnerGiftBindings.TipDotSystem,
            ()=>{var level=slots.CurrentLevelSnapshot(1);return level.Tables.Limits.GetTreasureBaseLevelLimit(level.SubRank);},(go,gray)=>home.PartnerGiftBindings.SetUIGray(null,go,gray),result=>throw new InvalidOperationException(result.Message),_=>false);
        var context=new ItemContext{Common=home.PartnerGiftBindings,Data=config,Player=icons};var services=new OfflineTreasurePageServices(slots,icons);
        var images=new OfflineProfessionImageResourceLoader(assets.Images);previousImages=ProfessionImageResourceBridge.Provider;
        ProfessionImageResourceBridge.Provider=image=>image.GetComponentInParent<TreasurePageSelectItem>(true)?images:previousImages(image);
        var windows=Field<OfflineHomeCurrencyWindows>(home.Hud,"nativeCurrencyWindows");var common=Field<OfflineHomeCurrencyWindowBindings>(windows,"bindings");
        // The enclosing fullscreen window supplies the screen-sized content rect; preserve the authored middle rect.
        var parent=new GameObject("Original promotion middle rect",typeof(RectTransform)).GetComponent<RectTransform>();parent.SetParent(common.Canvas.transform,false);
        var rect=native["original_parent_rects"][0];parent.anchorMin=Vec(rect["m_AnchorMin"]);parent.anchorMax=Vec(rect["m_AnchorMax"]);
        parent.pivot=Vec(rect["m_Pivot"]);parent.sizeDelta=Vec(rect["m_SizeDelta"]);parent.anchoredPosition=Vec(rect["m_AnchoredPosition"]);
        var audio=ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();audio.Events=((JObject)native["audio"]).Properties().Select(row=>new OfflineProfessionAudioCatalog.Entry{
            EventId=(uint)row.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)row.Value["pointer"]["guid"]))}).ToArray();
        var sound=new GameObject("Original selector sound host").AddComponent<OfflineProfessionSoundHost>();sound.Bind(audio);
        int sounds=0;var attachments=Field<ItemIconAttachmentManager>(home.PartnerGiftBindings,"attachments");
        var staging=new GameObject("Original selector pool injection");staging.SetActive(false);
        Action<GameObject> bind=null;
        bind=root=>{
            OfflineTreasureIconBinding.Bind(root,context,icons,attachments,config.TextValue,()=>OfflineHomeHudAssets.LoadDefault().defaultFont);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.font=OfflineHomeHudAssets.LoadDefault().defaultFont;
            foreach(var item in root.GetComponentsInChildren<UIClickSound>(true))item.AkEvent.InstanceHost=sound;
            foreach(var item in root.GetComponentsInChildren<WorldButtonSound>(true))item.Post=(clip,go)=>{Require(clip&&clip.samples>0,"Native arrow click retains original decoded PCM.");sounds++;};
        };
        var uiScope=OfflineUiBindingModule.Register(parent,new OfflineUiBindings{
            Localize=config.TextValue,Font=()=>OfflineHomeHudAssets.LoadDefault().defaultFont,Images=_=>images,
            Pool=(scope,prefab,owner,keepWorld)=>{var go=Object.Instantiate(prefab,staging.transform,false);bind(go);go.transform.SetParent(owner,keepWorld);go.SetActive(true);return go;}});
        var view=new OfflineTreasurePageView(parent,services,bind);
        var cells=Field<List<ItemIconTreasureSelectableItem>>(view.Content,"mItemList");var left=Field<Button>(view.Content,"mLeftBtn");var right=Field<Button>(view.Content,"mRightBtn");
        var selected=new List<int>();view.Content.OnTreasureSelected+=selected.Add;
        view.Show(icons.GetItemById(picks[order[0]][0]));yield return null;yield return null;
        Require(Field<List<S.ElementType>>(view.Content,"mPageOrderElementMenus").Select(e=>(int)e).SequenceEqual(order),"Exact original serialized element order is retained.");
        foreach(var node in view.Root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"No missing original selector component: "+node.name);
        Require(Field<WorldStateSwitcher<bool>>(view.Content,"mSinglePageStates").State&&!left.gameObject.activeInHierarchy&&!right.gameObject.activeInHierarchy,"One occupied element hides both original page arrows.");
        CheckCells(view,slots,order[0],picks[order[0]][0]);yield return Capture("01-single-sparse-page");
        // Add a later physical slot after slot two; element navigation must retain dictionary iteration order.
        Require(slots.SetupTreasureSlot(picks[order[0]][1],0,"page-add-slot-zero").IsApplied,"Second item occupies actual physical slot zero.");
        Require(slots.SetupTreasureSlot(picks[order[2]][0],1,"page-second-element").IsApplied,"Equip a nonadjacent element, leaving the intermediate page empty.");
        view.Hide();view.Show(icons.GetItemById(picks[order[0]][1]));yield return new WaitForSeconds(.25f);
        yield return Capture("02-native-arrows-ready");
        File.WriteAllText(Path.Combine(Work,"arrow-render-state.json"),new JArray(right.GetComponentsInChildren<Graphic>(true).Select(g=>new JObject{
            ["name"]=g.name,["depth"]=g.depth,["active"]=g.isActiveAndEnabled,["color"]=g.color.ToString(),
            ["scale"]=g.transform.lossyScale.ToString(),["position"]=g.transform.position.ToString(),["rect"]=g.rectTransform.rect.ToString(),
            ["canvas"]=g.canvas?g.canvas.name:"null",["canvasEnabled"]=g.canvas&&g.canvas.enabled,
            ["registered"]=g.canvas&&GraphicRegistry.GetGraphicsForCanvas(g.canvas).Contains(g),
            ["sprite"]=g is Image image&&image.sprite?image.sprite.name:"none",
            ["vertices"]=g is Image image2&&image2.sprite?image2.sprite.vertices.Length:0
        })).ToString());
        Require(!Field<WorldStateSwitcher<bool>>(view.Content,"mSinglePageStates").State,"Two occupied elements show original arrows.");
        int callbackCount=selected.Count;Click(Field<Button>(cells[3],"mSelectBtn"));Require(selected.Count==callbackCount,"Original empty physical slot consumes click without selection.");
        Click(Field<Button>(cells[2],"mSelectBtn"));Require(selected.Last()==picks[order[0]][0],"Original cell selects the saved item, including sparse slot two.");
        CheckCells(view,slots,order[0],picks[order[0]][0]);
        Click(right);yield return null;Require(selected.Last()==picks[order[2]][0]&&view.Content.CheckSameElement((S.ElementType)order[2]),"Right arrow skips the unequipped element page.");
        CheckCells(view,slots,order[2],picks[order[2]][0]);yield return Capture("02-skipped-empty-page");
        Click(right);yield return null;int first=slots.Snapshot().GetTreasureElementSlotData((S.ElementType)order[0]).TreasureSlots.Values.First(s=>s.TreasureClassId>0).TreasureClassId;
        Require(selected.Last()==first,"Wrapped page selects first equipped item in original dictionary iteration order.");
        CheckCells(view,slots,order[0],first);Click(left);yield return null;Require(selected.Last()==picks[order[2]][0],"Left arrow wraps and skips every empty page.");
        for(int e=0;e<order.Length;e++)for(int slot=0;slot<4;slot++)Require(slots.SetupTreasureSlot(picks[order[e]][slot],slot,"page-fill-"+e+"-"+slot).IsApplied||slots.Snapshot().GetTreasureSlotData((S.ElementType)order[e],slot)?.TreasureClassId==picks[order[e]][slot],"Populate actual saved element "+order[e]+" slot "+slot);
        icons.Synchronize();view.Hide();view.Show(icons.GetItemById(picks[order[0]][0]));yield return new WaitForSeconds(.25f);
        string file=Path.Combine(profiles.GetSlotDirectory(id),"city-save.json");byte[] before=File.ReadAllBytes(file);
        for(int e=1;e<=order.Length;e++){
            Click(right);yield return null;int element=order[e%order.Length];int expected=slots.Snapshot().GetTreasureElementSlotData((S.ElementType)element).TreasureSlots.Values.First(s=>s.TreasureClassId>0).TreasureClassId;
            Require(view.Content.CheckSameElement((S.ElementType)element)&&selected.Last()==expected,"Original right-arrow cycle follows authored element order.");
            CheckCells(view,slots,element,expected);
        }
        for(int i=0;i<4;i++){Click(Field<Button>(cells[i],"mSelectBtn"));Require(selected.Last()==picks[order[0]][i],"Every original cell dispatches exactly its actual item.");CheckCells(view,slots,order[0],picks[order[0]][i]);}
        yield return Capture("03-full-native-selector");
        for(int i=0;i<3;i++){
            view.Hide();Require(view.Content.CheckSameElement(S.ElementType.None)&&cells.All(c=>c.ItemIcon.myInfo==null),"Original disable clears every icon and resets page element.");
            view.Show(icons.GetItemById(picks[order[4]][2]));yield return new WaitForSeconds(.25f);int count=selected.Count;Click(Field<Button>(cells[2],"mSelectBtn"));
            Require(selected.Count==count+1,"Reopening never duplicates native selection subscriptions.");
        }
        var cold=OfflineMainlineProgressSession.OpenCharacter(profiles,id);var restored=new OfflineTreasureSlotService(cold,slotConfig);
        for(int e=0;e<order.Length;e++)for(int i=0;i<4;i++)Require(restored.Snapshot().GetTreasureSlotData((S.ElementType)order[e],i).TreasureClassId==picks[order[e]][i],"Cold reopen preserves all actual equipped selections.");
        Require(before.SequenceEqual(File.ReadAllBytes(file)),"Page arrows, cell selection and repeated reopening leave the saved role byte-identical.");
        Require(sounds>=order.Length,"Original arrow sound callback runs through native click listeners.");
        view.Dispose();icons.Dispose();uiScope.Dispose();Object.Destroy(staging);Object.Destroy(parent.gameObject);Object.Destroy(sound.gameObject);home.Close();home=null;yield return null;
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Application.logMessageReceived-=Log;
        if(previousImages!=null)ProfessionImageResourceBridge.Provider=previousImages;previousImages=null;
        if(home)home.Close();home=null;fonts?.Dispose();fonts=null;
        if(error==null){File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");Debug.Log("TREASURE_PAGE_NATIVE_PASSED: "+checks);}
        else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
