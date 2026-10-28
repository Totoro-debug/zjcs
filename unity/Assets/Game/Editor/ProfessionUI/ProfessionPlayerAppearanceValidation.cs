using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionPlayerAppearanceValidation
    {
        [Serializable] private sealed class RuleFixture{public string path;public AnimCombineRule[] rules;}
        private const string Active="Zjcs.ProfessionPlayerAppearance.Active";
        private static bool ready;
        static ProfessionPlayerAppearanceValidation()
        {
            EditorApplication.update+=Update;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Player appearance fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                Check();Debug.Log("Original player appearance application lifecycle passed in Play Mode: self event registration/enable/disable/filtering, lite callback release/reinitialization/completion and original Clear behavior, data identity/runtime reuse, override restoration, observer order and exception boundaries, animation before asset loading, cached names, base-profession/sex/loop parameters and rule formatting. Player/protocol/resource/animation endpoints are fixtures; native character rendering and full profession page remain unbound.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check()
        {
            var graph=new Bindings();var go=new GameObject("Original player appearance lifecycle fixture");
            var original=JsonUtility.FromJson<RuleFixture>(System.IO.File.ReadAllText(ProfessionValidationWorkspace.Fixture("player-appearance-validation.json")));
            var nativeRules=AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>(original.path);
            Require(nativeRules&&nativeRules.Rules.Count==original.rules.Length,"original animation-rule native asset binding");
            for(int i=0;i<original.rules.Length;i++)
            {
                var a=original.rules[i];var b=nativeRules.Rules[i];
                Require(a.Key==b.Key&&a.Content==b.Content&&a.Desc==b.Desc&&a.FillList.SequenceEqual(b.FillList)&&ReferenceEquals(nativeRules.GetRule(a.Key),b),"all original rule fields/order and deserialized lookup");
            }
            Require(ProfessionAppearanceAnimationNames.GetAnimName(graph,nativeRules,"profession_sex",new List<string>{"idle","1"},Sex.Female,200)=="200/idle_F_01","actual original animation rule format");
            var appearance=go.AddComponent<AppearanceProbe>();appearance.Graph=graph;
            var character=go.AddComponent<AppearanceCharacter>();character.Configure(graph);
            var player=go.AddComponent<PlayerProbe>();player.AppearanceCharacter=character;player.ConfigurePlayerAppearance(graph);player.SetAnimation("profession_sex",new List<string>{"idle","1"},true);
            var observer=go.AddComponent<Observer>();observer.Graph=graph;Set(player,typeof(PlayerAppearance),"mAppearanceInfoList",new List<APlayerAppearanceInfo>{null,observer});
            player.OnAppearanceRefreshed+=()=>graph.Events.Add("event");
            bool completed=false;player.Init(7,true,()=>completed=true);
            Require(completed&&player.Inited&&player.IsSelf&&graph.Subscribers==1&&graph.Requests.Count==0,"self init completion and single event subscription");
            Require(graph.Events[0]=="play:100/M/idle_01:True"&&graph.Events.IndexOf("build")<graph.Events.IndexOf("load")&&graph.Events.IndexOf("skins")<graph.Events.IndexOf("observer")&&graph.Events.Last()=="event","animation before data/build/load, then appearance/observer/event order");
            Require(ReferenceEquals(graph.LastData,graph.AppearanceData)&&ReferenceEquals(graph.LastAppend,graph.Append),"original data and append-list identity");
            player.SetAnimation("profession_sex",new List<string>{"different","9"},false);graph.Events.Clear();player.RefreshAppearance(7,Sex.Male,100,graph.AppearanceData);
            Require(graph.Events[0]=="play:100/M/idle_01:False","same profession/sex retains cached name while using current loop");player.SetAnimation("profession_sex",new List<string>{"idle","1"},true);
            var runtime=player.AppearanceRuntimeData;Require(ReferenceEquals(runtime,observer.Data),"observer receives original runtime object");
            graph.Events.Clear();graph.Raise(AppearanceOperateSource.Map);graph.Raise(AppearanceOperateSource.ProfessionPromote);Require(graph.Events.Count==0,"map and promote events do not refresh");
            graph.Raise(AppearanceOperateSource.ProfessionChange);Require(ReferenceEquals(runtime,player.AppearanceRuntimeData)&&graph.Events.Contains("build"),"other events reuse runtime object");
            int count=graph.Builds;go.SetActive(false);Require(graph.Subscribers==0,"disable unsubscribes self");graph.Raise(AppearanceOperateSource.Normal);Require(graph.Builds==count,"disabled self receives no refresh");go.SetActive(true);Require(graph.Subscribers==1&&graph.Builds==count+1,"enable registers then refreshes");
            var first=new Override("override-one");var second=new Override("override-two");player.SetRuntimeDataOverride(first);Require(runtime.SkinMap[SkinType.body]=="override-one"&&first.Calls==1,"override applies immediately");
            player.SetRuntimeDataOverride(first);Require(first.Calls==1,"same override is no-op");player.SetRuntimeDataOverride(second);Require(second.Before=="base"&&runtime.SkinMap[SkinType.body]=="override-two","old changed skin restored before new override");player.SetRuntimeDataOverride(null);Require(runtime.SkinMap[SkinType.body]=="base","removing override restores original map entry");
            count=graph.Builds;graph.Valid=false;player.Init(7);Require(player.Inited&&graph.Builds==count&&graph.Subscribers==1,"invalid self data skips refresh but completes init");graph.Valid=true;
            int exceptions=0;Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Exception)exceptions++;};Application.logMessageReceived+=capture;
            Action fault=()=>throw new InvalidOperationException("fixture observer failure");player.OnAppearanceRefreshed+=fault;
            try{player.RefreshAppearance(7,Sex.Male,100,graph.AppearanceData);}finally{player.OnAppearanceRefreshed-=fault;Application.logMessageReceived-=capture;}
            Require(exceptions==1&&graph.Events.Last()=="event","appearance-refreshed subscriber failure is contained after original multicast order");
            graph.Events.Clear();player.AutoResetAnimation=false;player.RefreshAppearance(7,Sex.Female,200,graph.AppearanceData);Require(!graph.Events.Any(x=>x.StartsWith("play:")),"auto-reset off skips only animation");player.AutoResetAnimation=true;
            graph.Events.Clear();player.RefreshAppearance(7,Sex.Female,0,graph.AppearanceData);Require(graph.Events[0]=="play:100/F/idle_01:True"&&graph.BaseRequests>0,"none profession resolved before formatting/animation");
            player.SetRuntimeDataOverride(first);player.Clear();Require(!player.Inited&&!player.IsSelf&&player.AppearanceRuntimeData==null&&graph.Subscribers==0,"clear unregisters and resets runtime/init flags");
            graph.Events.Clear();player.RefreshAppearance(7,Sex.Male,100,graph.AppearanceData);Require(player.AppearanceRuntimeData.SkinMap[SkinType.body]=="override-one","original Clear retains selected override");
            CheckPreview(graph,character,go);
            player.Clear();bool remoteComplete=false;player.Init(99,true,()=>remoteComplete=true);var stale=graph.Requests.Last();Require(!player.Inited&&!player.IsSelf&&stale.Valid&&graph.Force.Last(),"remote request stores callback and force flag");
            player.Init(100,false,()=>remoteComplete=true);var current=graph.Requests.Last();Require(!stale.Valid&&current.Valid&&!graph.Force.Last(),"reinitialization releases previous callback");stale.Invoke(new ProfessionPlayerLiteAppearance{Sex=Sex.Male,Profession=100,AppearanceData=new object()});Require(!remoteComplete,"released callback cannot complete stale request");
            object remote=new object();current.Invoke(new ProfessionPlayerLiteAppearance{Sex=Sex.Female,Profession=200,AppearanceData=remote});Require(remoteComplete&&player.Inited&&ReferenceEquals(graph.LastData,remote),"remote data identity and completion precede Inited");
            player.Clear();player.Init(101);current=graph.Requests.Last();player.Clear();Require(current.Valid,"original Clear does not release pending lite callback");current.Invoke(new ProfessionPlayerLiteAppearance{Sex=Sex.Male,Profession=100,AppearanceData=remote});Require(player.Inited,"original pending callback can initialize after Clear");
            player.Clear();player.Init(102);current=graph.Requests.Last();UnityEngine.Object.DestroyImmediate(go);Require(!current.Valid,"destroy releases pending callback");
            var rules=graph.AnimationRules;
            Require(ProfessionAppearanceAnimationNames.GetAnimName(graph,rules,"profession_sex",new List<string>{"run","9"},Sex.Female,200)=="200/F/run_09","source rule fill order and ID padding");
            Require(ProfessionAppearanceAnimationNames.GetAnimName(graph,rules,"unknown",null)==string.Empty,"absent rule returns empty");
            Require(ProfessionAppearanceAnimationNames.GetFormatId(-1)=="0-1"&&ProfessionAppearanceAnimationNames.GetFormatId(10)=="10","original signed/small ID formatting");
            var cancel=new ProfessionLiteInfoCallback(_=>throw new MissingReferenceException("The object of type fixture was destroyed"));int errors=0;capture=(message,stack,type)=>{if(type==LogType.Error)errors++;};Application.logMessageReceived+=capture;
            try{cancel.Invoke(null);}finally{Application.logMessageReceived-=capture;}Require(errors==1&&!cancel.Valid,"destroyed callback target logs and releases");
            UnityEngine.Object.DestroyImmediate(graph.Config);UnityEngine.Object.DestroyImmediate(graph.AnimationRules);
        }
        private static void CheckPreview(Bindings graph,AppearanceCharacter character,GameObject go)
        {
            var data=new PreviewData();data.Appended[SkinType.pWeapon]=10;data.Appended[SkinType.sWeapon]=11;data.Appended[SkinType.hair]=12;data.Equipment[EquipPos.Weapon]=20;data.Equipment[EquipPos.SecondWeapon]=21;
            var preview=new PreviewBindings(graph);preview.Definition=new ProfessionAppearancePreviewDefinition
            {
                DefaultSkins=new Dictionary<Sex,Dictionary<SkinType,int>>{{Sex.Male,new Dictionary<SkinType,int>{{SkinType.pWeapon,501}}}},
                AppendAppearanceRewards=new Dictionary<Sex,Dictionary<SkinType,int>>{{Sex.Male,new Dictionary<SkinType,int>{{SkinType.body,33}}}}
            };
            ProfessionAppearancePreview.SetProfessionAppearance(preview,graph.Config,null,Sex.Male,100,data,0);Require(preview.Clones==0&&preview.Lookups==0,"null preview character returns before clone/config lookup");
            var component=go.AddComponent<ProfessionAppearance>();component.CharacterAppearance=character;component.ConfigurePreview(preview);component.InitAppearance(100,Sex.Male,data,"idle");
            var clone=(PreviewData)graph.LastData;
            Require(!ReferenceEquals(clone,data)&&clone.Equipment[EquipPos.Weapon]==501&&!clone.Equipment.ContainsKey(EquipPos.SecondWeapon)&&!clone.Appended.ContainsKey(SkinType.pWeapon)&&!clone.Appended.ContainsKey(SkinType.sWeapon)&&clone.Appended[SkinType.body]==33&&clone.Appended[SkinType.hair]==12,"source default weapon/removal and appended reward operations on cloned data");
            Require(data.Equipment[EquipPos.Weapon]==20&&data.Equipment[EquipPos.SecondWeapon]==21&&data.Appended.Count==3&&!data.Appended.ContainsKey(SkinType.body),"profession preview preserves player dictionaries");
            var ui=go.AddComponent<PreviewUIProbe>();var routing=new PreviewUIBindings(graph);ui.ConfigureUI(routing);
            component.InitAppearance(200,Sex.Female,data,"preview-loop");clone=(PreviewData)graph.LastData;
            Require(clone.Equipment[EquipPos.Weapon]==20&&clone.Equipment[EquipPos.SecondWeapon]==21&&clone.Appended.Count==3,"missing sex defaults/rewards preserve cloned state");
            Require(graph.Events.Last()=="preview-play:preview-loop:True","original profession component routes looping UI animation after skin data");
            preview.Definition=null;component.InitAppearance(0,Sex.Male,data,"none-loop");clone=(PreviewData)graph.LastData;
            Require(clone.Equipment.Count==2&&clone.Appended.Count==3&&preview.Clones==3&&preview.Lookups==3,"none profession still clones/looks up definition and skips default overrides");
        }
        public sealed class PreviewUIProbe:AppearanceUI
        {protected override void Awake(){}protected override void OnDestroy(){}}
        private sealed class PreviewUIBindings:IProfessionAppearanceUIBindings
        {
            private readonly Bindings graph;public PreviewUIBindings(Bindings graph){this.graph=graph;}
            public IProfessionAppearanceUIAssetManager AssetManager=>throw new InvalidOperationException("No graphics in preview-routing fixture.");
            public bool TryLazyLoadSpineAnimation(IProfessionAppearanceUIGraphic graphic,string name,out object animation){throw new InvalidOperationException("No graphics in preview-routing fixture.");}
            public void PlayAnimation(IProfessionAppearanceUIGraphic graphic,string name,bool loop){graph.Events.Add("preview-play:"+name+":"+loop);}
        }
        private sealed class PreviewData
        {public Dictionary<SkinType,int> Appended=new Dictionary<SkinType,int>();public Dictionary<EquipPos,int> Equipment=new Dictionary<EquipPos,int>();}
        private sealed class PreviewBindings:IProfessionAppearancePreviewBindings
        {
            public IProfessionPlayerAppearanceBindings Player{get;}public ProfessionAppearancePreviewDefinition Definition;public int Clones,Lookups;
            public PreviewBindings(IProfessionPlayerAppearanceBindings player){Player=player;}
            public ProfessionAppearancePreviewData CloneAppearance(object source)
            {Clones++;var data=(PreviewData)source;var copy=new PreviewData{Appended=new Dictionary<SkinType,int>(data.Appended),Equipment=new Dictionary<EquipPos,int>(data.Equipment)};return new ProfessionAppearancePreviewData(copy,copy.Appended,copy.Equipment);}
            public ProfessionAppearancePreviewDefinition GetProfession(int profession){Lookups++;return Definition;}
        }
        public sealed class PlayerProbe:PlayerAnimationAppearance
        {public void SetAnimation(string rule,List<string> parameters,bool loop){mRuleKey=rule;mParams=parameters;mLoop=loop;}}
        public sealed class Observer:APlayerAppearanceInfo
        {
            public Bindings Graph;public AppearanceRuntimeData Data;
            public override void RefreshAppearance(Sex sex,int profession,AppearanceRuntimeData data){Data=data;Graph.Events.Add("observer");}
        }
        public sealed class AppearanceProbe:MonoBehaviour,IAppearanceComponent,IAppearanceAnimComponent
        {
            public Bindings Graph;public bool Valid=>true;public IProfessionAppearanceAnimationState AnimState=>null;
            public void Init(ScriptableObject main,ScriptableObject split){Graph.Events.Add("init");}
            public void SetSkins(List<string> skins){Graph.Events.Add("skins");}
            public void SetDye(string name,Dictionary<int,HsvChange> dye){Graph.Events.Add("dye");}
            public bool TryGetMaterial(string name,out Material material){material=null;return false;}
            public void Release(bool tryUnloadAllAssets=false){Graph.Events.Add("release");}
            public void PlayAnimation(string name,bool loop){Graph.Events.Add("play:"+name+":"+loop);}
        }
        private sealed class Override:IAppearanceRuntimeDataOverride
        {
            private readonly string value;public int Calls;public string Before;
            public Override(string value){this.value=value;}
            public void ApplyOverride(AppearanceRuntimeData runtime,object data,Dictionary<SkinType,string> changed=null)
            {Calls++;Before=runtime.SkinMap[SkinType.body];changed.Clear();changed.Add(SkinType.body,Before);runtime.SkinMap[SkinType.body]=value;}
        }
        public sealed class Bindings:IProfessionPlayerAppearanceBindings,IProfessionPlayerAppearanceSource,IProfessionAppearanceCharacterBindings
        {
            public readonly List<string> Events=new List<string>();public readonly List<ProfessionLiteInfoCallback> Requests=new List<ProfessionLiteInfoCallback>();public readonly List<bool> Force=new List<bool>();
            public List<string> Append=new List<string>{"append"};public object LastData;public List<string> LastAppend;public int Builds,BaseRequests;public bool Valid=true;
            public IProfessionPlayerAppearanceSource MainPlayer=>this;public long Id=>7;public Sex Sex=>Sex.Male;public int Profession=>100;public object AppearanceData{get;}=new object();
            private Action<AppearanceOperateSource> handlers;public int Subscribers=>handlers?.GetInvocationList().Length??0;
            public event Action<AppearanceOperateSource> OnAppearanceUpdate{add{handlers+=value;}remove{handlers-=value;}}
            public void Raise(AppearanceOperateSource source){handlers?.Invoke(source);}
            public AppearanceConfig Config{get;}=ScriptableObject.CreateInstance<AppearanceConfig>();public AppearanceAnimNameCombineRule AnimationRules{get;}=ScriptableObject.CreateInstance<AppearanceAnimNameCombineRule>();
            public Bindings()
            {
                var append=new AppendSkinConfig();Set(append,typeof(AppendSkinConfig),"mAppendSkins",Append);
                Set(Config,typeof(AppearanceConfig),"mPartConfigs",new List<AppearancePartConfig>());Set(Config,typeof(AppearanceConfig),"mAppendSkinConfigs",new List<AppendSkinConfig>{append});
                Set(AnimationRules,typeof(AppearanceAnimNameCombineRule),"mRules",new List<AnimCombineRule>{new AnimCombineRule{Key="profession_sex",Content="{0}/{1}/{2}_{3}",FillList=new List<RuleFillType>{RuleFillType.Profession,RuleFillType.Sex,RuleFillType.InputStr,RuleFillType.InputId}}});AnimationRules.OnAfterDeserialize();
            }
            public bool IsAppearanceValid(object data)=>Valid;public IEnumerable<string> GetSkinNames(object data)=>new[]{"fixture"};
            public void GetLiteInfo(long id,ProfessionLiteInfoCallback callback,bool force){Requests.Add(callback);Force.Add(force);}
            public void BuildAppearanceRuntimeData(Sex sex,int profession,object data,List<string> append,AppearanceRuntimeData target)
            {Builds++;Events.Add("build");LastData=data;LastAppend=append;target.Sex=sex;target.SkinMap=target.SkinMap??new Dictionary<SkinType,string>();target.SkinMap.Clear();target.SkinMap.Add(SkinType.body,"base");target.AppendSkins=append;}
            public int GetBaseProfession(int profession){BaseRequests++;return profession==0?100:profession;}
            public string GetProfessionIdStr(int profession)=>GetBaseProfession(profession).ToString();
            public IProfessionAppearanceCharacterAsset LoadAsset(string path){Events.Add("load");return new Handle(Config);}
            public void SetPartSkins(Component component,Dictionary<SkinType,string> skins){throw new InvalidOperationException("No parts in this lifecycle fixture.");}
            public void ReleaseParts(Component component,bool all){throw new InvalidOperationException("No parts in this lifecycle fixture.");}
        }
        private sealed class Handle:IProfessionAppearanceCharacterAsset
        {public ScriptableObject Result{get;}public Handle(ScriptableObject asset){Result=asset;}public void Dispose(){}}
        private static void Set(object target,Type type,string field,object value){type.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Player appearance validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
