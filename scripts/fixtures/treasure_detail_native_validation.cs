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
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Presentation.Home;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TreasureDetailNativeValidation
{
    private const string Active="Zjcs.TreasureDetailNative.Active";
    private static int checks,errors;
    private static HomeFeatureRuntime home;
    private static FontAssetFontService fonts;
    private static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_DETAIL_OUTPUT");
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
    static TreasureDetailNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=Log;
            var runner=new GameObject("Treasure detail runtime verification");Object.DontDestroyOnLoad(runner);runner.AddComponent<Runner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Active,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Complete original treasure detail verification timed out."));
        };
    }
    public static void Run()
    {
        string project=Environment.GetEnvironmentVariable("ZJCS_TREASURE_DETAIL_PROJECT");
        if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))
            throw new InvalidOperationException("Use the explicit independent source project.");
        typeof(Zjcs.Editor.Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{675,1463});
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)EditorApplication.timeSinceStartup+900);EditorApplication.EnterPlaymode();
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
    private static IEnumerator Ready(OfflineTreasureDetailWindowView view)
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
    private static IEnumerator Close(OfflineTreasureDetailWindowView view,OfflineHomeCurrencyWindows windows)
    {
        view.Close();float end=Time.realtimeSinceStartup+15;
        while(view.Content&&Time.realtimeSinceStartup<end)yield return null;
        Require(!view.Content&&windows.WindowCount==0,"Original hide animation closes content and blur, releasing the window owner.");
        Object.Destroy(view);yield return null;
    }
    private static IEnumerator Steps()
    {
        float deadline=Time.realtimeSinceStartup+15;while((Screen.width!=675||Screen.height!=1463)&&Time.realtimeSinceStartup<deadline)yield return null;
        Require(Screen.width==675&&Screen.height==1463,"Independent process uses the actual portrait surface.");
        var assets=OfflineTreasureDetailAssets.LoadDefault();var config=new OfflineTreasureDetailConfiguration(assets.Configuration);
        Require(!config.TextValue("ui_blessing_player_level_tip_2").Contains("{Replace.")&&
            config.TextValue("ui_blessing_player_level_tip_2").Contains(config.TextValue("Replace.Item61")),
            "Original language replacement resolves item aliases before numeric formatting.");
        var replacer=new WorldTextReplacer(key=>key=="named"?"原始":key);replacer.Register("dynamic",()=>7);
        Require(replacer.ReplaceText("{0}/{c}/{named}/{dynamic}/tail{")=="{0}/{c}/原始/7/tail",
            "Original replacement retains numeric/color arguments, dynamic callbacks and unmatched-brace behavior.");
        Require(replacer.TryMatchReplaceKey("{0}{named}{c}{dynamic}",out var replaceKeys)&&replaceKeys.SetEquals(new[]{"named","dynamic"}),
            "Original replacement-key discovery excludes numeric and color arguments.");
        var tables=OfflineTreasurePropertyCatalog.LoadDefault();var rules=OfflineTreasureCatalog.Load();
        int ordinary=tables.Treasures.First(row=>row.Effect is TreasureEffectNone&&config.GetItem(row.ClassId).Quality==5).ClassId;
        int task=tables.Treasures.First(row=>row.Effect is TreasureEffectTask).ClassId;
        int npcTreasure=config.Items.First(row=>config.GetNpcByTreasure(row.ClassId)!=null&&row.ClassId!=ordinary&&row.ClassId!=task).ClassId;
        string saveRoot=Path.Combine(Work,"isolated-saves");var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="古遗物详情";
        Require(creation.TryCreateCharacter(slot,draft,out string error)&&creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated actual character: "+error);
        var profiles=new ProfileSlotCatalog(saveRoot);var profile=OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        Require(profile.ApplyInventoryTransaction("detail-start",next=>{
            next.Player.Inventory.Add(new OfflineItemStack{ItemId=ordinary,Count=1});
            next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=ordinary,Active=false,Star=0,HitCount=0});
            next.Player.Inventory.Add(new OfflineItemStack{ItemId=task,Count=1});
            next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=task,Active=true,Star=2,HitCount=17});
            next.Player.Inventory.Add(new OfflineItemStack{ItemId=npcTreasure,Count=1});
            next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=npcTreasure,Active=true,Star=0,HitCount=0});
            foreach(var material in rules.GetUpgradeRule(config.GetItem(ordinary).Quality,0).Materials)
                next.Player.Inventory.Add(new OfflineItemStack{ItemId=material.ItemId,Count=material.Count+100});
            next.Player.Inventory.Add(new OfflineItemStack{ItemId=rules.GetTreasure(ordinary).TreasurePieceId,Count=37});
        }).IsApplied,"Seed real inactive and task treasures, original activation materials and fragments.");
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService();fonts.Initialize();
        home=HomeFeatureRuntime.BeginWithNativePointer(slot,saveRoot,null,new Pointer());home.TutorialAutoStartEnabled=false;
        for(int i=0;i<12;i++)yield return null;
        profile=home.Profile;
        var windows=Field<OfflineHomeCurrencyWindows>(home.Hud,"nativeCurrencyWindows");
        var common=Field<OfflineHomeCurrencyWindowBindings>(windows,"bindings");
        var info=(IHomePlayerInfoBindings)Field<object>(home.Hud,"nativePlayerServices");
        var slots=new OfflineTreasureSlotService(profile,OfflineTreasureSlotCatalog.LoadDefault());
        var tips=new OfflineTreasureTipService(profile,rules,OfflineTreasureTipCatalog.LoadDefault(),slots);
        var icons=new OfflineTreasureIconServices(profile,tips,slots,id=>config.GetItem(id),home.PartnerGiftBindings.TipDotSystem,
            ()=>tables.GetMaxTreasureBaseLevel(profile.Save.Player.SubRank),
            (go,gray)=>home.PartnerGiftBindings.SetUIGray(home.PartnerGiftBindings.GrayConfig,go,gray),
            result=>throw new InvalidOperationException("Unexpected detail slot failure: "+result.Message),_=>false);
        var player=new OfflineTreasurePropertyPlayer(profile,slots,_=>throw new InvalidOperationException("Owned fixture task parameters must supply their actual saved hit count."));
        var images=new OfflineProfessionImageResourceLoader(assets.Images);
        var textRows=config.Texts.Select(row=>new{key=row.Key,text=row.Value}).Concat(assets.Suit.Texts.Select(row=>new{key=row.Key,text=row.Value}))
            .GroupBy(row=>row.key).Select(group=>group.First()).ToArray();
        var language=new OfflinePartnerLocalizationService(new TextAsset(JsonUtility.ToJson(new LanguageDocument{texts=textRows.Select(row=>new LanguageRow{key=row.key,text=row.text}).ToArray()})));
        LanguageManager.Initialize(language);
        var properties=new OfflineProfessionPropertyBindings(assets.PropertyConfiguration,images,language,
            (owner,path,name,desc,x)=>throw new InvalidOperationException("Entry-detail popup outside this fixture."),
            (owner,rank,desc,effect,disabled,season)=>{Require(!string.IsNullOrEmpty(desc),"Original blessing button produces actual property text.");});
        var native=JObject.Parse(File.ReadAllText(Path.Combine(Work,"native.json")));
        var audio=ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
        audio.Events=((JObject)native["audio"]).Properties().Select(row=>new OfflineProfessionAudioCatalog.Entry{
            EventId=(uint)row.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)row.Value["pointer"]["guid"]))}).ToArray();
        Require(audio.Events.All(row=>row.Clip&&row.Clip.samples>0),"Every original detail-click Wwise event has actual decoded PCM.");
        var sound=new GameObject("Owned original detail click host").AddComponent<OfflineProfessionSoundHost>();sound.Bind(audio);
        int promoteCount=0;var navigation=new List<(int Id,bool Flag,bool Piece)>();
        Func<OfflineTreasureDetailWindowView> openView=()=>common.Canvas.gameObject.AddComponent<OfflineTreasureDetailWindowView>();
        OfflineTreasureDetailWindowView view=openView();
        OfflineTreasureDetailServices current=null;
        Func<OfflineTreasureDetailWindowView,OfflineTreasureDetailWindowContext> context=owner=>new OfflineTreasureDetailWindowContext{
            Configuration=config,Properties=properties,Images=images,Windows=windows,Sound=sound,Sharing=new Share(),Font=OfflineHomeHudAssets.LoadDefault().defaultFont,
            ItemDetails=new OfflineTreasureItemDetailBindings(config,tips.Cultivation,home.PartnerGiftBindings,images,
                _=>Array.Empty<ItemDetailSource>(),(prefab,parent)=>owner.Instantiate(prefab,parent),windows.Close),
            PostSound=common.PostSound,
            PopupTreasure=(id,flag)=>navigation.Add((id,flag,false)),PopupPiece=(id,flag)=>navigation.Add((id,flag,true)),
            CreateServices=suits=>current=new OfflineTreasureDetailServices(tables,player,icons,tips.Cultivation,slots,rules,config,suits,
                ()=>0,()=>info.PlanesRank,rank=>string.Format(config.TextValue("ui_blessing_player_level_tip_2"),info.GetSeasonNameByRank(rank)),
                windows.Close,(parent,item,complete)=>{promoteCount++;Require(item.Id==ordinary,"Original training request retains actual item identity.");complete();},
                code=>throw new InvalidOperationException("Unexpected original activation error: "+code),
                result=>throw new InvalidOperationException("Unexpected local activation failure: "+result.Message)),
            BindNativeTree=root=>{
                foreach(var row in root.GetComponentsInChildren<TreasurePlayerRedDotItemBase>(true))row.ConfigureTreasure(icons);
                foreach(var button in root.GetComponentsInChildren<HomeIntroductionPopBtn>(true))button.Configure(info);
            }
        };
        view.Open(context(view),ordinary,true,true);yield return Ready(view);
        foreach(var node in view.Content.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"Full original detail has no missing script: "+node.name);
        var itemDetail=view.Content.GetComponent<ItemDetailDialog>();
        Require(itemDetail.NameText.Text==config.TextValue("item_"+ordinary+"_name")&&itemDetail.ItemImage.sprite,"Original icon and custom item name appear in the complete window.");
        Require(!view.Panel.ShareItemBtn.ShareBtn.gameObject.activeSelf,"The confirmed offline chat policy hides actual world-chat sharing.");
        var itemBindings=(OfflineTreasureItemDetailBindings)context(view).ItemDetails;
        var palette=JObject.Parse(assets.Configuration.text)["GlobalColors"]["QualityColorConfig"]["TextColorLists"]
            .Single(row=>(int)row["ThemeColorType"]==0);
        for(int quality=0;quality<8;quality++){
            itemBindings.SetQualityText(itemDetail.QualityText,(Quality)quality);
            var expected=palette["TextColors"][quality];var parsed=config.QualityColor((Quality)quality);
            Require(parsed.EnableGradient==(int)expected["EnableGradient"],"Original quality gradient branch survives local serialization.");
            if(parsed.EnableGradient!=0){
                var color=parsed.GradientColor.Value().Evaluate(0);var key=expected["GradientColor"]["key0"];
                Require(Math.Abs(color.r-(float)key["r"])<.00001f&&Math.Abs(color.g-(float)key["g"])<.00001f,
                    "Original quality gradient endpoints survive local serialization.");
            }
        }
        itemBindings.SetQualityText(itemDetail.QualityText,(Quality)config.GetItem(ordinary).Quality);
        var piece=Field<WorldMultiLanguageText>(view.Panel,"mPieceItemCountText");Require(piece.Text.Contains("37"),"Full detail reads real fragment count.");
        var suit=Field<TreasureSuitContent>(view.Panel,"mSuit");
        var suitInfo=current.GetSuitByTreasure(ordinary);
        if(suitInfo!=null){
            int other=suitInfo.Treasures.First(id=>id!=ordinary&&!profile.Save.Player.Inventory.Any(row=>row.ItemId==id));
            var members=suit.mySuitItemNameRoot.GetComponentsInChildren<TreasureDetailSuitItem>();
            int index=suitInfo.Treasures.IndexOf(other);navigation.Clear();Click(members[index].Button);
            Require(navigation.Count==1&&navigation[0]==(current.GetPieceIdByTreasureId(other),true,true),
                "Changing operation flags after original initialization also updates native suite navigation.");
        }
        yield return Capture("01-inactive-activation");
        var activate=Field<Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent>(view.Panel,"mActivateContent");
        Click(activate.Btn);float end=Time.realtimeSinceStartup+15;while(view.Content&&Time.realtimeSinceStartup<end)yield return null;
        Require(!view.Content&&tips.Cultivation.GetItemById(ordinary).State.Active,"Actual native activation saves the treasure, then closes with original hide animation.");
        Object.Destroy(view);yield return null;icons.Synchronize();
        string saveFile=Path.Combine(profiles.GetSlotDirectory(slot),"city-save.json");byte[] before=File.ReadAllBytes(saveFile);
        view=openView();view.Open(context(view),ordinary,true,true);yield return Ready(view);
        Require(Field<Button>(view.Panel,"mTrainOpBtn").gameObject.activeInHierarchy&&Field<Button>(view.Panel,"mReplaceBtn").gameObject.activeInHierarchy,"Actual activated item shows original training and replace state.");
        yield return Capture("02-active-training");
        Click(Field<Button>(view.Panel,"mTrainOpBtn"));end=Time.realtimeSinceStartup+15;while(view.Content&&Time.realtimeSinceStartup<end)yield return null;
        Require(promoteCount==1&&!view.Content,"Original training host completes before closing the originating detail.");
        Object.Destroy(view);yield return null;
        view=openView();view.Open(context(view),ordinary,true,true);yield return Ready(view);
        Click(Field<Button>(view.Panel,"mReplaceBtn"));end=Time.realtimeSinceStartup+15;
        while(view.Content&&Time.realtimeSinceStartup<end)yield return null;
        var replacement=icons.ReplaceListener.GetReplaceInfo();
        Require(!view.Content&&replacement!=null&&replacement.TreasureClassId==ordinary&&replacement.OriginIndex==current.GetTreasureInSlotIndex(ordinary),
            "Original replace button closes its detail and dispatches the current item, element and slot.");
        icons.ReplaceListener.FireOnTreasureCancelReplace();Object.Destroy(view);yield return null;
        view=openView();view.Open(context(view),task,false,false);yield return Ready(view);yield return Capture("03-owned-task");
        Require(!Field<Button>(view.Panel,"mTrainOpBtn").gameObject.activeInHierarchy,"Read-only task details hide operation controls.");
        Require(view.Content.GetComponentsInChildren<WorldMultiLanguageText>().Any(text=>text.Text.Contains("17")),"Original task-effect rows read actual saved trigger count.");
        yield return Close(view,windows);
        var snapshot=new SnapshotTreasure{ItemId=ordinary,BaseLevel=4,BlessLevel=2,SubRank=profile.Save.Player.SubRank,InSlot=true,
            TreasureParam=new Zjcs.Official.TreasureCultivation.TreasureState{Active=true,Star=2,HitCount=9},ActivedInSuit=new Dictionary<int,int>()};
        view=openView();view.Open(context(view),snapshot);yield return Ready(view);
        Require(!Field<GameObject>(view.Panel,"mPieceItemCountGo").activeSelf&&!view.Panel.ShareItemBtn.ShareBtn.gameObject.activeSelf,
            "Original snapshot hides owned fragment count and chat sharing.");
        Require(Field<WorldMultiLanguageText>(view.Panel,"mLevelText").Text=="+6","Original snapshot level sums base and blessing levels.");
        yield return Capture("04-snapshot-blessing");yield return Close(view,windows);
        view=openView();view.Open(context(view),npcTreasure);yield return Ready(view);yield return null;
        var npcRows=view.Content.GetComponentsInChildren<TreasureNpcEffectItem>();
        Require(npcRows.Length==config.GetNpcByTreasure(npcTreasure).Count,"Original GUID proxy creates every configured NPC addition row.");
        foreach(var row in npcRows){
            Require(Field<ImageProxy>(row,"mNpcIcon").Sprite&&Field<ImageProxy>(row,"mNpcBgIcon").Sprite,
                "Actual original NPC portrait and friendship background load in the full detail.");
            Require(Field<WorldMultiLanguageText>(row,"mFixedPropValueText2").Text.Contains("%"),"Original NPC addition text formats its actual quality percentage.");
        }
        yield return Capture("05-npc-addition");yield return Close(view,windows);
        var cold=OfflineMainlineProgressSession.OpenCharacter(profiles,slot);
        Require(cold.Save.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==ordinary).Active,"Cold role reopen restores native activation.");
        Require(before.SequenceEqual(File.ReadAllBytes(saveFile)),"Training navigation, task viewing and full-window close/reopen never rewrite the saved role.");
        icons.Dispose();Object.Destroy(sound.gameObject);home.Close();home=null;yield return null;
    }
    [Serializable] private sealed class LanguageRow { public string key,text; }
    [Serializable] private sealed class LanguageDocument { public LanguageRow[] texts; }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Application.logMessageReceived-=Log;
        if(home)home.Close();home=null;fonts?.Dispose();fonts=null;
        if(error==null){File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");Debug.Log("TREASURE_DETAIL_NATIVE_PASSED: "+checks);}
        else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
