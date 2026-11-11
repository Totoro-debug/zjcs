using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TMPro;
using System.Reflection;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomePlayerPanelNativeValidation
{
    const string Key="Zjcs.HomePlayerPanelNative.Active";
    static HomePlayerPanelNativeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))
                new GameObject("OriginalHomeAvatarFixture").AddComponent<HomePlayerPanelNativeRunner>();
        };
        EditorApplication.update+=()=>{
            if(SessionState.GetBool(Key,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",float.MaxValue))
                Finish(new TimeoutException("Original home avatar validation timed out."));
        };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if(!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key,false);
        if(error==null)Debug.Log("HOME_PLAYER_PANEL_NATIVE_PASSED: full original panel, real selected-player portrait, HP/blessing/status events, source countdown, internal pointer and hide/reopen lifecycle.");
        else Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}

public sealed class HomePlayerPanelNativeRunner:MonoBehaviour
{
    Camera cameraUi;RenderTexture target;EventSystem events;
    OfflineHomePlayerPanel avatar;OfflineProfessionPortraitSession portraitSession;
    int errors,opened;string work;
    IEnumerator Start()
    {
        AudioListener.volume=0;work=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;};
        var steps=Check();
        for(;;){object current=null;bool next;
            try{next=steps.MoveNext();if(next)current=steps.Current;}
            catch(Exception error){avatar?.Dispose();portraitSession?.Dispose();HomePlayerPanelNativeValidation.Finish(error);yield break;}
            if(!next)break;yield return current;
        }
        HomePlayerPanelNativeValidation.Finish(errors==0?null:new Exception("Unexpected avatar errors: "+errors));
    }
    IEnumerator Check()
    {
        string saveRoot=Path.Combine(work,"avatar-saves-"+Guid.NewGuid().ToString("N"));
        var creation=CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="家园头像";
        Require(creation.TryCreateCharacter(slot,draft,out string error),error);
        Require(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),error);
        var profile=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot),slot);
        profile.EnsureLocalActorIdentities();
        var growth=new OfflineCharacterProgressionService(profile,OfficialCharacterAssetCatalog.LoadDefault().Load());
        var resources=ProfessionNativePortraitStudioResources.LoadDefault();
        var weapons=Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
        var rules=Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        var tables=ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        var defaults=new ProfessionAppearanceSkinDefaults(tables,Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
            message=>{throw new Exception(message);});
        var pipeline=UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        using var textures=new OfflineProfessionSceneTextureResources(resources.Characters);
        var lowMemory=new GAction();
        using var manager=new AppearanceAssetManager(weapons.Textures.ManagerConfig,lowMemory.Subscribe);
        var weaponMaterials=new OfflineProfessionWeaponTextureResources(resources.AppearanceConfig,manager,weapons.Textures);
        var previous=ProfessionAppearanceTextureBindings.Current;
        ProfessionAppearanceTextureBindings.Current=new OfflineProfessionBattleSceneTextures(resources.Characters,textures,weapons.Textures,weaponMaterials);
        var parts=new Parts(resources.AppearanceConfig,tables.SpineSkinInfoParser);
        using var pool=new CharacterPartEntryProvider(new OfflineProfessionScenePartPoolBindings(weapons,weaponMaterials,parts,()=>true));
        var appearance=new OfflineProfessionAppearanceBindings(profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,
            ()=>growth.Player,id=>growth.Catalog.ItemType(id),tables,resources.AppearanceConfig,rules,defaults);
        var images=ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();images.Entries=Array.Empty<OfflineProfessionImageCatalog.Entry>();
        using var actors=new OfflinePartnerActorUiRuntime(profile,growth,OfficialPartnerAssetCatalog.LoadDefault(),
            new OfflineProfessionImageResourceLoader(images),key=>key,_=>{throw new Exception("Home avatar requested NPC popup");},()=>opened++ ,parts);
        // Actor bindings install their own texture scope. The real photographer
        // needs the original body/dye/weapon resource closure used above.
        ProfessionAppearanceTextureBindings.Current=new OfflineProfessionBattleSceneTextures(resources.Characters,textures,weapons.Textures,weaponMaterials);
        portraitSession=new OfflineProfessionPortraitSession(Path.Combine(work,"avatar-portrait-cache"),resources,appearance,tables,manager,pool,parts,lowMemory);
        try
        {
            cameraUi=new GameObject("AvatarCamera",typeof(Camera)).GetComponent<Camera>();
            cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);cameraUi.orthographic=true;
            target=new RenderTexture(608,1080,24);cameraUi.targetTexture=target;
            var root=new GameObject("NativeAvatarCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            events=new GameObject("InternalAvatarPointer",typeof(EventSystem)).GetComponent<EventSystem>();
            var assets=Resources.Load<OfficialHomePlayerPanelAssetCatalog>("Home/OfficialHomePlayerPanelAssetCatalog");
            var textAssets=OfficialHomePlayerInfoAssetCatalog.LoadDefault();
            var experience=new Experience(assets,textAssets,profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId);
            LanguageManager.Initialize(experience);
            ProfessionPropertyBindings.Current=experience;
            avatar=new OfflineHomePlayerPanel(canvas.transform,actors.TeamIcons,portraitSession.Manager,experience,experience);
            Require(avatar.Root.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c),"all native original panel scripts resolved");
            Require(avatar.Root.GetComponentsInChildren<Transform>(true).Count()==22,"complete 22-GameObject original panel retained");
            for(int frame=0;frame<90;frame++){portraitSession.Update();yield return null;}
            var raw=avatar.Root.GetComponentInChildren<RawImage>(true);
            Require(avatar.Icon.myIconMe.InitFinished && raw.texture is Texture2D,"original AutoInit binds actual selected player and captures a head texture");
            var texture=(Texture2D)raw.texture;
            Require(texture.width==100 && texture.height==120,"original Head capture dimensions");
            Require(raw.uvRect==new Rect(0,0,1,1) && raw.rectTransform.rect.size==new Vector2(100,120),"native Rect v2 preserves the full UV area and original SetNativeSize result");
            Canvas.ForceUpdateCanvases();var headMesh=raw.canvasRenderer.GetMesh();
            Require(headMesh.vertexCount>0 && headMesh.uv.All(uv=>!float.IsNaN(uv.x) && !float.IsNaN(uv.y)
                && !float.IsInfinity(uv.x) && !float.IsInfinity(uv.y)),"original capsule produces finite textured vertices");
            Require(texture.GetPixels32().Count(pixel=>pixel.a>0)>500 && texture.GetPixels32().Any(pixel=>pixel.a==0),"actual player head with transparent margins");
            File.WriteAllBytes(Path.Combine(work,"home-avatar-head.png"),texture.EncodeToPNG());
            Require(avatar.Icon.myIconMe.InfoUI.PlayerInfoData.Id==profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,"persisted player identity reaches original portrait controller");
            var hp=avatar.Root.GetComponentInChildren<MainPlayerHpImage>(true);
            var slider=(Slider)Field(hp,"mSlider");
            var red=avatar.Root.GetComponent<PlayerHpRedPoint>();
            var status=avatar.Root.GetComponentInChildren<PlayerBuffContentUI>(true);
            var statusButton=(Button)Field(status,"mClickBtn");
            Require(Math.Abs(slider.normalizedValue-1)<.001f,"original HP initializes full");
            Require(!red.IsShow,"full HP suppresses original cure prompt");
            Require(!status.gameObject.activeInHierarchy && experience.Player.Subscribers==18 && experience.Display!=null,"empty original status group stays inactive; other observers register once: "+experience.Player.Subscribers);
            Click(avatar.Icon.GetComponent<Button>());Require(opened==1,"original embedded avatar pointer opens actual character");
            Require(AudioListener.volume==0 && Object.FindObjectsOfType<AudioSource>().Any(source=>source.isPlaying && assets.sounds.Any(row=>row.clip==source.clip)),"native pointer emits and starts the packaged original click sound while test output stays muted");
            experience.Player.Hp=.2f;experience.Player.RaiseHp();yield return null;
            Require(Math.Abs(slider.normalizedValue-.2f)<.001f && red.IsShow,"original HP and recovery-food prompt react together");
            experience.Player.Cure=false;experience.Player.RaiseFood();yield return null;
            Require(!red.IsShow,"removing last cure food hides prompt");
            experience.Player.Cure=true;experience.Player.RaiseFood();Require(red.IsShow,"cure-food item observer refreshes prompt");
            experience.Player.Hp=.7f;yield return null;yield return null;
            Require(Math.Abs(slider.normalizedValue-.7f)<.001f,"official no-map polling follows HP recovery");
            var level=avatar.Root.GetComponentInChildren<HomePlayerLevelText>(true);
            var combat=avatar.Root.GetComponentInChildren<HomePlayerCombatRatingText>(true);
            var bless=avatar.Root.GetComponent<PlayerBlessLevelViewController>();
            Require(!level.LevelText.GetComponent<TMP_Text>().enableVertexGradient,"normal native level has no blessing gradient");
            experience.Player.Bless=true;experience.Player.Power=20000;experience.Player.RaiseBless();yield return null;
            Require(level.LevelText.GetComponent<TMP_Text>().enableVertexGradient,"official blessing reset enables original level gradient");
            Require(combat.Text.GetComponent<TMP_Text>().enableVertexGradient,"positive blessing power enables original combat gradient");
            Click((Button)Field(avatar.Root.GetComponentInChildren<ProfessionBlessPropBtn>(true),"mOpenBtn"));Require(experience.BlessPopups==1,"original blessing combat button emits current power description");
            experience.Player.Statuses.Add(1,new StatusData(StatusEffectIconType.Normal,EStatusSourceType.Cooking,experience.Now,0,3));
            experience.Player.Statuses.Add(2,new StatusData(StatusEffectIconType.Duration,EStatusSourceType.Map,experience.Now.AddSeconds(-2),10,0));
            experience.Player.Statuses.Add(3,new StatusData(StatusEffectIconType.Normal,EStatusSourceType.Battle,experience.Now,0,2));
            experience.Player.RaiseStatus();yield return null;yield return null;
            var items=avatar.Root.GetComponentsInChildren<BuffItemUI>().Where(item=>item.gameObject.activeInHierarchy).ToArray();
            Require(items.Length==2,"original status source filter excludes battle status");
            Require(status.gameObject.activeInHierarchy && experience.Player.Subscribers==19,"showing original status group starts its provider subscription");
            Require(items.All(item=>!((Button)Field(item,"mBuffBtn")).enabled),"pooled status icons defer pointer to original group");
            var duration=(BuffItemDurationDecorator)Field(items.Single(item=>item.StatusEffectData.Info.IconType==StatusEffectIconType.Duration),"mBuffItemDecorator");
            var timeImage=(Image)Field(duration,"mTimeDownImg");
            Require(Math.Abs(timeImage.fillAmount-.8f)<.01f,"source countdown uses LastGainedTime and duration");
            experience.Now=experience.Now.AddSeconds(3);yield return null;yield return null;
            Require(Math.Abs(timeImage.fillAmount-.5f)<.01f,"source countdown follows injected official clock");
            Capture("home-player-panel-native.png");
            Click(statusButton);Require(experience.EntryPopups==1 && experience.LastEntries.Count==2,"native group pointer supplies only displayed status details");
            var endTime=(EntryDetailAdditionEndTimeInfo)experience.LastEntries.Single(entry=>entry.AdditionInfoDic!=null).AdditionInfoDic[EntryDetailAdditionInfoType.EndTime];
            Require(endTime.EndTime==experience.Player.Statuses[2].LastGainedTime.AddSeconds(10),"original details retain exact status end time");
            int normalInstance=items.Single(item=>item.StatusEffectData.Info.IconType==StatusEffectIconType.Normal).GetInstanceID();
            experience.Player.Statuses.Remove(2);experience.Player.RaiseStatus();yield return null;
            Require(avatar.Root.GetComponentsInChildren<BuffItemUI>().Count(item=>item.gameObject.activeInHierarchy)==1,"status removal returns expired icon to its original pool");
            float stopped=timeImage.fillAmount;experience.Now=experience.Now.AddSeconds(1);yield return null;yield return null;
            Require(timeImage.fillAmount==stopped,"returned duration icon cancels its official coroutine");
            Require(avatar.Root.GetComponentsInChildren<BuffItemUI>().Single(item=>item.gameObject.activeInHierarchy).GetInstanceID()==normalInstance,"normal status is reused by original pool");
            var a=new object();var b=new object();avatar.Visibility.Hide(a);avatar.Visibility.Hide(b);
            Require(!avatar.Root.activeSelf && !avatar.Visibility.IsShow && experience.Player.Subscribers==0,"multiple hide references release all original subscriptions");
            avatar.Visibility.TryShow(a);Require(!avatar.Root.activeSelf,"one outstanding hide reference keeps original panel hidden");
            avatar.Visibility.TryShow(b);yield return null;yield return null;
            Require(avatar.Root.activeSelf && avatar.Visibility.IsShow && experience.Player.Subscribers==19,"last reference reopens and subscribes each observer once");
            experience.OnPlayerUpdate.Invoke();Require(experience.Player.Subscribers==19,"original reinit does not duplicate observers");
            experience.Player.Statuses.Clear();experience.Player.RaiseStatus();yield return null;
            Require(!avatar.Root.GetComponentsInChildren<BuffItemUI>().Any(item=>item.gameObject.activeInHierarchy),"clearing statuses returns every icon");
            experience.Player.Bless=false;experience.Player.Power=12345;experience.Player.Hp=1;experience.Player.RaiseBless();experience.Player.RaiseHp();
            yield return null;Capture("home-player-panel-reopened.png");
            avatar.Dispose();avatar=null;yield return null;
            Require(experience.Player.Subscribers==0 && experience.Display==null,"disposal releases complete original panel observers");
            portraitSession.Dispose();portraitSession=null;
        }
        finally{ProfessionAppearanceTextureBindings.Current=previous;}
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(cameraUi,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
        Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"unobstructed native avatar raycast");
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();cameraUi.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        foreach(var text in avatar.Root.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            Require(!text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.character=='\u25a1'),"original source font renders actual characters without missing-glyph squares");
            if(!string.IsNullOrWhiteSpace(text.text))Require(text.textInfo.meshInfo.Any(mesh=>mesh.vertexCount>0),"active original text produces glyph geometry");
        }
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        Require(texture.GetPixels32().Count(pixel=>pixel.r>180 && pixel.g>180 && pixel.b>180)>50,"complete original panel emits visible pixels");
        File.WriteAllBytes(Path.Combine(work,name),texture.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(texture);
    }
    static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Parts:IProfessionAppearancePartBindings
    {
        public AppearanceConfig Config{get;}readonly SpineSkinInfoParser parser;
        public Parts(AppearanceConfig config,SpineSkinInfoParser parser){Config=config;this.parser=parser;}
        public bool HasEffect(string skin)=>parser.HasEffect(skin);
        public IProfessionAppearancePartAsset LoadPrefab(string path)=>throw new NotSupportedException("Default home head requires no effect prefab: "+path);
        public GameObject Instantiate(GameObject prefab,Transform parent,bool world)=>Object.Instantiate(prefab,parent,world);
    }
    [Serializable]sealed class TextRows{public TextRow[] texts;}[Serializable]sealed class TextRow{public string key,text;}
    sealed class Experience:IHomePlayerPanelBindings,IHomeLevelExpParser,IHomeLevelBlessParser,IHomeExperienceRewards,
        ILocalizationTextService,IHomePanelResourceLoader,IProfessionPropertyBindings,IProfessionTipDotSystem
    {
        public readonly PlayerData Player=new PlayerData();public Func<long,long,IEnumerator> Display;
        readonly OfficialHomePlayerPanelAssetCatalog assets;readonly Dictionary<string,string> texts;
        readonly Sprite icon,bg;public DateTime Now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        public int EntryPopups,BlessPopups;public IReadOnlyList<HomeEntryDetailData> LastEntries;
        public Experience(OfficialHomePlayerPanelAssetCatalog assets,OfficialHomePlayerInfoAssetCatalog textAssets,long id)
        {
            this.assets=assets;Player.PlayerId=id;
            texts=JsonUtility.FromJson<TextRows>(assets.localizedTexts.text).texts.ToDictionary(row=>row.key,row=>row.text);
            var prefab=assets.statusPrefabs.Single(row=>row.guid=="6906661e5401e28438d9fb3a60ba1a8f").prefab;
            var status=prefab.GetComponentInChildren<FightStatusUIItem>(true);icon=status.IconImg.sprite;bg=status.BgImg.sprite;
            Require(icon && bg,"original status prefab icon and background dependencies resolve");
        }
        public IHomePanelPlayer MainPlayer=>Player;
        IHomePlayerInfoPlayer IHomePlayerInfoBindings.MainPlayer=>Player;
        IHomeExperiencePlayer IHomeExperienceServices.MainPlayer=>Player;
        public bool ManagerAvailable=>true;public SafeAction OnPlayerUpdate{get;}=new SafeAction();
        public IHomeLevelExpParser LevelExpParser=>this;public IHomeLevelBlessParser LevelBlessInfoParser=>this;
        public IHomeExperienceRewards Rewards=>this;public int PlayerExpItemId=>4;public int PlanesRank=>1;
        public void ResolveSubRank(int value){}public object Compact(long value)=>CultureNumberArg.Compact(value);
        public string GetSeasonNameByRank(int rank)=>rank.ToString();
        public long HandledExp=>0;public long PlayerRealExp=>0;
        public void RegisterRewardExpDisplay(Func<long,long,IEnumerator> display){Require(Display==null,"unique reward display");Display=display;}
        public void UnregisterRewardExpDisplay(Func<long,long,IEnumerator> display){Require(Display==display,"matching reward removal");Display=null;}
        readonly long[] levels={0,0,100,300,600,1000};
        public long GetExp(int id,int level)=>level<=0?0:level>=levels.Length?long.MaxValue:levels[level];
        public int CalcLevelByExp(int id,long value)=>Enumerable.Range(0,levels.Length).Last(index=>levels[index]<=value);
        public long GetLevelUpExp(int id,int level)=>GetExp(id,level+1)-GetExp(id,level);
        public long GetLevelUpExp(int id,int from,int to)=>GetExp(id,to)-GetExp(id,from);
        public int GetBlessLevel(int rank,long value)=>value<200?0:value<500?1:2;
        public HomeLevelBlessInfo Get(int rank,int level)=>new HomeLevelBlessInfo{Exp=GetExpRequestByBlessLevel(level)};
        public long GetExpRequestByBlessLevel(int level)=>level<=0?0:level==1?200:500;
        public bool TMPEffectFontSizeIssueFixed=>TMPEffect.FixFontSizeIssue;public Vector3 PointerPosition=>Vector3.zero;
        public string Text(string key)=>texts.TryGetValue(key,out var value)?value:key;
        public string Text(string key,params object[] args)=>TextFormatted(key,args);
        public string TextFormatted(string key,params object[] args)=>string.Format(Text(key),args);
        public void PopupIntroduction(UIBase owner,HomeInfoTextKey title,IReadOnlyList<string> content,Vector3 position)=>throw new Exception("original native introduction reference is null");
        public IProfessionTipDotSystem TipDotSystem=>this;public float PlayerHpRedDotThreshold=>.5f;
        public IHomePanelResourceLoader Resources=>this;public DateTime GetDateTime()=>Now;
        public HomePanelItemInfo GetItemInfo(int id)=>new HomePanelItemInfo{ItemType=HomePanelItemType.Cooking,PartInfo=new HomePanelCookingInfo{Type=CookingType.Recovery}};
        // Controlled status payloads exercise rendering and filtering, not profile status rules.
        public HomePanelEntryInfo GetEntry(int id)=>new HomePanelEntryInfo{Icon="ItemIcon/Props/Abnormal_Poisoned",Bg="ItemIcon/Props/BG_Buff"};
        public string GetSeasonLevelUnEffectDesc(int rank)=>throw new Exception("null native introduction cannot request season text");
        public string GetEntryNameKey(int id)=>"fixture_status_name";public string GetEntryDescKey(int id)=>"fixture_status_desc";
        public string GetStatusEffectVaildShowScopesName(IHomePanelPlayer player,int classId)=>"fixture_scope";
        public bool TryGetCookingBuffEntryDesc(IHomePanelPlayer player,int entryId,out string desc){desc=null;return false;}
        public void PopupEntries(UIBase owner,IReadOnlyList<HomeEntryDetailData> entries){EntryPopups++;LastEntries=entries.ToArray();}
        public void PopupEntry(UIBase owner,Func<HomeEntryDetailData> entry){EntryPopups++;LastEntries=new[]{entry()};}
        public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T:Object
        {
            Object value=assets.statusPrefabs.FirstOrDefault(row=>row.guid==guid)?.prefab;
            if(!value)value=assets.sprites.FirstOrDefault(row=>row.guid==guid)?.sprite;
            if(!(value is T typed))throw new Exception("unbound original panel resource "+guid);
            return new Handle<T>(typed);
        }
        public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T:Object=>LoadAssetByGuid<T>(guid);
        public IHomeHudAssetHandle<Sprite> LoadSprite(string path)=>new Handle<Sprite>(path=="ItemIcon/Props/Abnormal_Poisoned"?icon:path=="ItemIcon/Props/BG_Buff"?bg:throw new Exception(path));
        public LanguageType CurrentLanguage=>LanguageType.zh_CN;public void ReloadData(){}public bool ContainsKey(string key)=>texts.ContainsKey(key);
        public bool TryGetOriginalText(string key,LanguageType language,out string result)=>texts.TryGetValue(key,out result);
        public string GetText(string key,LanguageType language,params object[] args)=>TextFormatted(key,args);
        public string GetTextFormated(string key,LanguageType language,params object[] args)=>GetText(key,language,args);
        public void SetReplacer(ITextReplacer replacer){}public bool TryGetText(string key,LanguageType language,out string result)=>TryGetOriginalText(key,language,out result);
        public bool TryGetText(string key,out string result)=>texts.TryGetValue(key,out result);public void AddDataProvider(ILanguageTextDataProvider provider){}
        public LanguageType Language=>CurrentLanguage;
        IProfessionImageResourceLoader IProfessionPropertyBindings.Resources=>throw new Exception("power description needs no property image");
        public ProfessionPropertyDefinition GetProperty(ProfessionPropertyType type)=>throw new Exception("power description needs no property definition");
        public string GetEntryIcon(int id)=>throw new Exception("power description needs no entry icon");
        public object DecimalNumber(long value,int decimalPlaces)=>CultureNumberArg.Compact(value);
        public void PopupEntryDetail(IWindowOperator owner,string path,ProfessionItemText name,ProfessionItemText desc,bool control)=>throw new Exception("unexpected property entry popup");
        public void PopupBless(IWindowOperator owner,int itemRank,string description,bool effect,string unEffect,string unSeason){BlessPopups++;Require(description.Contains("1.23") && description.Contains("7655"),"original power popup uses base and blessing difference: "+description);}
        public bool IsShowTipDot=>true;public bool TempLimitShowTip=>false;public Action OnTipDotShowChanged{get;set;}
        public SafeAction OnTipDotSystemInited{get;set;}=new SafeAction();public SafeAction OnTipDotSystemPreClear{get;set;}=new SafeAction();
        public TipDotGraphType? GetGraphType(string name)=>null;
    }
    sealed class Handle<T>:IHomeHudAssetHandle<T> where T:Object
    {
        public Handle(T value){Result=value;}public T Result{get;}public bool IsValid=>true;public int Status=>2;
        public T WaitForAsyncComplete()=>Result;public event Action<IHomeHudAssetHandle<T>> Completed{add{}remove{}}public void Dispose(){}
    }
    sealed class PlayerData:IHomePanelPlayer
    {
        public long PlayerId;public long Id=>PlayerId;public float Hp=1;public bool Cure=true,Bless;public long Power=12345;
        public float HpPercent=>Hp;public IReadOnlyCollection<int> Maps=>Array.Empty<int>();
        public readonly Dictionary<int,IHomePanelStatusEffect> Statuses=new Dictionary<int,IHomePanelStatusEffect>();
        public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffectManager=>Statuses;
        public long NoBlessCombatRating=>12345;public int MyPlanesRank=>1;public bool HasCureItem()=>Cure;public bool HasBlessLevel()=>Bless;
        public string Name=>"家园玩家";public long CombatRating=>Power;public int LevelLimit=>5;public int Rank=>1;public int BaseLevel=>2;public int Level=>1;public int SubRank=>1;
        Action name,bless;Action<int> level,subrank;Action<long,long> combat,nextCombat;Action<object> seasonAdd,end;
        Action<float> hp;Action<int,int> reset;Action<IHomePanelStatusEffect> status;Action<HomeExperienceItem,long,object> item;
        public int Subscribers=>(name?.GetInvocationList().Length??0)+(bless?.GetInvocationList().Length??0)+(level?.GetInvocationList().Length??0)
            +(subrank?.GetInvocationList().Length??0)+(combat?.GetInvocationList().Length??0)+(nextCombat?.GetInvocationList().Length??0)
            +(seasonAdd?.GetInvocationList().Length??0)+(end?.GetInvocationList().Length??0)+(hp?.GetInvocationList().Length??0)
            +(reset?.GetInvocationList().Length??0)+(status?.GetInvocationList().Length??0)+(item?.GetInvocationList().Length??0);
        public event Action OnNameUpdate{add{name+=value;}remove{name-=value;}}public event Action<int> OnLevelUpdate{add{level+=value;}remove{level-=value;}}
        public event Action<long,long> OnCombatRatingChanged{add{combat+=value;}remove{combat-=value;}}
        public event Action<long,long> OnNextRankCombatRatingChanged{add{nextCombat+=value;}remove{nextCombat-=value;}}
        public event Action<int> OnSubRankUpdate{add{subrank+=value;}remove{subrank-=value;}}
        public event Action<object> OnSeasonRecordDataAdd{add{seasonAdd+=value;}remove{seasonAdd-=value;}}
        public event Action<object> OnSeasonRecordDataEndUpdate{add{end+=value;}remove{end-=value;}}
        public event Action<HomeExperienceItem,long,object> OnItemCountUpdate{add{item+=value;}remove{item-=value;}}
        public event Action OnBlessLevelDataUpdate{add{bless+=value;}remove{bless-=value;}}
        public event Action<float> OnHpUpdate{add{hp+=value;}remove{hp-=value;}}
        public event Action<int,int> OnBlessLevelDataReset{add{reset+=value;}remove{reset-=value;}}
        public event Action<IHomePanelStatusEffect> OnStatusEffectDataUpdate{add{status+=value;}remove{status-=value;}}
        public void RaiseHp()=>hp?.Invoke(Hp);public void RaiseFood()=>item?.Invoke(new HomeExperienceItem{Id=1},0,null);
        public void RaiseBless(){reset?.Invoke(1,1);combat?.Invoke(12345,Power);}public void RaiseStatus()=>status?.Invoke(null);
        public bool IsBlessLevelOpen()=>Bless;public bool ShowPlayerStoreExpLimit()=>false;public int GetMaxStoreExpLevel()=>5;
        public bool IsMaxBlessLevel()=>false;public long GetCurBlessPlayerExp()=>250;public int GetCurBlessPlayerLevel()=>1;public long GetItemAmount(int id)=>0;
    }
    sealed class StatusData:IHomePanelStatusEffect,IHomePanelStatusInfo
    {
        public StatusData(StatusEffectIconType icon,EStatusSourceType source,DateTime time,int duration,int count)
        {IconType=icon;StatusSourceType=source;LastGainedTime=time;Duration=duration;this.count=count;}
        readonly int count;public bool IsActive=>true;public EStatusSourceType StatusSourceType{get;}
        public IHomePanelStatusInfo Info=>this;public DateTime LastGainedTime{get;}public int GetDisplayRemainTimes()=>count;
        public StatusEffectIconType IconType{get;}public int EntryId=>1;public int ClassId=>1;public int Duration{get;}public bool IsReduceOncePerPlay=>false;
    }
}
