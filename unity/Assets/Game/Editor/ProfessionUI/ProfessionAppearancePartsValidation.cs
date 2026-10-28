using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearancePartsValidation
    {
        private const string Active="Zjcs.ProfessionParts.Active";
        private static bool ready;private static int phase,frame,refreshes;
        private static Bindings bindings;private static AppearanceUIPartHandler handler;private static GraphicProbe graphic;
        private static PartProbe back;private static GameObject root;private static List<GameObject> released;
        static ProfessionAppearancePartsValidation(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
        public static void Run()
        {
            try{ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();}
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("UI parts validation timed out.");
                if(!ready||!EditorApplication.isPlaying)return;
                if(phase==0){StartFixture();frame=Time.frameCount+2;phase=1;return;}
                if(Time.frameCount<frame)return;
                if(phase==1)
                {
                    Require(refreshes==2,"initial refresh repeats once in first LateUpdate before dirty resets");
                    Require(bindings.EffectRoots.All(x=>!x),"effect container destruction is deferred, extracted children survive");
                    Require(handler.LoadedDict[SkinType.hair].Parts.All(x=>x.GameObject),"extracted effect parts survive container destruction");
                    int loads=bindings.Handles.Count;handler.SetSkins(Skins());Require(loads==bindings.Handles.Count&&ReferenceEquals(back,handler.LoadedDict[SkinType.backDecoration].Parts[0]),"same skins reuse loaded parts");
                    Require(handler.AddSeparatorSlot("first")&&!handler.AddSeparatorSlot("first")&&!handler.AddSeparatorSlot("")&&!handler.RemoveSeparatorSlot(null),"dynamic separator mutation guards");
                    Require(graphic.separatorSlots.Count==3,"dynamic separator added and duplicates excluded");
                    Require(handler.AddSeparatorSlot("absent")&&handler.RemoveSeparatorSlot("absent"),"missing dynamic slot remains removable");
                    Require(handler.RemoveSeparatorSlot("first")&&!handler.RemoveSeparatorSlot("first"),"dynamic removal only updates on mutation");handler.ClearDynamicSeparatorSlots();
                    int before=graphic.Prepares;handler.AddExtraPart("extra-late");Require(graphic.Prepares==before+1,"late extra prepares immediately");
                    Require(bindings.Parts.Last().transform.parent.name=="Storage","late extra waits for LateUpdate reparenting");
                    frame=Time.frameCount+2;phase=2;return;
                }
                if(phase==2)
                {
                    Require(bindings.Parts.Last().transform.parent==graphic.SeparatorParts[0],"late extra is reparented behind its separator on real LateUpdate");
                    Require(bindings.Parts.Where(x=>x.Label.StartsWith("extra")).All(x=>x.Follower.Initializations==0),"extra parts are excluded from main follower initialization loop");
                    handler.ReleaseExtraParts();Require(bindings.Handles.Where(x=>x.Path.StartsWith("extra")).All(x=>x.Releases==1&&!x.Unload),"extra release uses default unload flag");
                    handler.IgnoreEffectTypes(SkinType.hair);handler.SetSkins(new Dictionary<SkinType,string>{{SkinType.backDecoration,"replacement"},{SkinType.hair,"effect-ignored"},{SkinType.faceDecoration,"emptySkin"}});
                    Require(handler.LoadedDict.Count==2&&handler.LoadedDict[SkinType.hair].Parts.Count==0&&!handler.TryGetSkeletonPart(SkinType.hair,out _),"ignored effects and emptySkin do not load parts; prior entries remain");
                    Require(back&&bindings.Handles[0].Releases==1,"changed skin releases handle before deferred object destruction");
                    released=handler.LoadedDict.Values.SelectMany(x=>x.Parts).Select(x=>x.GameObject).ToList();handler.enabled=false;handler.Release(true);
                    Require(handler.LoadedDict.Count==2&&handler.LoadedDict.Values.All(x=>x.Parts.Count==0&&x.Handles.Count==0&&x.SeparatorSlots==null),"release clears contents and retains original dictionary entries");
                    Require(bindings.Events.Last()=="release:Appearance/PlayerPart/replacement/replacement_UI_Integrated:True"&&bindings.Events.Contains("unload:replacement"),"immediate texture unload precedes asset release");
                    int errors=0;Application.LogCallback capture=(m,s,t)=>{if(t==LogType.Error)errors++;};Application.logMessageReceived+=capture;
                    try{handler.AddExtraPart("missing");}finally{Application.logMessageReceived-=capture;}
                    Require(errors==1&&bindings.Handles.Last().Path=="missing"&&bindings.Handles.Last().Releases==0,"original null-prefab failure leaves returned resource handle unreleased");
                    frame=Time.frameCount+2;phase=3;return;
                }
                Require(!back&&released.All(x=>!x),"released objects actually destroy after frame boundary");
                Debug.Log("Original UI appearance part orchestration passed real Play Mode frames: filtered skin maps, queued and immediate extras, reverse effect extraction, main part initialization/event order, cached skins, separator grouping and front/back parents, world transform preservation, dynamic mutations, LateUpdate dirty timing, deferred destruction, unload/release order and original null-prefab handle behavior. Resource, skeleton, follower and separator-renderer endpoints are fixtures; full profession UI and native actor rendering remain unbound.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static Dictionary<SkinType,string> Skins()=>new Dictionary<SkinType,string>{{SkinType.backDecoration,"back"},{SkinType.hair,"effect"},{SkinType.faceDecoration,"ignored"}};
        private static void StartFixture()
        {
            // Keep unrelated automatic startup from progressing into user save selection.
            foreach(var item in Object.FindObjectsOfType<MonoBehaviour>().Where(x=>x.GetType().FullName=="Zjcs.Offline.Startup.OfflineStartupFlow"))item.StopAllCoroutines();
            bindings=new Bindings();root=new GameObject("Original part application fixture");root.SetActive(false);graphic=root.AddComponent<GraphicProbe>();graphic.Initialize();
            var storage=new GameObject("Storage");storage.transform.SetParent(root.transform,false);storage.transform.localPosition=new Vector3(7,8,0);
            handler=root.AddComponent<AppearanceUIPartHandler>();Set(handler,"mSkeletonGraphic",graphic);Set(handler,"mSlot",storage.transform);handler.ConfigureParts(bindings);handler.IgnoreSkinTypes=new[]{SkinType.faceDecoration};
            handler.OnDrawOrderRefresh=()=>refreshes++;
            handler.OnSkinChanged.AddListener(()=>Require(bindings.Parts.Where(x=>!x.Label.StartsWith("extra")).All(x=>x.Follower.Initializations>0),"skin event follows main follower initialization"));
            root.SetActive(true);Require(!storage.activeSelf,"source Awake disables storage");
            handler.AddExtraPart("extra-queued");Require(bindings.Handles.Count==0,"extra before initialization queues without loading");
            var skins=Skins();handler.SetSkins(skins);
            Require(skins.Count==3&&bindings.Handles.Count==3&&!handler.LoadedDict.ContainsKey(SkinType.faceDecoration),"filtered copy preserves caller map and excludes ignored skin");
            back=(PartProbe)handler.LoadedDict[SkinType.backDecoration].Parts[0];
            Require(handler.TryGetSkeletonPart(SkinType.backDecoration,out var skeletonPart)&&ReferenceEquals(skeletonPart,back),"first skeleton part lookup");
            Require(handler.LoadedDict[SkinType.hair].Parts.Cast<PartProbe>().Select(x=>x.Label).SequenceEqual(new[]{"effect-front","effect-back"}),"effects extracted in reverse child order");
            Require(back.transform.parent==graphic.SeparatorParts[0]&&bindings.Parts.Single(x=>x.Label=="effect-front").transform.parent==graphic.SeparatorParts[1],"source back/front separator grouping");
            Require((back.transform.position-back.InitialWorld).sqrMagnitude<0.000001f,"default reparenting preserves world position");
            Require(graphic.enableSeparatorSlots&&graphic.separatorSlots.Count==2&&refreshes==1,"initial distinct separators and synchronous refresh");
            for(int i=0;i<3;i++){var mode=(PlayerPartMode)i;var suffix=i==0?"":"_"+mode;Require(ProfessionAppearancePartPaths.GetPlayerPartPrefabUI(mode,"x")=="Appearance/PlayerPart/x/x_UI"+suffix&&ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(mode,"x")=="Appearance/PlayerPart/x/x"+suffix,"all original part path modes");}
            Require(ProfessionAppearancePartPaths.GetSkinEffectPath(AppearanceRenderType.Renderer,"x")=="Appearance/PlayerEffect/Renderer/x","renderer effect address");
        }
        private static void Set(object target,string name,object value)=>typeof(AppearanceUIPartHandler).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void IgnoreEffectTypes(this AppearanceUIPartHandler target,SkinType type)=>Set(target,"IgnoreEffectTypes",new List<SkinType>{type});
        public sealed class Slot:IProfessionSkinClipSlot{public object Attachment=>null;}
        public sealed class Skeleton:IProfessionAppearancePartSkeleton
        {
            public IProfessionSkinClipSlot[] DrawOrder{get;}={new Slot(),new Slot(),new Slot()};
            public IProfessionSkinClipSlot FindSlot(string name)=>name=="first"?DrawOrder[0]:name=="mid"?DrawOrder[1]:name=="end"?DrawOrder[2]:null;
        }
        public sealed class GraphicProbe:MonoBehaviour,IProfessionAppearancePartGraphic
        {
            public IProfessionAppearancePartSkeleton Skeleton{get;}=new Skeleton();public List<IProfessionSkinClipSlot> separatorSlots{get;}=new List<IProfessionSkinClipSlot>();
            public List<ProfessionAppearancePartInstruction> separatorPartInstructions{get;}=new List<ProfessionAppearancePartInstruction>();public List<Transform> SeparatorParts{get;}=new List<Transform>();public bool enableSeparatorSlots{get;set;}public int Prepares;
            public void Initialize(){for(int i=0;i<3;i++){var go=new GameObject("Separator"+i);go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(i*13,2*i,0);SeparatorParts.Add(go.transform);}}
            public void PrepareInstructionsAndRenderers(bool validate){Require(!validate,"runtime prepare flag");Prepares++;separatorPartInstructions.Clear();foreach(var slot in separatorSlots.OrderBy(x=>Array.IndexOf(Skeleton.DrawOrder,x)))separatorPartInstructions.Add(new ProfessionAppearancePartInstruction(Array.IndexOf(Skeleton.DrawOrder,slot)));}
        }
        public sealed class Follower:IProfessionAppearancePartFollower
        {public string SlotName{get;set;}public bool ZFront{get;set;}public int Initializations;public void Initialize(){Initializations++;}}
        public sealed class PartProbe:MonoBehaviour,IAppearanceSkeletonPart,IProfessionAppearanceUnloadable
        {
            public string Label,SlotName;public bool Front;public Bindings Graph;public Vector3 InitialWorld;public Follower Follower;
            public GameObject GameObject=>gameObject;public GameObject SkeletonTarget=>gameObject;public bool HasAnim=>false;
            public IProfessionAppearancePartFollower SkeletonFollower=>Follower;
            public void Init(GameObject target){Require(target.GetComponent<GraphicProbe>(),"part init receives graphic object");Follower=new Follower{SlotName=SlotName,ZFront=Front};InitialWorld=transform.position;Graph.Parts.Add(this);Graph.Events.Add("init:"+Label);}
            public void TryPlayAnim(string sourceAnim){throw new InvalidOperationException("No animation endpoint in part fixture.");}
            public void MarkUnloadTextureImmediately(){Graph.Events.Add("unload:"+Label);}
        }
        public sealed class Bindings:IProfessionAppearancePartBindings
        {
            public AppearanceConfig Config{get;}=ScriptableObject.CreateInstance<AppearanceConfig>();public readonly List<Handle> Handles=new List<Handle>();public readonly List<PartProbe> Parts=new List<PartProbe>();public readonly List<string> Events=new List<string>();public readonly List<GameObject> EffectRoots=new List<GameObject>();private readonly List<GameObject> templates=new List<GameObject>();
            public Bindings(){typeof(AppearanceConfig).GetField("mPartConfigs",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Config,new List<AppearancePartConfig>{new AppearancePartConfig{SkinType=SkinType.backDecoration}});}
            public bool HasEffect(string skin)=>skin.StartsWith("effect");
            public IProfessionAppearancePartAsset LoadPrefab(string path)
            {
                GameObject prefab=null;if(path!="missing")
                {
                    prefab=new GameObject("Template");prefab.SetActive(false);templates.Add(prefab);
                    if(path=="Appearance/PlayerEffect/UI/effect")
                    {Add(prefab,"effect-back","end",false);Add(prefab,"effect-front","end",true);prefab.AddComponent<SeparatorSlotData>().SlotNames=new List<string>{"mid","end","absent"};}
                    else{var part=prefab.AddComponent<PartProbe>();part.Label=path.StartsWith("extra")?path:path.Contains("replacement")?"replacement":"back";part.SlotName=path.StartsWith("extra")?"end":"mid";part.Front=false;}
                }
                var handle=new Handle(this,path,prefab);Handles.Add(handle);return handle;
            }
            private static void Add(GameObject parent,string label,string slot,bool front){var go=new GameObject(label);go.transform.SetParent(parent.transform,false);var part=go.AddComponent<PartProbe>();part.Label=label;part.SlotName=slot;part.Front=front;}
            public GameObject Instantiate(GameObject prefab,Transform parent,bool world)
            {Require(!world,"source instantiate keeps local transform");var go=Object.Instantiate(prefab,parent,world);foreach(var part in go.GetComponentsInChildren<PartProbe>(true))part.Graph=this;if(go.GetComponent<SeparatorSlotData>())EffectRoots.Add(go);return go;}
            public void Cleanup(){foreach(var template in templates)if(template)Object.DestroyImmediate(template);Object.DestroyImmediate(Config);}
        }
        public sealed class Handle:IProfessionAppearancePartAsset
        {
            private readonly Bindings graph;public string Path;public int Releases;public bool Unload;public bool IsValid=>true;public int Status=>0;public GameObject Result{get;}public event Action<IProfessionAssetOperation<GameObject>> Completed;
            public Handle(Bindings graph,string path,GameObject result){this.graph=graph;Path=path;Result=result;}
            public void Release(bool all){Releases++;Unload=all;graph.Events.Add("release:"+Path+":"+all);}
            public void Dispose(){Release(false);}
        }
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("UI parts validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);if(root)Object.DestroyImmediate(root);bindings?.Cleanup();EditorApplication.Exit(code);}
    }
}
