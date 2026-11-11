using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomePlayerInfoNativeValidation
{
    const string Key="Zjcs.HomePlayerInfoNative.Active";
    static HomePlayerInfoNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("OriginalPlayerInfoFixture").AddComponent<HomePlayerInfoNativeRunner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Original player-info validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error==null)Debug.Log("HOME_PLAYER_INFO_NATIVE_PASSED: native text subtrees, original number formats, player observers, hide/reopen and sized-level early return. Enclosing MainCanvas, HP, blessing, status and Windows Player are outside this fixture.");
        else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomePlayerInfoNativeRunner:MonoBehaviour
{
    int errors;Camera cameraUi;RenderTexture target;string work;
    IEnumerator Start()
    {
        AudioListener.volume=0;Application.runInBackground=true;
        work=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;};
        var steps=Check();
        for(;;){object current=null;bool next;
            try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){HomePlayerInfoNativeValidation.Finish(error);yield break;}
            if(!next)break;yield return current;
        }
        HomePlayerInfoNativeValidation.Finish(errors==0?null:new Exception("Unexpected player-info errors: "+errors));
    }
    IEnumerator Check()
    {
        var assets=OfficialHomePlayerInfoAssetCatalog.LoadDefault();
        var textData=JsonUtility.FromJson<TextRows>(assets.LocalizedTexts.text);
        var bindings=new Bindings(textData.texts.ToDictionary(row=>row.key,row=>row.text));
        LanguageManager.Initialize(bindings);
        var fonts=assets.CreateFontService();fonts.Initialize();
        var context=JsonUtility.FromJson<Context>(File.ReadAllText(Path.Combine(Application.dataPath,"HomePlayerInfoValidation/parent-context.json")));
        cameraUi=new GameObject("PlayerInfoCamera",typeof(Camera)).GetComponent<Camera>();
        cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);cameraUi.orthographic=true;
        target=new RenderTexture(608,1080,24);cameraUi.targetTexture=target;
        var root=new GameObject("IsolatedOriginalPlayerInfoCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var parentMap=new Dictionary<long,RectTransform>();
        var widgets=new List<GameObject>();
        foreach(var entry in context.entries)
        {
            // Diagnostic framing ends at the original PublicPlayerInfo rect.
            // This does not stand in for the enclosing MainCanvas lifecycle.
            int top=Array.FindIndex(entry.chain,node=>node.name=="PublicPlayerInfo");
            Require(top>0,"source includes PublicPlayerInfo ancestor");
            Transform parent=root.transform;
            for(int index=top;index>=1;index--)
            {
                var node=entry.chain[index];
                if(!parentMap.TryGetValue(node.native_file_id,out var rect))
                {
                    rect=new GameObject(node.name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
                    var data=node.rect;rect.anchorMin=data.m_AnchorMin;rect.anchorMax=data.m_AnchorMax;rect.pivot=data.m_Pivot;
                    rect.sizeDelta=data.m_SizeDelta;rect.anchoredPosition=data.m_AnchoredPosition;
                    rect.localScale=data.m_LocalScale;rect.localRotation=data.m_LocalRotation;
                    parentMap.Add(node.native_file_id,rect);
                }
                parent=rect;
            }
            var prefab=entry.name=="HomeNameText"?assets.NamePrefab:entry.name=="HomeLevelText"?assets.LevelPrefab:assets.CombatRatingPrefab;
            var gate=new GameObject("Inactive original binding");gate.SetActive(false);
            var widget=Object.Instantiate(prefab,gate.transform,false);
            foreach(var text in widget.GetComponentsInChildren<TMP_Text>(true))fonts.SetupFont(text.font);
            foreach(var label in widget.GetComponentsInChildren<WorldMultiLanguageText>(true))label.Localizer=bindings.Text;
            foreach(var controller in widget.GetComponentsInChildren<HomePlayerInfoUIBase>(true))controller.Configure(bindings);
            widget.transform.SetParent(parent,false);Object.Destroy(gate);widgets.Add(widget);
            Require(widget.GetComponentsInChildren<MonoBehaviour>(true).All(component=>component),"all original native scripts are bound");
            Require(widget.GetComponent<TMP_Text>().font,"selected native font binding retained");
        }
        yield return null;yield return null;
        var name=widgets.SelectMany(widget=>widget.GetComponentsInChildren<HomePlayerNameText>(true)).Single();
        var level=widgets.SelectMany(widget=>widget.GetComponentsInChildren<HomePlayerLevelText>(true)).Single();
        var combat=widgets.SelectMany(widget=>widget.GetComponentsInChildren<HomePlayerCombatRatingText>(true)).Single();
        Require(name.myText.Text=="家园玩家","original name observer uses raw {0} key");
        Require(combat.Text.Text.Contains("<size=42>1.23</size>万"),"original sized combat number keeps native size and Chinese ten-thousand unit: "+combat.Text.Text+"; active="+combat.gameObject.activeInHierarchy+"; sized="+new HomeNumberWithSizeArg(12345,42));
        Require(bindings.Player.Subscribers==5,"name, level/subrank/season and combat events subscribe once");
        string firstLevel=level.LevelText.Text;
        Require(!string.IsNullOrWhiteSpace(firstLevel),"selected level localization formats at initialization");
        Capture("home-player-info-native.png",widgets);
        bindings.Player.NameValue="刷新后的名字";bindings.Player.Power=123456789;bindings.Player.LevelValue=27;
        bindings.Player.RaiseName();bindings.Player.RaiseLevel();bindings.Player.RaiseCombat();
        yield return null;
        Require(name.myText.Text=="刷新后的名字" && level.LevelText.Text!=firstLevel,"actual observer events refresh native name/level fields");
        Require(combat.Text.Text.Contains("<size=42>1.23</size>亿"),"combat observer changes unit at the original hundred-million boundary");
        bindings.Player.Store=true;bindings.Player.RaiseSubRank();
        var state=(WorldStateSwitcher<bool>)Field(level,"mMaxLevelStateSwitcher");
        Require(state.State,"subrank observer recomputes the store-experience state");
        bindings.Player.Store=false;bindings.Player.RaiseSeason();Require(!state.State,"season end observer recomputes store-experience state");
        typeof(HomePlayerLevelText).GetField("mNumberSize",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(level,38);
        int reads=bindings.Player.StoreReads;bindings.Player.Store=true;bindings.Player.RaiseLevel();
        Require(bindings.Player.StoreReads==reads && level.LevelText.Text.Contains("<size=38>27</size>"),"original sized-level early return skips the store-experience branch");
        foreach(var widget in widgets)widget.SetActive(false);
        Require(bindings.Player.Subscribers==0,"deactivation unsubscribes every player observer");
        bindings.Player.NameValue="重新打开";bindings.Player.RaiseName();
        Require(name.myText.Text=="刷新后的名字","hidden native widget receives no player callback");
        foreach(var widget in widgets)widget.SetActive(true);yield return null;
        Require(bindings.Player.Subscribers==5 && name.myText.Text=="重新打开","reactivation refreshes current data without duplicate listeners");
        bindings.OnPlayerUpdate.Invoke();Require(bindings.Player.Subscribers==5,"original PlayerUIBase reinitialization does not duplicate current-player observers");
        Capture("home-player-info-updated.png",widgets);
        foreach(var widget in widgets)Object.Destroy(widget);yield return null;
        Require(bindings.Player.Subscribers==0,"destroy releases all original player subscriptions");
        bindings.OnPlayerUpdate.Invoke();
        Require(new HomeNumberWithSizeArg(9999,42).ToString()=="<size=42>9999</size>","native number-unit boundary below ten thousand");
        Require(new HomeNumberWithSizeArg(10000,42).ToString()=="<size=42>1</size>万","native number-unit boundary at ten thousand");
        Require(new HomeNumberWithSizeArg(100000000,42).ToString()=="<size=42>1</size>亿","native number-unit boundary at hundred million");
        fonts.Dispose();
    }
    static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    void Capture(string name,List<GameObject> widgets)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();
        foreach(var widget in widgets)
        {
            var text=widget.GetComponent<TMP_Text>();text.ForceMeshUpdate();
            Require(!text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(character=>character.character=='\u25a1'),"native source font supplies the actual Chinese glyphs, without square fallback");
            Require(text.textInfo.characterCount>0 && text.textInfo.meshInfo.Any(mesh=>mesh.vertexCount>0),"native text emits actual glyph geometry");
        }
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        Require(image.GetPixels32().Count(pixel=>pixel.r>100 && pixel.g>100 && pixel.b>100)>50,"actual original text is visible in rendered pixels");
        File.WriteAllBytes(Path.Combine(work,name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    [Serializable]sealed class TextRows{public TextRow[] texts;}[Serializable]sealed class TextRow{public string key,text;}
    [Serializable]sealed class Context{public Chain[] entries;}[Serializable]sealed class Chain{public string name;public Node[] chain;}
    [Serializable]sealed class Node{public long native_file_id;public string name;public NativeRect rect;}
    [Serializable]sealed class NativeRect{public Vector2 m_AnchorMin,m_AnchorMax,m_Pivot,m_SizeDelta,m_AnchoredPosition;public Vector3 m_LocalScale;public Quaternion m_LocalRotation;}
    sealed class Bindings:IHomePlayerInfoBindings,ILocalizationTextService
    {
        readonly Dictionary<string,string> texts;public readonly PlayerData Player=new PlayerData();
        public Bindings(Dictionary<string,string> texts){this.texts=texts;}
        public IHomePlayerInfoPlayer MainPlayer=>Player;IHomeExperiencePlayer IHomeExperienceServices.MainPlayer=>Player;
        public bool ManagerAvailable=>true;public SafeAction OnPlayerUpdate{get;}=new SafeAction();
        public IHomeLevelExpParser LevelExpParser=>throw new Exception("text controller requested experience parser");
        public IHomeLevelBlessParser LevelBlessInfoParser=>throw new Exception("text controller requested blessing parser");
        public IHomeExperienceRewards Rewards=>throw new Exception("text controller requested reward display");
        public int PlayerExpItemId=>4;public int PlanesRank=>1;public void ResolveSubRank(int value)=>throw new Exception("unexpected parser request");
        public object Compact(long value)=>CultureNumberArg.Compact(value);public string GetSeasonNameByRank(int value)=>throw new Exception("unexpected season name request");
        public bool TMPEffectFontSizeIssueFixed=>TMPEffect.FixFontSizeIssue;public Vector3 PointerPosition=>Vector3.zero;
        public string Text(string key)=>texts.TryGetValue(key,out var value)?value:key;
        public void PopupIntroduction(UIBase owner,HomeInfoTextKey title,IReadOnlyList<string> content,Vector3 position)=>throw new Exception("native text subtrees have no introduction popup reference");
        public LanguageType CurrentLanguage=>LanguageType.zh_CN;public void ReloadData(){}public bool ContainsKey(string key)=>texts.ContainsKey(key);
        public bool TryGetOriginalText(string key,LanguageType language,out string result)=>texts.TryGetValue(key,out result);
        public string GetText(string key,LanguageType language,params object[] args)=>args.Length==0?Text(key):string.Format(Text(key),args);
        public string GetTextFormated(string key,LanguageType language,params object[] args)=>GetText(key,language,args);
        public void SetReplacer(ITextReplacer replacer){}public bool TryGetText(string key,LanguageType language,out string result)=>TryGetOriginalText(key,language,out result);
        public bool TryGetText(string key,out string result)=>texts.TryGetValue(key,out result);public void AddDataProvider(ILanguageTextDataProvider provider){}
    }
    sealed class PlayerData:IHomePlayerInfoPlayer
    {
        public string NameValue="家园玩家";public long Power=12345;public int LevelValue=1;public bool Store;public int StoreReads;
        public string Name=>NameValue;public long CombatRating=>Power;public int LevelLimit=>520;public int Rank=>1;public int BaseLevel=>LevelValue;public int Level=>LevelValue;public int SubRank=>1;
        Action name;Action<int> level,subrank;Action<long,long> combat;Action<object> season;
        public int Subscribers=>(name?.GetInvocationList().Length??0)+(level?.GetInvocationList().Length??0)+(subrank?.GetInvocationList().Length??0)+(combat?.GetInvocationList().Length??0)+(season?.GetInvocationList().Length??0);
        public event Action OnNameUpdate{add{name+=value;}remove{name-=value;}}public event Action<int> OnLevelUpdate{add{level+=value;}remove{level-=value;}}
        public event Action<long,long> OnCombatRatingChanged{add{combat+=value;}remove{combat-=value;}}
        public event Action<int> OnSubRankUpdate{add{subrank+=value;}remove{subrank-=value;}}public event Action<object> OnSeasonRecordDataEndUpdate{add{season+=value;}remove{season-=value;}}
        public event Action<HomeExperienceItem,long,object> OnItemCountUpdate{add{throw new Exception("unexpected item observer");}remove{}}
        public event Action OnBlessLevelDataUpdate{add{throw new Exception("unexpected blessing observer");}remove{}}
        public event Action<object> OnSeasonRecordDataAdd{add{throw new Exception("unexpected season-add observer");}remove{}}
        public void RaiseName()=>name?.Invoke();public void RaiseLevel()=>level?.Invoke(LevelValue);public void RaiseCombat()=>combat?.Invoke(0,Power);
        public void RaiseSubRank()=>subrank?.Invoke(1);public void RaiseSeason()=>season?.Invoke(null);
        public bool ShowPlayerStoreExpLimit(){StoreReads++;return Store;}
        public bool IsBlessLevelOpen()=>false;public int GetMaxStoreExpLevel()=>520;public bool IsMaxBlessLevel()=>false;
        public long GetCurBlessPlayerExp()=>0;public int GetCurBlessPlayerLevel()=>0;public long GetItemAmount(int value)=>0;
    }
}
