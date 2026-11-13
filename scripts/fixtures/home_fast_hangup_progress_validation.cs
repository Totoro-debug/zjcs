using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.HomeFastHangUpProgressUI;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeFastHangUpProgressUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeFastHangUpProgressValidation
{
    const string Key = "Zjcs.HomeFastHangUpProgressValidation";
    static HomeFastHangUpProgressValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("MutedOriginalHomeProgress").AddComponent<HomeFastHangUpProgressRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key+".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original acceleration presentation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSettings.asyncShaderCompilation = false; ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key+".Deadline", (float)EditorApplication.timeSinceStartup+240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "progress-result.json"),
            JsonUtility.ToJson(new Result { success=error==null, error=error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        else Debug.Log("HOME_FAST_HANGUP_PROGRESS_PASSED: complete original hierarchy/legacy animation events, actual male/female saved player, native sleep/bubble animation, sex-specific appearance override, reward sprite leases, UI visibility and close callback/disposal. Reward item selection and UI owner are controlled ports; production transactions and Player remain pending.");
        EditorApplication.Exit(error==null?0:1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class HomeFastHangUpProgressRunner : MonoBehaviour, IHomeFastHangUpProgressServices, IHomeFastHangUpServices
{
    string work; OfficialHomeFastHangUpProgressAssetCatalog assets; OfficialHomeFastHangUpAssetCatalog selection;
    HomeFastHangUpInfo fast; FontAssetFontService fonts; IDisposable metrics; IProfessionImageResourceLoader images;
    AppearanceConfig config; ProfessionAppearanceTables tables; ProfessionAppearanceResolver resolver;
    long playerId; Camera cameraUi; RenderTexture render; Canvas canvas; GameObject current;
    int errors, beds, closes, finished; bool visible = true; readonly List<string> calls = new List<string>();
    LeaseLoader leases;
    IEnumerator Start()
    {
        Application.runInBackground = true; AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) => { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors++; };
        var steps = Check();
        while(true) { object value; try { if(!steps.MoveNext())break; value=steps.Current; } catch(Exception error) { HomeFastHangUpProgressValidation.Finish(error); yield break; } yield return value; }
        HomeFastHangUpProgressValidation.Finish(errors==0?null:new Exception("Runtime errors: "+errors));
    }
    IEnumerator Check()
    {
        work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"); string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        assets = OfficialHomeFastHangUpProgressAssetCatalog.LoadDefault(); selection=OfficialHomeFastHangUpAssetCatalog.LoadDefault();
        fast = JObject.Parse(selection.rules.text)["tables"]["fast_hang_up"].First().ToObject<HomeFastHangUpInfo>();
        Require(fast.BindFurniture==EFurniture.Bed && assets.progressPrefab, "Actual selected bed configuration and original prefab");
        LanguageManager.Initialize(new OfflinePartnerLocalizationService(assets.texts));
        fonts=OfflineHomeHudAssets.LoadDefault().CreateFontService(); fonts.Initialize(); metrics=ScreenMetrics.Bind(new Metrics());
        var imageCatalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        imageCatalog.Entries = selection.images.Select(x=>new OfflineProfessionImageCatalog.Entry { Path="Assets/Image/"+x.path,AssetGuid=x.assetGuid,Asset=x.sprite }).ToArray();
        leases=new LeaseLoader(new OfflineProfessionImageResourceLoader(imageCatalog));images=leases;
        var resources=UnityEngine.Resources.Load<ProfessionNativeSceneAppearanceResources>("Partners/appearance-resources");
        config=ProfessionNativePortraitStudioResources.LoadDefault().AppearanceConfig;
        tables=ProfessionAppearanceTables.Load(UnityEngine.Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text); resolver=new ProfessionAppearanceResolver(tables);
        var defaults=new ProfessionAppearanceSkinDefaults(tables,UnityEngine.Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,message=>{throw new Exception(message);});
        var animationRules=UnityEngine.Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        Func<string,string> read=path=>File.ReadAllText(Path.Combine(repo,"unity/Assets/Official",path));
        var catalog=OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"),read("CharacterCreation/wardrobe-rules.json"),read("Character/character-overview.json"));
        var slots=new ProfileSlotCatalog(Path.Combine(work,"progress-profiles")); var slot=slots.CreateSlot();
        var session=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        Require(session.ApplyCharacterMutation(player=>{ player.CharacterCreated=true;player.CharacterName="家园加速测试";player.InitialWeaponType=200;player.SubRank=1;SetAppearance(player,Sex.Male); }).IsApplied,"Create isolated saved player");
        session.EnsureLocalActorIdentities(); playerId=session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId;
        var progression=new OfflineCharacterProgressionService(session,catalog);
        var playerBindings=new OfflineProfessionAppearanceBindings(playerId,()=>progression.Player,id=>catalog.ItemType(id),tables,config,animationRules,defaults);
        using var manager=new AppearanceAssetManager(UnityEngine.Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config"),new GAction().Subscribe);
        using var textures=new OfflineProfessionSceneTextureResources(resources);
        var partAssets=UnityEngine.Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/Rank/PartAssets");
        var partTextures=new OfflinePartnerUiTextureResources(partAssets,manager,textures);
        var parts=new OfflinePartnerUiPartBindings(partAssets,tables.SpineSkinInfoParser,root=>partTextures.BindTree(root));
        var oldTextures=ProfessionAppearanceTextureBindings.Current;var oldClip=ProfessionAppearanceSkinClipBindings.Current;
        ProfessionAppearanceTextureBindings.Current=partTextures;ProfessionAppearanceSkinClipBindings.Current=new OfflineProfessionCurrentUiSkinClipBindings(oldClip);
        var pipeline=UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        cameraUi=new GameObject("MutedHomeProgressCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cameraUi.orthographic=true;cameraUi.clearFlags=CameraClearFlags.SolidColor;cameraUi.backgroundColor=new Color(.12f,.2f,.28f);
        render=new RenderTexture(608,1080,24);render.Create();cameraUi.targetTexture=render;
        canvas=new GameObject("OriginalProgressViewport",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUi;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        try
        {
            for(int pass=0;pass<2;pass++)
            {
                if(pass==1) { Require(session.ApplyCharacterMutation(player=>SetAppearance(player,Sex.Female)).IsApplied,"Persist female appearance"); session=OfflineMainlineProgressSession.OpenCharacter(slots,slot);progression=new OfflineCharacterProgressionService(session,catalog);playerBindings.Refresh(AppearanceOperateSource.Normal,false); }
                var stage=new GameObject("InactiveOriginalProgressOwner");stage.SetActive(false);
                current=Object.Instantiate(assets.progressPrefab,stage.transform,false);current.SetActive(false);
                OfflineHomeFastHangUpProgressBindings.Bind(current,this,resources,manager,parts,playerBindings);
                foreach(var text in current.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer=key=>LanguageManager.Instance.GetText(key);
                current.transform.SetParent(canvas.transform,false);Object.Destroy(stage);
                var dialog=current.GetComponent<FastHangUpProgressBarDialog>();var graphic=dialog.PlayerAppearance.GetComponent<ProfessionNativeUiGraphic>();
                var animation=current.GetComponent<Animation>();
                Require(current.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x),"Every original script bound");
                Require(animation && animation.clip && animation.clip.events.Length==2 && animation.clip.events[0].functionName=="PlayEndAnim" && animation.clip.events[1].functionName=="CloseWindow","Original legacy animation callbacks");
                int oldFinished=finished,oldBeds=beds,oldCloses=closes;calls.Clear();
                current.SetActive(true);
                Require(graphic.startingAnimation=="home/idle_M_01" && graphic.State!=null
                    && !graphic.State.Source.AnimationNames.Contains(graphic.startingAnimation)
                    && graphic.AnimationState.GetCurrent(0)==null,"Preserve the original unavailable starting name and initialize without substituting an animation");
                dialog.Init(fast.FastHangUpId,()=>{Require(leases.Active==8,"Original callback precedes sprite release");finished++;calls.Add("finished");});
                yield return null;
                Require(graphic.State!=null && graphic.AnimationState!=null,"Actual saved native player initialized by original MyAppearance");
                Require(graphic.AnimationState.GetCurrent(0)?.Animation.Name=="home/sleep_02","Actual original sleeping animation track");
                Require(dialog.PlayerAppearance.AppearanceRuntimeData.Sex==(pass==0?Sex.Male:Sex.Female),"Original player consumes the persisted sex");
                Require(!visible && beds==oldBeds+1 && finished==oldFinished,"Bed signal and underlying UI hidden before awards");
                Require(dialog.AppearanceUI.GetComponent<RectTransform>().localPosition==new Vector3(-5,63,0),"Original sleep position");
                Require(dialog.RewardImageList.Count==8 && dialog.RewardImageList.All(x=>x.sprite),"Every original reward sprite bound");
                Require(current.GetComponentsInChildren<WorldMultiLanguageText>(true).All(x=>x.Text==LanguageManager.Instance.GetText(x.myKey)),"Every original presentation label resolves its official text");
                var bubble=current.GetComponentsInChildren<PartnerNativeUiActor>(true).Single(x=>x.gameObject.name=="Sleep_Bubble");Require(bubble.Graphic.State!=null && bubble.TryGetAnimation("loop"),"Actual original sleeping bubble animation initialized");
                var overrideConfig=config.GetOverrideConfig(AppearanceOverrideType.Sleep);
                var skins=pass==0?overrideConfig.MaleOverrideSkinIdList:overrideConfig.FemaleOverrideSkinIdList;
                Require(skins.Count>0 && skins.All(id=>{var skin=tables.SpineSkinInfoParser.Get(id);return dialog.PlayerAppearance.AppearanceRuntimeData.SkinMap[skin.SkinType]==skin.SkinName;}),"Actual sex-specific original sleeping skin override");
                yield return new WaitForSeconds(.35f);Capture("progress-"+pass+"-sleep.png");
                float start=Time.realtimeSinceStartup;
                while(current.activeSelf && Time.realtimeSinceStartup-start<8)yield return null;
                Require(!current.activeSelf && closes==oldCloses+1 && finished==oldFinished+1 && visible,"Original clip closes the window and delivers completion exactly once");
                Require(leases.Active==0,"Every actual original reward sprite lease released");
                Require(calls.SequenceEqual(new[]{"hide","show","close","finished","show"}),"Original PlayEndAnim/CloseWindow/OnDisable callback and visibility order: "+string.Join(",",calls));
                Object.Destroy(current);yield return null;
            }
        }
        finally
        {
            if(current)Object.DestroyImmediate(current);
            ProfessionAppearanceTextureBindings.Current=oldTextures;ProfessionAppearanceSkinClipBindings.Current=oldClip;
            metrics.Dispose();fonts.Dispose();
        }
        Require(errors==0,"No actual runtime errors");
    }
    void SetAppearance(OfflinePlayerProgress player,Sex sex)
    {
        var data=resolver.BuildDefaultAppearanceData(config,sex,(Profession)1);player.Wardrobe.Sex=(int)sex;player.Wardrobe.Profession=1;
        player.Wardrobe.BaseSkins=data.SkinMap.Select(p=>new OfflineAppearanceSkin{Type=(int)p.Key,Id=p.Value.SkinId}).ToList();
        player.Wardrobe.Dyes=data.DyeMap.Select(p=>new OfflineAppearanceDye{Type=(int)p.Key,ColorId=p.Value.ColorId,Hue=p.Value.Hsv.H,Saturation=p.Value.Hsv.S,Value=p.Value.Hsv.V}).ToList();
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(cameraUi,new UniversalRenderPipeline.SingleCameraRequest{destination=render});
        var previous=RenderTexture.active;RenderTexture.active=render;var image=new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,render.width,render.height),0,0);image.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(work,name),image.EncodeToPNG());
        Require(!image.GetPixels32().Any(c=>c.r>245 && c.g<10 && c.b>245),"No shader error pixels");Object.Destroy(image);
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Exception Outside()=>new InvalidOperationException("Outside the original progress presentation fixture");
    sealed class Metrics:IScreenMetrics{public int Width=>608;public int Height=>1080;public ScreenOrientation Orientation=>ScreenOrientation.Portrait;public Rect SafeArea=>new Rect(0,0,608,1080);}
    sealed class Info:IProfessionItemInfo{public int ClassId=>1;public string Icon{get;set;}public string IconEffect=>null;public Quality Quality=>0;public bool CanOverlap=>true;public object GetFakeIcon()=>null;}
    sealed class LeaseLoader:IProfessionImageResourceLoader
    {
        readonly IProfessionImageResourceLoader inner;public int Active;
        public LeaseLoader(IProfessionImageResourceLoader inner){this.inner=inner;}
        public IProfessionAssetOperation<T> LoadAsset<T>(string path)where T:Object=>new Lease<T>(this,inner.LoadAsset<T>(path));
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path)where T:Object=>throw Outside();
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string path)where T:Object=>throw Outside();
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string path)where T:Object=>throw Outside();
        sealed class Lease<T>:IProfessionAssetOperation<T>where T:Object
        {
            readonly LeaseLoader owner;readonly IProfessionAssetOperation<T> inner;bool disposed;
            public Lease(LeaseLoader owner,IProfessionAssetOperation<T> inner){this.owner=owner;this.inner=inner;owner.Active++;}
            public bool IsValid=>inner.IsValid;public int Status=>inner.Status;public T Result=>inner.Result;
            public event Action<IProfessionAssetOperation<T>> Completed{add{inner.Completed+=value;}remove{inner.Completed-=value;}}
            public void Dispose(){Require(!disposed,"Reward sprite lease disposed once");disposed=true;inner.Dispose();owner.Active--;}
        }
    }
    public IHomeFastHangUpServices FastHangUp=>this;public IProfessionImageResourceLoader Resources=>images;
    public AppearanceConfig AppearanceConfig=>config;public ProfessionAppearanceTables AppearanceTables=>tables;public ProfessionAppearanceResolver AppearanceResolver=>resolver;public long MainPlayerId=>playerId;
    public void GetFastHangUpRewardItems(int id,ref List<IProfessionItemInfo> items){items.Add(new Info{Icon=selection.images.First().path});}
    public int RandomRange(int min,int max)=>UnityEngine.Random.Range(min,max);public void PublishBedFastHangUp(){beds++;}
    public void SetActiveUIVisible(UIBase owner,bool value){visible=value;calls.Add(value?"show":"hide");}
    public void Close(GameObject root){closes++;calls.Add("close");root.SetActive(false);}
    public IHomeHangUpWindowServices Window=>throw Outside();public IHomeFastHangUpPlayer Player=>throw Outside();public int DefaultFastItemUseCount=>throw Outside();public double FastItemMaxCountMessageColdDown=>throw Outside();
    public HomeFastHangUpInfo GetFastHangUpInfo(int id)=>fast;public bool TryGetFastHangUpItemHours(int id,out int hours){throw Outside();}public IProfessionItemInfo GetHangUpRewardItem(int id)=>throw Outside();
    public ItemIcon GetIcon(int id,Transform parent)=>throw Outside();public ItemIcon GetIcon(IProfessionItemInfo item,Transform parent)=>throw Outside();public void RecycleIcon(ItemIcon icon)=>throw Outside();
    public double GetHangUpUnsettledRewardCount(int id,out double seconds){throw Outside();}public void RequestFastHangUp(HomeFastHangUpRequest request,Action<HomeFastHangUpResponse,int> callback)=>throw Outside();
    public void OpenProgress(int id,Action callback)=>throw Outside();public void OpenAward(IHomeWindowAwardMessage message)=>throw Outside();
}
