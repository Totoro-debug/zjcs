using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.TreasureUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TreasureSuitsNativeValidation
{
    private const string Active="Zjcs.TreasureSuitsNative.Active";
    private static int checks,errors;
    private static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_SUITS_OUTPUT");
    private static Camera camera;
    private static RenderTexture target;
    private static EventSystem events;
    private static FontAssetFontService fonts;
    private sealed class Runner:MonoBehaviour
    {
        private IEnumerator Start()
        {
            var steps=Steps();
            while(true)
            {
                bool next;try{next=steps.MoveNext();}catch(Exception error){Finish(error);yield break;}
                if(!next)break;yield return steps.Current;
            }
            Finish(errors==0?null:new Exception("Unexpected Unity errors: "+errors));
        }
    }
    static TreasureSuitsNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Active,false))return;
            Application.runInBackground=true;AudioListener.volume=0;Application.logMessageReceived+=Log;
            new GameObject("TreasureSuitsNativeValidation").AddComponent<Runner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Active,false)&&EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Original treasure suit verification timed out."));
        };
    }
    public static void Run()
    {
        string project=Environment.GetEnvironmentVariable("ZJCS_TREASURE_SUITS_PROJECT");
        if(string.IsNullOrEmpty(project)||Path.GetFullPath(Application.dataPath)!=Path.Combine(Path.GetFullPath(project),"Assets")||string.IsNullOrEmpty(Work))
            throw new InvalidOperationException("Use the explicit independent source project.");
        typeof(Zjcs.Editor.Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{675,1463});
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)EditorApplication.timeSinceStartup+600);
        EditorApplication.EnterPlaymode();
    }
    private static void Require(bool value,string detail){checks++;if(!value)throw new InvalidOperationException(detail);}
    private static void Log(string text,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||text.Contains("was not found in the ["))errors++;}
    private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
    private sealed class Texts:ILocalizationTextService
    {
        private readonly OfflineTreasureSuitAssets assets;
        public Texts(OfflineTreasureSuitAssets assets){this.assets=assets;}
        public LanguageType CurrentLanguage=>LanguageType.zh_CN;
        public void ReloadData(){}public bool ContainsKey(string key)=>assets.Texts.Any(row=>row.Key==key);
        public bool TryGetOriginalText(string key,LanguageType language,out string result){result=assets.Text(key);return ContainsKey(key);}
        public bool TryGetText(string key,LanguageType language,out string result)=>TryGetOriginalText(key,language,out result);
        public bool TryGetText(string key,out string result)=>TryGetOriginalText(key,CurrentLanguage,out result);
        public string GetText(string key,LanguageType language,params object[] args)=>args==null||args.Length==0?assets.Text(key):string.Format(assets.Text(key),args);
        public string GetTextFormated(string key,LanguageType language,params object[] args)=>string.Format(assets.Text(key),args??Array.Empty<object>());
        public void SetReplacer(ITextReplacer replacer){}public void AddDataProvider(ILanguageTextDataProvider provider){}
    }
    private sealed class Items:IProfessionItemPlayer
    {
        public OfflineMainlineProgressSession Session;
        public long GetItemAmount(int id)=>Session.Save.Player.Inventory.Where(row=>row.ItemId==id).Sum(row=>row.Count);
        public event Action<IProfessionItemData,long,object> OnItemCountUpdate{add{}remove{}}
        public bool CanGetItemFromMapLocation(object location)=>throw new InvalidOperationException("Suit view never queries map acquisition.");
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();
        var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,
            "Original rendered member button wins raycast: "+button.name+", point="+point+", hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Steps()
    {
        var assets=OfflineTreasureSuitAssets.LoadDefault();var tables=OfflineTreasurePropertyCatalog.LoadDefault();
        Require(assets.Prefab&&assets.PropertyConfiguration&&assets.PropertyImages,"Actual original suit prefab and property resources import.");
        var language=new Texts(assets);LanguageManager.Initialize(language);
        var resources=new OfflineProfessionImageResourceLoader(assets.PropertyImages);
        var bindings=new OfflineProfessionPropertyBindings(assets.PropertyConfiguration,resources,language,
            (owner,path,name,desc,x)=>throw new InvalidOperationException("Unexpected entry popup"),
            (owner,rank,desc,effect,disabled,season)=>throw new InvalidOperationException("Unexpected blessing popup"));
        var previous=ProfessionPropertyBindings.Current;ProfessionPropertyBindings.Current=bindings;
        var hud=Zjcs.Offline.HomeHudUI.OfflineHomeHudAssets.LoadDefault();fonts=hud.CreateFontService();fonts.Initialize();
        var native=JObject.Parse(File.ReadAllText(Path.Combine(Work,"native.json")));
        var audio=ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
        audio.Events=((JObject)native["audio"]).Properties().Select(p=>new OfflineProfessionAudioCatalog.Entry{
            EventId=(uint)p.Value["event"]["id"],Clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath((string)p.Value["pointer"]["guid"]))}).ToArray();
        Require(audio.Events.All(row=>row.Clip&&row.Clip.samples>0),"All original member-click Wwise events have decoded PCM.");
        var sound=new GameObject("Owned original suit audio").AddComponent<OfflineProfessionSoundHost>();sound.Bind(audio);
        var creation=Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.OpenDefault(Path.Combine(Work,"isolated-saves"));
        string id=creation.CreateSlot();var draft=creation.OpenDraft(id);draft.name="套装显示";
        Require(creation.TryCreateCharacter(id,draft,out string error)&&creation.TrySetInitialWeapon(id,
            Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.SwordInitialWeaponType,out error),"Create isolated actual profile: "+error);
        var profiles=new Zjcs.Offline.State.ProfileSlotCatalog(Path.Combine(Work,"isolated-saves"));
        var profile=OfflineMainlineProgressSession.OpenCharacter(profiles,id);
        var slots=new OfflineTreasureSlotService(profile,OfflineTreasureSlotCatalog.LoadDefault());
        var player=new OfflineTreasurePropertyPlayer(profile,slots,_=>throw new InvalidOperationException("Suit collection does not query effect tasks."));
        var cultivation=new OfflineTreasureService(profile,OfflineTreasureCatalog.Load(),_=>throw new InvalidOperationException("Suit navigation only reads the shared piece mapping."));
        var items=new Items{Session=profile};string file=Path.Combine(profiles.GetSlotDirectory(id),"city-save.json");
        camera=new GameObject("Original suit render camera",typeof(AudioListener)).AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=731.5f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,1);
        target=new RenderTexture(675,1463,24);target.Create();camera.targetTexture=target;
        var canvas=new GameObject("Original suit test canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,2340);
        var layoutData=JObject.Parse(File.ReadAllText(Path.Combine(Work,"layout.json")));
        var parent=new GameObject("Original enclosing detail slot",typeof(RectTransform)).GetComponent<RectTransform>();parent.SetParent(canvas.transform,false);
        parent.anchorMin=Vector2.zero;parent.anchorMax=Vector2.one;parent.offsetMin=parent.offsetMax=Vector2.zero;
        var layout=(bool)layoutData["custom"]?(Component)parent.gameObject.AddComponent<UGUIJszx.VerticalLayoutGroupJSZX>():parent.gameObject.AddComponent<VerticalLayoutGroup>();
        JsonUtility.FromJsonOverwrite(layoutData["layout"].ToString(),layout);
        events=new GameObject("Owned synthetic suit pointer events").AddComponent<EventSystem>();
        var popups=new List<(int Id,bool Flag,bool Piece)>();
        Func<OfflineTreasureSuitView> create=()=>new OfflineTreasureSuitView(parent,assets,tables,cultivation,player,items,bindings,hud.defaultFont,assets.Text,sound,
            (treasure,operation)=>popups.Add((treasure,operation,false)),(piece,buttons)=>popups.Add((piece,buttons,true)));
        var view=create();
        foreach(var node in view.Root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)==0,"No missing original suit script: "+node.name);
        var selection=tables.Suits.First(value=>value.Treasures.Count>1);
        for(int state=0;state<5;state++)
        {
            int stage=state;
            Require(profile.ApplyInventoryTransaction("suit-state-"+state,next=>{
                next.Player.Inventory.RemoveAll(row=>selection.Treasures.Contains(row.ItemId));
                next.Player.CharacterProperties.Treasures.RemoveAll(row=>selection.Treasures.Contains(row.ClassId));
                foreach(int treasure in selection.Treasures)
                {
                    next.Player.Inventory.Add(new OfflineItemStack{ItemId=treasure,Count=1});
                    next.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=treasure,Active=stage!=0,
                        Star=stage==4?tables.GetTreasure(treasure).StarMax:stage==3?3:stage==2?2:0});
                }
            }).IsApplied,"Prepare real saved collection/star state.");
            byte[] unchanged=File.ReadAllBytes(file);view.Show(selection.Treasures[0],true);yield return null;yield return null;
            CheckRows(view,selection,player,tables,assets);
            Capture("0"+state+"-collection");
            Require(unchanged.SequenceEqual(File.ReadAllBytes(file)),"Native suit rendering leaves player save byte-identical.");
        }
        // Every configured suit uses the same original fixed property rows and dynamic member template.
        foreach(var suit in tables.Suits)
        {
            view.Show(suit.Treasures[0],true);yield return null;
            CheckRows(view,suit,player,tables,assets);
        }
        int first=selection.Treasures[0],other=selection.Treasures[1];
        Require(profile.ApplyInventoryTransaction("suit-popup-unowned",next=>{
            next.Player.Inventory.RemoveAll(row=>row.ItemId==other);next.Player.CharacterProperties.Treasures.RemoveAll(row=>row.ClassId==other);
        }).IsApplied,"Remove other member through actual isolated save transaction.");
        byte[] beforeClicks=File.ReadAllBytes(file);
        foreach(bool operation in new[]{true,false})
        {
            view.Show(first,operation);yield return null;yield return null;
            var members=view.Content.mySuitItemNameRoot.GetComponentsInChildren<TreasureDetailSuitItem>();
            popups.Clear();Click(members[0].Button);Require(popups.Count==0,"Current member has no recursive detail callback.");
            Click(members[1].Button);Require(popups.Count==1&&popups[0]==(operation?tables.GetTreasure(other).TreasurePieceId:other,operation,operation),
                "Unowned member follows original operation-enabled piece / read-only detail branch.");
        }
        Require(beforeClicks.SequenceEqual(File.ReadAllBytes(file)),"Original member-navigation callbacks never write player saves.");
        Require(profile.ApplyInventoryTransaction("suit-popup-owned",next=>next.Player.Inventory.Add(new OfflineItemStack{ItemId=other,Count=1})).IsApplied,"Own inactive member without activating it.");
        view.Show(first,true);yield return null;yield return null;popups.Clear();
        Click(view.Content.mySuitItemNameRoot.GetComponentsInChildren<TreasureDetailSuitItem>()[1].Button);
        Require(popups.Count==1&&popups[0]==(other,true,false),"Owned inactive member opens operable detail, independent of activated color.");
        byte[] finalSave=File.ReadAllBytes(file);view.Dispose();yield return null;
        var cold=OfflineMainlineProgressSession.OpenCharacter(profiles,id);
        Require(cold.Save.Player.Inventory.Any(row=>row.ItemId==other)&&finalSave.SequenceEqual(File.ReadAllBytes(file)),"Cold reopen restores actual suit inventory without view writes.");
        profile=cold;items.Session=cold;slots=new OfflineTreasureSlotService(cold,OfflineTreasureSlotCatalog.LoadDefault());
        player=new OfflineTreasurePropertyPlayer(cold,slots,_=>throw new InvalidOperationException("Suit collection never queries effect tasks."));
        cultivation=new OfflineTreasureService(cold,OfflineTreasureCatalog.Load(),_=>throw new InvalidOperationException("Suit navigation only reads the shared piece mapping."));
        view=create();view.Show(first,true);yield return null;yield return null;CheckRows(view,selection,player,tables,assets);Capture("06-reopened");
        view.Show(-1,true);Require(!view.Root.activeSelf,"A treasure without a suit hides the original suit root.");
        view.Dispose();yield return null;ProfessionPropertyBindings.Current=previous;
    }
    private static void CheckRows(OfflineTreasureSuitView view,TreasureSuitInfo suit,ITreasurePropertyPlayer player,ITreasurePropertyTables tables,OfflineTreasureSuitAssets assets)
    {
        var stars=new Dictionary<int,int>();TreasureSuitQueries.GetSuitStartInfo(suit,player,stars);
        Require(view.Content.mySuitCount.Text==string.Format(assets.Text("treasureDialog_suit_count"),assets.Text("item_"+suit.ClassId+"_name"),stars.Count,suit.Treasures.Count),"Original localized suit name and activated count.");
        var members=view.Content.mySuitItemNameRoot.GetComponentsInChildren<TreasureDetailSuitItem>();
        Require(members.Length==suit.Treasures.Count,"Original member refresh removes prior active clones.");
        for(int i=0;i<members.Length;i++)
        {
            Require(Field<WorldStateSwitcher<bool>>(members[i],"mActiveStateSwitcher").State==stars.ContainsKey(suit.Treasures[i]),"Original member collected state follows activated parameters.");
            foreach(var text in Field<List<WorldMultiLanguageText>>(members[i],"mTitles"))Require(text.Text==assets.Text("item_"+suit.Treasures[i]+"_name"),"Exact source member name and order.");
        }
        var enabled=suit.GetActivedCondition(stars).ToList();int index=0;
        foreach(var entry in suit.PropData)foreach(int prop in entry.Value.PropTypes)
        {
            var row=view.Content.mySuitPropItems[index++];var value=row.GetComponent<ProfessionPlayerPropItem>();
            var condition=Field<WorldMultiLanguageText>(row,"mConditionText");
            Color color=Field<Color>(row,enabled.Contains(entry.Key)?"ActivedColor":"UnActivedColor");
            Require(condition.Text==assets.Text("TreasureSuitPropCondition."+entry.Key)&&condition.Color==color&&value.myPropTypeText.Color==color&&value.myPropValueText.Color==color,"Exact original condition label and all property colors.");
            foreach(var image in Field<List<Image>>(row,"mImages"))Require(image.color==color,"Original property image follows active color.");
            Require(!value.myIcon&&value.myPropTypeText.Text==assets.Text("PropType."+(ProfessionPropertyType)prop)&&!string.IsNullOrEmpty(value.myPropValueText.Text),"Original text-only property row preserves its absent icon, localized type and formatted value.");
        }
        Require(view.Content.mySuitPropItems.Count(row=>row.gameObject.activeSelf)==index,"Only configured original suit bonuses are shown.");
    }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=old;
        Require(texture.GetRawTextureData().Where((v,i)=>i%4<3).Distinct().Count()>8,"Actual native suit text and artwork render.");
        File.WriteAllBytes(Path.Combine(Work,name+".png"),texture.EncodeToPNG());Object.Destroy(texture);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);Application.logMessageReceived-=Log;fonts?.Dispose();
        if(error==null){File.WriteAllText(Path.Combine(Work,"result.json"),"{\"success\":true,\"checks\":"+checks+"}");Debug.Log("TREASURE_SUITS_NATIVE_PASSED: "+checks);}
        else Debug.LogException(error);EditorApplication.Exit(error==null?0:1);
    }
}
