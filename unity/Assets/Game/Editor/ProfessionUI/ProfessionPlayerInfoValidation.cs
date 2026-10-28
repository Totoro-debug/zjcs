using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionPlayerInfoValidation
    {
        private const string Active="Zjcs.ProfessionPlayerInfo.Active";private static bool ready;private static int phase,frame;
        private static Bindings context;private static PlayerInfoUI ui;private static PlayerIconMe icon;private static GameObject root;private static Camera camera;private static RenderTexture target;private static EventSystem events;private static SimulatedMouse mouse;private static PlayerIconMe automatic;
        static ProfessionPlayerInfoValidation(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
        public static void Run()
        {
            try{ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+180));EditorApplication.EnterPlaymode();}
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Player info validation timed out.");if(!ready||!EditorApplication.isPlaying)return;
                if(phase==0){Initialize();Check();frame=Time.frameCount+3;phase=1;return;}if(Time.frameCount<frame)return;frame=Time.frameCount+1;
                if(phase==1)
                {
                    Canvas.ForceUpdateCanvases();camera.Render();var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(512,512,TextureFormat.RGBA32,false,true);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=before;
                    Require(image.GetPixels32().Count(x=>x.a>0)>100,"fixture button actually renders on GPU");File.WriteAllBytes(ProfessionValidationWorkspace.Output("player-info-input.png"),image.EncodeToPNG());Object.Destroy(image);
                    var rect=(RectTransform)ui.Btn.transform;mouse.Point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));var hits=new List<RaycastResult>();events.RaycastAll(new PointerEventData(events){position=mouse.Point},hits);
                    Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==ui.Btn.gameObject,"simulated pointer reaches info button without occlusion");phase=2;return;
                }
                if(phase==2){mouse.Down=true;mouse.Held=true;phase=3;return;}
                if(phase==3){mouse.Down=false;mouse.Held=false;mouse.Up=true;phase=4;return;}
                if(phase==4)
                {
                    mouse.Up=false;Require(context.Popups==1&&context.DetailInitializations==0,"simulated mouse down/up schedules original detail callback");context.Pending(context);Require(context.DetailInitializations==1&&context.LastScene==7&&context.LastSkill==123,"deferred detail callback reads current player context");
                    icon.enabled=false;Require(!icon.InitFinished&&context.Player.Subscribers==0,"disable clears InitFinished and all nine player listeners");
                    int builds=context.Builds;context.OnPlayerUpdate.Invoke();Require(context.Builds==builds,"disabled self icon removes manager update listener");
                    var go=new GameObject("Original automatic icon fixture");go.transform.SetParent(root.transform,false);go.SetActive(false);automatic=go.AddComponent<PlayerIconMe>();automatic.ConfigureInfo(context);automatic.InfoUI=ui;automatic.AutoInit=true;go.SetActive(true);phase=5;frame=Time.frameCount+2;return;
                }
                Require(automatic.InitFinished&&context.Player.Subscribers==9,"original Start AutoInit runs in actual frame");automatic.ResetInitState();Require(context.Player.Subscribers==0&&context.Head.Resets==2,"reset delegates head reset before original subscription cleanup");
                int count=context.Operations.Count;Object.DestroyImmediate(root);root=null;Require(context.Operations.All(x=>x.Disposals==1)&&count>0,"destroy releases every retained resource operation exactly once");
                Debug.Log("Original PlayerInfoUI and PlayerIconMe passed Play Mode and simulated mouse input: data/appearance identity, ordered refresh, nine event routes and source duplicate-listener behavior, init/enable/disable/reset/automatic Start, self/UI/scene/head/NPC branches, title/planes/text/icons, collection/pin/joint/assist conditions, resource retention/disposal and deferred popup context. Widgets, player/config/resource and target-data producers are fixtures; original full page and production services remain unbound.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Initialize()
        {
            foreach(var startup in Object.FindObjectsOfType<MonoBehaviour>().Where(x=>x.GetType().FullName=="Zjcs.Offline.Startup.OfflineStartupFlow"))startup.StopAllCoroutines();
            context=new Bindings();root=new GameObject("Original player info application fixture");root.SetActive(false);
            camera=new GameObject("Fixture camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.orthographic=true;camera.orthographicSize=256;camera.transform.localPosition=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;target=new RenderTexture(512,512,24);target.Create();camera.targetTexture=target;
            var canvas=new GameObject("Input fixture canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));canvas.transform.SetParent(root.transform,false);var canvasComponent=canvas.GetComponent<Canvas>();canvasComponent.renderMode=RenderMode.ScreenSpaceCamera;canvasComponent.worldCamera=camera;canvasComponent.planeDistance=1;
            var host=new GameObject("Info fixture",typeof(RectTransform));host.transform.SetParent(canvas.transform,false);ui=host.AddComponent<PlayerInfoUI>();ui.ConfigureInfo(context);
            var buttonGo=new GameObject("Original listener test target",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));buttonGo.transform.SetParent(host.transform,false);var rect=buttonGo.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(200,80);buttonGo.GetComponent<Image>().color=new Color(0.25f,0.6f,0.3f,1);ui.Btn=buttonGo.GetComponent<Button>();
            ui.mStateSwitcher=WorldStateSwitcher<ActorTargetType>.FromBindings(host,new[]{new StateProbe(context,"target")});Set(ui,"mIsMyselfSwitcher",WorldStateSwitcher<bool>.FromBindings(host,new[]{new StateProbe(context,"self")}));
            ui.NameText=Text(host,"Name");ui.LevelText=Text(host,"Level");ui.SubRankText=Text(host,"Rank");ui.CombatRatingText=Text(host,"Combat");ui.ProfessionName=Text(host,"Profession");
            ui.ProfessionIcon=Image(host,"First icon");ui.ProfessionIconGo=ui.ProfessionIcon.gameObject;ui.ProfessionBigIcon=Image(host,"Third icon");ui.PlayerFigure=Image(host,"Figure");ui.PlayerIcon=Image(host,"Icon");ui.CollectionImg=Image(host,"Collection");ui.PinMask=Image(host,"Pin");
            foreach(var image in host.GetComponentsInChildren<Image>(true))if(image.gameObject!=buttonGo){image.raycastTarget=false;((RectTransform)image.transform).anchoredPosition=new Vector2(10000,10000);}
            context.Head=Widget(host,"Head",context);ui.PlayerHeadPortrait=context.Head;ui.PlayerAppearanceScene=Widget(host,"Scene",context);ui.HonorTitleItemProvider=Widget(host,"Title",context);ui.PlanesIconItem=Widget(host,"Planes",context);ui.NpcAppearanceSlot=new GameObject("NPC slot");ui.NpcAppearanceSlot.transform.SetParent(host.transform,false);ui.PlayerAppearance=host.AddComponent<AppearanceProbe>();
            icon=host.AddComponent<PlayerIconMe>();icon.ConfigureInfo(context);icon.InfoUI=ui;icon.IconMeHpSlider=Widget(host,"HP",context);icon.PanelPopupSceneType=7;
            var eventGo=new GameObject("Simulated input",typeof(EventSystem),typeof(StandaloneInputModule),typeof(SimulatedMouse));eventGo.transform.SetParent(root.transform,false);events=eventGo.GetComponent<EventSystem>();mouse=eventGo.GetComponent<SimulatedMouse>();eventGo.GetComponent<StandaloneInputModule>().inputOverride=mouse;
            root.SetActive(true);
        }
        private static void Check()
        {
            var ignored=new Target{Id=99,Kind=ActorTargetType.Npc};icon.Init(null,ignored,true);Require(icon.InitFinished&&context.HpInits==1&&context.Builds==1&&context.Player.Subscribers==9,"source init, HP initialization and nine subscriptions");Require(context.LastTarget==null&&ui.PlayerInfoData.TargetData.TargetId==1,"original Init does not assign actorTargetData parameter");
            Require(ReferenceEquals(ui.PlayerInfoData.AppearanceData,context.Appearance)&&ui.NameText.Text=="Main"&&ui.LevelText.Text=="5"&&ui.SubRankText.Text=="rank-3"&&ui.CombatRatingText.Text=="compact-60","projection preserves opaque appearance and original text routing");
            Require(((AppearanceProbe)ui.PlayerAppearance).Calls==1&&context.Head.Portraits==0&&context.TitleInits==1,"self UI appearance and honor title branch");Require(context.Events.Where(x=>x.StartsWith("target:")||x.StartsWith("self:")).Take(2).SequenceEqual(new[]{"target:0","self:1"}),"refresh player icon precedes appearance self state");
            context.Player.RaiseAll();Require(ui.PlayerInfoData.Level==8&&ui.PlayerInfoData.SubRank==4&&ui.PlayerInfoData.Name=="Updated"&&ui.PlayerInfoData.CombatRating==99&&ui.PlayerInfoData.Profession==10&&ui.PlayerInfoData.Sex==Sex.Female,"all original player events route to data setters");
            int appearanceCalls=((AppearanceProbe)ui.PlayerAppearance).Calls;context.OnPlayerUpdate.Invoke();Require(context.Player.Subscribers==18,"original repeated manager refresh appends duplicate player delegates");icon.Init();Require(context.Player.Subscribers==18,"original re-init removes one previous delegate then subscribes once");
            // Restore a single subscription set for the remaining independent branches.
            icon.ResetInitState();Require(context.Player.Subscribers==9,"reset removes one original subscription occurrence");context.Player.ClearEvents();icon.Init();Require(context.Player.Subscribers==9,"fresh explicit fixture event source");
            ui.UseUISpine=false;ui.Refresh();Require(context.SceneRefreshes>0,"self scene appearance branch");ui.UseUISpine=true;
            var other=context.Data();other.TargetData=new Target{Id=2};ui.IsNameWithPlanesName=true;ui.Init(other,icon.HandleOnClick);ui.Refresh();Require(context.Head.Portraits==1,"other player chooses head portrait before UI/scene path");
            ui.SetNameText("Other");Require(ui.NameText.Text=="Other:False"&&ui.PlanesIconItem.gameObject.activeSelf,"original name argument creation and other-server icon visibility");ui.SetNameTextActive(false);Require(!ui.NameText.gameObject.activeSelf,"explicit name visibility");
            ui.PlanesIconItem=null;ui.Refresh();Require(ui.NameText.Text=="Other:False@20","name-with-planes fallback format");
            var npc=context.Data();npc.TargetData=new Target{Id=40,Kind=ActorTargetType.Npc};npc.Icon="npc-icon";ui.Init(npc,icon.HandleOnClick);ui.SetShowCollectionImg(true);ui.Refresh();Require(ui.GetModelGo()==ui.GetSceneModelGo()&&ui.GetModelGo()!=null&&context.NpcInitializations==1,"NPC model load and both model getters");
            Require(ui.CollectionImg.gameObject.activeSelf&&!ui.PinMask.gameObject.activeSelf,"collected NPC visible without a team restriction");context.Catalog=TeamTargetCatalog.JointStrike;context.Joint=false;ui.SetProfessionIcon(10);Require(ui.PinMask.gameObject.activeSelf,"joint compatibility restriction");context.Joint=true;ui.SetProfessionIcon(10);Require(!ui.PinMask.gameObject.activeSelf,"joint compatible target allowed");
            context.Catalog=TeamTargetCatalog.ExploreBossTeam;context.Invite=new ProfessionInfoRobotInvite{OnePlayerMaxInviteCount=1,PlayType=8};((Target)npc.TargetData).Assist=false;ui.SetShowCollectionImg(true);Require(ui.PinMask.gameObject.activeSelf,"contextual assist count and cooldown branch");context.Invite=null;context.DefaultInvite.OnePlayerMaxInviteCount=0;ui.SetShowCollectionImg(true);Require(!ui.PinMask.gameObject.activeSelf,"default invite nonpositive limit returns before assist query");
            context.Player.Limited=true;ui.SetShowCollectionImg(true);Require(!ui.CollectionImg.gameObject.activeSelf&&!ui.PinMask.gameObject.activeSelf,"function limit suppresses collection and mask");context.Player.Limited=false;context.Player.Collected=false;ui.SetShowCollectionImg(true);Require(!ui.CollectionImg.gameObject.activeSelf,"uncollected NPC hidden");context.Player.Collected=true;
            var main=context.Data();ui.Init(main,icon.HandleOnClick);ui.Refresh();ui.SetProfessionIcon(0);Require(!ui.ProfessionIconGo.activeSelf&&!ui.ProfessionBigIcon.gameObject.activeSelf,"None profession hides both icons");ui.SetProfessionIcon(10);Require(ui.ProfessionIconGo.activeSelf&&ui.ProfessionBigIcon.gameObject.activeSelf,"profession icons restored");
            int oldCalls=context.Operations.Count;ui.SetPlayerIcon("new-icon");Require(context.Operations.Count==oldCalls+1&&context.Operations.All(x=>x.Disposals==0),"original refresh retains loaded handles until destruction");
            icon.SetStatus(1);icon.SetStatus(1);Require(context.Operations.Count==oldCalls+2,"status change refreshes icon once");
            icon.Init(new object(),ignored);Require(context.LastTarget==null,"lite path also consumes original unassigned target field");icon.Init();
            context.Events.Clear();context.DetailDeferred=true;
        }
        private static WorldMultiLanguageText Text(GameObject parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent.transform,false);go.SetActive(false);go.AddComponent<TextMeshProUGUI>();var text=go.AddComponent<WorldMultiLanguageText>();text.myKey="{0}";text.Localizer=key=>key;return text;}
        private static Image Image(GameObject parent,string name){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent.transform,false);return go.GetComponent<Image>();}
        private static WidgetProbe Widget(GameObject parent,string name,Bindings graph){var go=new GameObject(name);go.transform.SetParent(parent.transform,false);var widget=go.AddComponent<WidgetProbe>();widget.Graph=graph;widget.Label=name;return widget;}
        private static void Set(object targetObject,string field,object value)=>typeof(PlayerInfoUI).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(targetObject,value);
        public sealed class AppearanceProbe:PlayerAppearance
        {public int Calls;public override void RefreshAppearance(long id,Sex sex,int profession,object data){Calls++;}}
        public sealed class WidgetProbe:MonoBehaviour,IProfessionPlayerInfoHead,IProfessionPlayerInfoScene,IProfessionPlayerInfoTitle,IProfessionPlayerInfoPlanesIcon,IProfessionPlayerInfoHpSlider
        {
            public Bindings Graph;public string Label;public int Portraits,Resets;
            public void GeneratePortrait(long id,Sex sex,int profession,object appearance){Portraits++;}
            public void ResetInitState(){Resets++;}
            public void RefreshAppearance(long id,Sex sex,int profession,object appearance){Graph.SceneRefreshes++;}
            public void ClearTitleItem(){Graph.TitleClears++;}
            public void Init(object appearance){Graph.TitleInits++;}
            public void Init(long serverId){Graph.Events.Add("planes:"+serverId);}
            public void Init(){Graph.HpInits++;}
        }
        private sealed class StateProbe:IWorldStateConfig
        {private readonly Bindings graph;private readonly string name;public StateProbe(Bindings graph,string name){this.graph=graph;this.name=name;}public void SetState(WorldStateConfigValue state){graph.Events.Add(name+":"+(int)state);}public void Clear(){}}
        public sealed class Target:IProfessionInfoNpcTarget
        {public long Id=1;public ActorTargetType Kind;public bool Assist=true;public ActorTargetType TargetType=>Kind;public long TargetId=>Id;public long ServerId=>20;public bool IsNpc=>Kind==ActorTargetType.Npc;public int NpcClassId=>123;public string GetFigurePath(Sex sex)=>"npc-figure";public bool CheckActorCanAssist(int playType)=>Assist;}
        public sealed class Player:IProfessionInfoPlayer
        {
            public long Id=>1;public string Icon=>"main-icon";public string Name=>"Updated";public object AppearanceData{get;set;}public int Profession=>10;public bool Limited,Collected=true;public bool IsNpcPinLimited=>Limited;public bool? IsNpcCollected(int id)=>Collected;
            public event Action OnNameUpdate,OnIconUpdate;public event Action<int> OnSubRankUpdate,OnLevelUpdate,OnProfessionPromote;public event Action<long,long> OnCombatRatingChanged;public event Action<int,int> OnProfessionChange;public event Action<AppearanceOperateSource> OnAppearanceUpdate;public event Action<Sex> OnSexUpdate;
            public int Subscribers=>new Delegate[]{OnNameUpdate,OnIconUpdate,OnSubRankUpdate,OnLevelUpdate,OnProfessionPromote,OnCombatRatingChanged,OnProfessionChange,OnAppearanceUpdate,OnSexUpdate}.Sum(x=>x?.GetInvocationList().Length??0);
            public void RaiseAll(){OnNameUpdate?.Invoke();OnIconUpdate?.Invoke();OnSubRankUpdate?.Invoke(4);OnLevelUpdate?.Invoke(8);OnProfessionPromote?.Invoke(1);OnCombatRatingChanged?.Invoke(60,99);OnProfessionChange?.Invoke(1,10);OnAppearanceUpdate?.Invoke((AppearanceOperateSource)0);OnSexUpdate?.Invoke(Sex.Female);}
            public void ClearEvents(){OnNameUpdate=null;OnIconUpdate=null;OnSubRankUpdate=null;OnLevelUpdate=null;OnProfessionPromote=null;OnCombatRatingChanged=null;OnProfessionChange=null;OnAppearanceUpdate=null;OnSexUpdate=null;}
        }
        public sealed class Bindings:IProfessionPlayerInfoBindings,IProfessionImageResourceLoader,IProfessionPlayerInfoDetailPanel
        {
            public Player Player=new Player();public object Appearance=new object();public WidgetProbe Head;public int Builds,HpInits,TitleInits,TitleClears,SceneRefreshes,NpcInitializations,Popups,DetailInitializations,LastScene,LastSkill;public IProfessionInfoTarget LastTarget;
            public readonly List<string> Events=new List<string>();public readonly List<Handle> Operations=new List<Handle>();public TeamTargetCatalog? Catalog;public bool Joint,DetailDeferred;public Action<IProfessionPlayerInfoDetailPanel> Pending;public ProfessionInfoRobotInvite Invite,DefaultInvite=new ProfessionInfoRobotInvite();private GameObject npcPrefab;
            public Bindings(){Player.AppearanceData=Appearance;npcPrefab=new GameObject("NPC resource fixture");npcPrefab.SetActive(false);}
            public IProfessionInfoPlayer MainPlayer=>Player;public bool ManagerAvailable=>true;public SafeAction OnPlayerUpdate{get;}=new SafeAction();public IProfessionImageResourceLoader Resources=>this;
            public PlayerInfoData Data()=>new PlayerInfoData{TargetData=new Target(),Name="Main",NameArg="Main",Icon="main-icon",Level=5,SubRank=3,CombatRating=60,Profession=10,Sex=Sex.Male,AppearanceData=Appearance};
            public PlayerInfoData BuildData(IProfessionInfoPlayer player){Builds++;return Data();}public PlayerInfoData BuildLiteData(object lite,IProfessionInfoTarget targetData){LastTarget=targetData;return Data();}public object BuildBriefInfo(IProfessionInfoPlayer player,bool online){Require(online&&ReferenceEquals(player,Player),"popup builds current main brief info online at callback time");return player;}
            public object CreateNameArg(string name,bool translate)=>name+":"+translate;public object CompactNumber(long value)=>"compact-"+value;public string SubRankKey(int rank)=>"rank-"+rank;public string ProfessionNameKey(int profession)=>"profession-"+profession;public string GetFullBodyGuid(int profession,Sex sex)=>"figure-"+profession;public string FirstProfessionIcon(int profession)=>"first-"+profession;public string ThirdProfessionIcon(int profession)=>"third-"+profession;public ProfessionInfoText NameWithPlanes(object name,long serverId)=>new ProfessionInfoText("{0}@{1}",name,serverId);
            public bool HasHonorTitle(object data)=>ReferenceEquals(data,Appearance);public ProfessionInfoNpcDefinition GetNpc(int id)=>new ProfessionInfoNpcDefinition{Id=id,FigureModelUI="npc-model"};public GameObject InstantiateKeepScale(GameObject prefab,Transform parent)=>Object.Instantiate(prefab,parent,false);public void OverrideNpcInitialAnimation(GameObject model,int id){NpcInitializations++;}public TeamTargetCatalog? TeamCatalog(Component component)=>Catalog;public ProfessionInfoRobotInvite RobotInvite(Component component)=>Invite;public ProfessionInfoRobotInvite DefaultRobotInvite()=>DefaultInvite;public bool CanJointTogether(int targetProfession,int mainProfession)=>Joint;public int DuelSkillGroup(long playerId,long targetId){Require(playerId==-1&&targetId==-1,"source duel skill plan query uses sentinel IDs");return 123;}
            public void Popup(Component owner,Action<IProfessionPlayerInfoDetailPanel> initialized){Popups++;Require(DetailDeferred,"popup invoked after fixture setup");Pending=initialized;}public void Init(object info,int scene,int skill){Require(ReferenceEquals(info,Player),"detail brief info identity");DetailInitializations++;LastScene=scene;LastSkill=skill;}
            public IProfessionAssetOperation<T> LoadAsset<T>(string path)where T:Object{var handle=new Handle(path,typeof(T)==typeof(GameObject)?npcPrefab:null);Operations.Add(handle);return new TypedHandle<T>(handle);}
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string path)where T:Object=>LoadAsset<T>(path);public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path)where T:Object=>throw new InvalidOperationException("Original info UI loads synchronously.");public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string path)where T:Object=>throw new InvalidOperationException("Original info UI loads synchronously.");public void Cleanup(){if(npcPrefab)Object.DestroyImmediate(npcPrefab);}
        }
        public sealed class Handle{public readonly string Path;public readonly Object Value;public int Disposals;public Handle(string path,Object value){Path=path;Value=value;}}
        private sealed class TypedHandle<T>:IProfessionAssetOperation<T>where T:Object
        {private readonly Handle handle;public TypedHandle(Handle handle){this.handle=handle;}public bool IsValid=>true;public int Status=>0;public T Result=>handle.Value as T;public event Action<IProfessionAssetOperation<T>> Completed;public void Dispose(){handle.Disposals++;}}
        public sealed class SimulatedMouse:BaseInput
        {public Vector2 Point;public bool Down,Held,Up;public override bool mousePresent=>true;public override Vector2 mousePosition=>Point;public override Vector2 mouseScrollDelta=>Vector2.zero;public override bool GetMouseButtonDown(int button)=>button==0&&Down;public override bool GetMouseButtonUp(int button)=>button==0&&Up;public override bool GetMouseButton(int button)=>button==0&&Held;public override float GetAxisRaw(string name)=>0;public override bool GetButtonDown(string name)=>false;}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Player info validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);if(root)Object.DestroyImmediate(root);context?.Cleanup();if(target){target.Release();Object.DestroyImmediate(target);}EditorApplication.Exit(code);}
    }
}
