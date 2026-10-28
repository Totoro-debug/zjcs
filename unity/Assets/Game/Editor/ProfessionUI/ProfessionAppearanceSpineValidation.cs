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
    public static class ProfessionAppearanceSpineValidation
    {
        private const string Active = "Zjcs.ProfessionAppearanceSpine.Active";
        private static bool ready;
        static ProfessionAppearanceSpineValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Skin composition fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Profession original AppearanceSpine application lifecycle passed: ordered combined-skin reuse/clear/merge, missing skins, same/new skin slot branches, setup/event order, caught event exception, source HSV material writes and config failures, null/same/different asset initialization, selective release/cache/setup/unload, null skeleton/data branches and destruction. Skin/skeleton endpoints are fixtures; no skeleton SDK implementation, production skin graph, source character rendering or full profession UI is validated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Check()
        {
            var go = new GameObject("Original application skin composition fixture");
            var spine = go.AddComponent<SpineProbe>();
            var graph = new Graph(); spine.Graph = graph; spine.Configure(graph, null);
            var asset = ScriptableObject.CreateInstance<AppearanceConfig>();
            var otherAsset = ScriptableObject.CreateInstance<AppearanceConfig>();
            var split = ScriptableObject.CreateInstance<AppearanceConfig>();
            var loader = ScriptableObject.CreateInstance<LoaderProbe>(); loader.Events = graph.Events; spine.Loader = loader;
            Require(!spine.Valid && !spine.UseSplitAnimation, "original initial validity");
            spine.Init(asset, split);
            Require(spine.Valid && spine.UseSplitAnimation && ReferenceEquals(spine.SkeletonDataAsset, asset) && ReferenceEquals(spine.SpineAnimationSplitBinarySo, split), "asset identities and validity");
            Require(graph.Events.SequenceEqual(new[] { "init" }), "first init does not release");
            graph.Events.Clear(); spine.Init(asset, otherAsset);
            Require(graph.Events.Count == 0 && ReferenceEquals(spine.SpineAnimationSplitBinarySo, split), "same asset skips split replacement and init");
            int errors = 0; Application.LogCallback capture = (m,s,t) => { if (t == LogType.Error) errors++; };
            Application.logMessageReceived += capture;
            try { spine.Init(null, otherAsset); } finally { Application.logMessageReceived -= capture; }
            Require(errors == 1 && ReferenceEquals(spine.SkeletonDataAsset, asset), "null init error preserves existing state");
            var a = new Skin(graph.Events, "a"); var b = new Skin(graph.Events, "b");
            object fromA = new object(), fromB = new object(), fromOther = new object();
            a.Attachments[(0,"body")] = fromA; b.Attachments[(0,"body")] = fromB; b.Attachments[(2,"keep")] = fromOther;
            graph.Skins["a"] = a; graph.Skins["b"] = b;
            int updates = 0; spine.OnSkinUpdate = () => { graph.Events.Add("event"); updates++; };
            graph.Events.Clear(); spine.SetSkins(new List<string> { "a", "missing", "b", "a" });
            Require(graph.Events.SequenceEqual(new[] { "create:combineSkin", "data:True", "find:a", "add:a", "find:missing", "find:b", "add:b", "find:a", "add:a", "component", "set-skin", "setup", "event" }), "original ordered composition and new-skin execution");
            Require(spine.Skins.SequenceEqual(new[] { "a", "b", "a" }) && updates == 1, "source valid names preserve duplicates");
            Require(ReferenceEquals(graph.SkeletonImpl.Skin.GetAttachment(0,"body"), fromA), "last original skin wins at compound key");
            var combined = graph.SkeletonImpl.Skin;
            object old = new object(); for (int i=0;i<graph.SkeletonImpl.Attachments.Length;i++) graph.SkeletonImpl.Attachments[i]=old;
            graph.Events.Clear(); spine.SetSkins(new List<string> { "b" });
            Require(ReferenceEquals(combined, graph.SkeletonImpl.Skin), "combined skin reused");
            Require(graph.Events.SequenceEqual(new[] { "clear:combineSkin", "data:True", "find:b", "add:b", "slot:0", "slot:2", "cache", "setup", "event" }), "same-skin branch restores found non-null setup names before cache/setup/event");
            Require(ReferenceEquals(graph.SkeletonImpl.Attachments[0], fromB) && ReferenceEquals(graph.SkeletonImpl.Attachments[2], fromOther) && ReferenceEquals(graph.SkeletonImpl.Attachments[1],old) && ReferenceEquals(graph.SkeletonImpl.Attachments[3],old), "null names and unresolved attachments retain current slot values");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            Require(shader, "original translated appearance shader"); spine.Material = new Material(shader);
            var values = new Dictionary<int,HsvChange> {
                { 2,new HsvChange { OffsetH=.25f, OffsetScaleS=-.5f, OffsetScaleV=.75f } },
                { 1,new HsvChange { OffsetH=-.1f, OffsetScaleS=.2f, OffsetScaleV=-.3f } } };
            spine.SetDye("b",values);
            Require(spine.Material.GetVector(AppearanceMaterialUtils.GetDyePropertyId(2)) == new Vector4(.25f,-.5f,.75f,0) && spine.Material.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)) == new Vector4(-.1f,.2f,-.3f,0), "original HSV channel values and order");
            Require(spine.TryGetMaterial("b",out Material material) && ReferenceEquals(material,spine.Material), "original material lookup");
            spine.SetDye("missing",values); Require(!spine.TryGetMaterial("missing",out _), "missing material branch");
            var textureAsset = ScriptableObject.CreateInstance<SkinTextureAsset>();
            var textureConfig = new SkinTextureConfig { SkinName="b",PlaceHolderTexture=Texture2D.whiteTexture };
            typeof(SkinTextureAsset).GetField("mSkinTextureConfigList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(textureAsset,new List<SkinTextureConfig>{textureConfig});
            textureAsset.OnAfterDeserialize(); spine.SkinTextureAsset=textureAsset;
            Require(ReferenceEquals(spine.ConfigFor("b"),textureConfig), "original skin texture config identity");
            errors=0;Application.logMessageReceived+=capture;
            try { Require(spine.ConfigFor("missing")==null,"missing config result"); }
            finally { Application.logMessageReceived-=capture; }
            Require(errors==1,"original missing config error");
            int afterThrow=0;spine.OnSkinUpdate=()=>throw new InvalidOperationException("original subscriber fixture");spine.OnSkinUpdate+=()=>afterThrow++;
            errors=0;Application.logMessageReceived+=capture;
            try { spine.SetSkins(new List<string>{"a"}); } finally { Application.logMessageReceived-=capture; }
            Require(errors==1 && afterThrow==0,"original event exception caught around entire multicast invocation");spine.OnSkinUpdate=null;
            graph.Events.Clear();spine.Release(false);
            Require(graph.Events.SequenceEqual(new[]{"clear:combineSkin"}) && !spine.Skins.Any() && spine.Valid,"partial release clears skins but retains valid asset and skips unload");
            graph.Events.Clear();spine.Release(true);
            Require(graph.Events.SequenceEqual(new[]{"clear:combineSkin","cache","setup","unload:True"}),"full release clears, recaches, resets then unloads");
            graph.Events.Clear();spine.Init(otherAsset,null);
            Require(graph.Events.SequenceEqual(new[]{"clear:combineSkin","init"}) && !spine.UseSplitAnimation && ReferenceEquals(spine.SkeletonDataAsset,otherAsset),"different valid asset releases before assigning and initializing");
            graph.SkeletonImpl.DataAvailable=false;graph.Events.Clear();spine.SetSkeletonSkins(a);
            Require(graph.Events.Count==0,"null skeleton data skips skin/cache operations");graph.SkeletonImpl.DataAvailable=true;
            graph.HasSkeleton=false;graph.Events.Clear();spine.Release(true);
            Require(graph.Events.SequenceEqual(new[]{"clear:combineSkin","unload:True"}),"missing skeleton still unloads when component exists");graph.HasSkeleton=true;
            graph.Events.Clear();UnityEngine.Object.DestroyImmediate(go);
            Require(graph.Events.SequenceEqual(new[]{"clear:combineSkin"}),"original destroy uses partial release");
            var absent = new GameObject("Absent skeleton component branch").AddComponent<SpineProbe>();
            var noComponent = new Graph { HasComponent=false };absent.Graph=noComponent;absent.Configure(noComponent,null);absent.Init(asset,null);
            noComponent.Events.Clear();absent.SetSkeletonSkins(a);absent.Release(true);
            Require(noComponent.Events.SequenceEqual(new[]{"component"}),"null skeleton component skips all slot/cache/unload paths");
            UnityEngine.Object.DestroyImmediate(absent.gameObject);
            UnityEngine.Object.DestroyImmediate(spine.Material);UnityEngine.Object.DestroyImmediate(asset);UnityEngine.Object.DestroyImmediate(otherAsset);UnityEngine.Object.DestroyImmediate(split);UnityEngine.Object.DestroyImmediate(loader);UnityEngine.Object.DestroyImmediate(textureAsset);
        }
        public sealed class SpineProbe : AppearanceSpine
        {
            public Graph Graph;public LoaderProbe Loader;public Material Material;
            public override YooAssetTextureLoader TextureLoader=>Loader;
            protected override void InitSkeleton(ScriptableObject asset){Graph.Events.Add("init");}
            protected override Material GetMaterial(string skin)=>skin=="b"?Material:null;
            public SkinTextureConfig ConfigFor(string name)=>GetSkinTextureConfig(name);
        }
        public sealed class LoaderProbe : YooAssetTextureLoader
        {public List<string> Events;public override void TryUnloadUnusedTextures(bool ignoreRequestFrame=false){Events.Add("unload:"+ignoreRequestFrame);}}
        public sealed class Skin : IProfessionAppearanceSkin
        {
            public readonly Dictionary<(int,string),object> Attachments=new Dictionary<(int,string),object>();
            private readonly List<string> events;public readonly string Name;
            public Skin(List<string> events,string name){this.events=events;Name=name;}
            public void Clear(){events.Add("clear:"+Name);Attachments.Clear();}
            public void AddSkin(IProfessionAppearanceSkin source){var skin=(Skin)source;events.Add("add:"+skin.Name);foreach(var pair in skin.Attachments)Attachments[pair.Key]=pair.Value;}
            public object GetAttachment(int slot,string name)=>Attachments.TryGetValue((slot,name),out object value)?value:null;
        }
        public sealed class Skeleton : IProfessionAppearanceSkeleton
        {
            private readonly Graph graph;public bool DataAvailable=true;public object[] Attachments=new object[4];
            public Skeleton(Graph graph){this.graph=graph;}
            public IProfessionAppearanceSkeletonData Data=>DataAvailable?graph:null;
            public IProfessionAppearanceSkin Skin{get;private set;}public int SlotCount=>4;
            public string SetupAttachmentName(int slot)=>new[]{"body",null,"keep","notfound"}[slot];
            public void SetSlotAttachment(int slot,object attachment){graph.Events.Add("slot:"+slot);Attachments[slot]=attachment;}
            public void SetSkin(IProfessionAppearanceSkin skin){graph.Events.Add("set-skin");Skin=skin;}
            public void SetToSetupPose(){graph.Events.Add("setup");}
            public void UpdateCache(){graph.Events.Add("cache");}
        }
        public sealed class Graph : IProfessionAppearanceSpineBindings,IProfessionAppearanceSkeletonData,IProfessionAppearanceSkeletonComponent
        {
            public readonly List<string> Events=new List<string>();public readonly Dictionary<string,Skin> Skins=new Dictionary<string,Skin>();
            public readonly Skeleton SkeletonImpl;public bool HasSkeleton=true,HasComponent=true;
            public Graph(){SkeletonImpl=new Skeleton(this);}
            public IProfessionAppearanceSkeleton Skeleton=>HasSkeleton?SkeletonImpl:null;
            public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner){Events.Add("component");return HasComponent?this:null;}
            public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset,bool quiet){Events.Add("data:"+quiet);return this;}
            public IProfessionAppearanceSkin FindSkin(string name){Events.Add("find:"+name);return Skins.TryGetValue(name,out Skin skin)?skin:null;}
            public IProfessionAppearanceSkin CreateSkin(string name){Events.Add("create:"+name);return new Skin(Events,name);}
        }
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
