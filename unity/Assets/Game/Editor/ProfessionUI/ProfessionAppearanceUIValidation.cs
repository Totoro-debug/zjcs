using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceUIValidation
    {
        private const string Active="Zjcs.ProfessionAppearanceUI.Active";
        private static bool ready;
        private static readonly List<Object> owned=new List<Object>();
        static ProfessionAppearanceUIValidation()
        {EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
        public static void Run()
        {
            try{ProfessionValidationWorkspace.AssertIsolatedProject();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Active,true);SessionState.SetFloat(Active+".Deadline",(float)(EditorApplication.timeSinceStartup+120));EditorApplication.EnterPlaymode();}
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Update()
        {
            if(!SessionState.GetBool(Active,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("UI appearance fixture did not enter Play Mode.");
                if(!ready||!EditorApplication.isPlaying)return;
                Check();Debug.Log("Profession original AppearanceUI orchestration passed: initialization/ref/event order, source material stencil/placeholder copying, main/dye demand requests and material reuse, replacement callback identity, deferred mask skin/dye refresh, mesh bounds guard/context, animation dispatch and release/unsubscribe. Graphic/skeleton/resource services are fixtures using real Unity Graphic/CanvasRenderer/masks/materials; source character rendering and full profession UI are not validated.");Finish(0);
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Check()
        {
            var graph=new ProfessionAppearanceSpineValidation.Graph();
            graph.Skins["a"]=new ProfessionAppearanceSpineValidation.Skin(graph.Events,"a");
            graph.Skins["b"]=new ProfessionAppearanceSpineValidation.Skin(graph.Events,"b");
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");Require(shader,"original UI appearance shader");
            var binding=new Binding(shader);ProfessionAppearanceTextureBindings.Current=binding;
            Texture placeholder=Own(new Texture2D(1,1));binding.Main=Own(new Texture2D(2,2));binding.Dye=Own(new Texture2D(2,2));
            var original=Own(new Material(shader));original.mainTexture=placeholder;original.SetFloat("_Stencil",13);original.SetFloat("_StencilComp",3);original.SetFloat("_StencilReadMask",63);original.SetFloat("_StencilWriteMask",7);original.SetFloat("_ColorMask",15);original.SetFloat("_StencilOp",2);
            var loader=Own(ScriptableObject.CreateInstance<YooAssetTextureLoader>());
            loader.placeholderMap=new[]{new AppearanceOnDemandTextureLoader<YooAssetTextureReference,YooAssetRequest>.PlaceholderTextureMapping{placeholderTexture=placeholder,targetTextureReference=new YooAssetTextureReference{AssetReference=Proxy("main")},dyeTextureReference=new YooAssetTextureReference{AssetReference=Proxy("dye")}}};
            var canvas=Own(new GameObject("Original UI orchestration fixture",typeof(RectTransform),typeof(Canvas)));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var root=Own(new GameObject("Original appearance component",typeof(RectTransform),typeof(CanvasRenderer)));root.SetActive(false);root.transform.SetParent(canvas.transform,false);
            var graphic=root.AddComponent<GraphicProbe>();graphic.Graph=graph;graphic.Events=binding.Events;
            var ui=root.AddComponent<AppearanceUI>();Set(ui,"mSkeletonGraphic",graphic);Set(ui,"mTextureLoader",loader);ui.Configure(graph,null);ui.ConfigureUI(binding);
            var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(100,200);root.SetActive(true);Require(rect.sizeDelta==Vector2.zero,"original Awake clears rectangle size");
            var asset=Own(ScriptableObject.CreateInstance<AppearanceConfig>());var split=Own(ScriptableObject.CreateInstance<AppearanceConfig>());
            binding.Events.Clear();ui.Init(asset,split);
            Require(binding.Events.SequenceEqual(new[]{"ref+","lazy:home/idle_01","init:True"})&&graphic.allowMultipleCanvasRenderers&&graphic.MeshListeners==1&&graphic.DemandListeners==1,"initialize native graph and refs exactly once before lazy animation/init");
            Require(ReferenceEquals(graphic.skeletonDataAsset,asset)&&ReferenceEquals(ui.AnimState,graphic.AnimationState),"source asset/animation identities");
            ui.Init(asset,null);Require(binding.Events.Count==3,"same asset skips repeated initialization");
            int skinsUpdated=0;ui.OnSkinUpdate=()=>{binding.Events.Add("event");skinsUpdated++;};
            binding.Events.Clear();ui.SetSkins(new List<string>{"a","b"});Require(binding.Events.SequenceEqual(new[]{"apply","sync","bounds","event"})&&skinsUpdated==1,"original animation/submesh/bounds refresh before skin callback");
            var textureAsset=Own(ScriptableObject.CreateInstance<SkinTextureAsset>());Set(textureAsset,"mSkinTextureConfigList",new List<SkinTextureConfig>{new SkinTextureConfig{SkinName="b",PlaceHolderTexture=placeholder}});textureAsset.OnAfterDeserialize();ui.SkinTextureAsset=textureAsset;
            graphic.Materials=new[]{original,null,original};graphic.Textures=new[]{placeholder,placeholder,placeholder};int replacements=0;
            ui.ReplaceMaterialsAfterLoad=materials=>{Require(ReferenceEquals(materials,graphic.Materials),"original replacement array identity");replacements++;};
            graphic.Demand();Require(replacements==1&&binding.Handles.Count==2&&binding.Materials.Count==1&&ui.IndependentMaterialDict.Count==1,"one material and main/dye operation for duplicate placeholder");
            Material independent=graphic.Materials[0];Require(ReferenceEquals(independent,graphic.Materials[2])&&!graphic.Materials[1]&&ReferenceEquals(graphic.Textures[0],binding.Main)&&ReferenceEquals(graphic.Textures[2],binding.Main)&&ReferenceEquals(graphic.Textures[1],placeholder),"source null slot, loaded texture replacements and shared material");
            Require(independent.GetFloat("_Stencil")==13&&independent.GetFloat("_StencilReadMask")==63&&independent.GetFloat("_StencilWriteMask")==7&&independent.GetFloat("_StencilComp")==3&&independent.GetFloat("_ColorMask")==15&&independent.GetFloat("_StencilOp")==2&&independent.GetTexture("_DyeTex")==binding.Dye,"source stencil and main/dye material values");
            Require(ui.TryGetMaterial("b",out Material found)&&ReferenceEquals(found,independent),"skin texture config resolves cached independent material");
            graphic.Materials=new[]{original,original};graphic.Textures=new[]{placeholder,placeholder};graphic.Demand();Require(replacements==2&&binding.Handles.Count==2&&binding.Materials.Count==1&&ReferenceEquals(graphic.Materials[0],independent),"source placeholder reuses cached material without loading or adding another reference");
            graphic.allowMultipleCanvasRenderers=false;graphic.Demand();Require(replacements==2,"single canvas early return");graphic.allowMultipleCanvasRenderers=true;
            ui.PlayAnimation("200/idle_01",true);Require(binding.Events.Last()=="play:200/idle_01:True","original animation dispatch");
            // The source mask search also sees a Mask outside an override-sorting
            // Canvas. Stencil depth stops at that nearer sorting root.
            canvas.AddComponent<Image>();canvas.AddComponent<Mask>();
            Canvas sorting=root.AddComponent<Canvas>();sorting.overrideSorting=true;
            Require(AppearanceMaterialUtils.IsMaskable(graphic)&&AppearanceMaterialUtils.GetStencilValue(graphic.transform)==0,"real enabled parent mask outside override-sorting canvas: mask="+AppearanceMaterialUtils.IsMaskable(graphic)+", depth="+AppearanceMaterialUtils.GetStencilValue(graphic.transform));
            binding.Events.Clear();ui.SetSkins(new List<string>{"b"});
            var dye=new Dictionary<int,HsvChange>{{1,new HsvChange{OffsetH=.2f}}};ui.SetDye("b",dye);dye[1]=new HsvChange{OffsetH=.4f,OffsetScaleS=.5f,OffsetScaleV=.6f};
            Require(binding.Events.Count==0&&Flag(ui,"mSkinApplyDirty")&&Flag(ui,"mDyeDirty"),"mask defers skin and retains dye-map reference");
            graphic.SetMaterialDirty();Require(Flag(ui,"mSkinApplyDirty")&&binding.Events.Count==0,"stencil-zero dirty callback retains pending state");
            var nested=Own(new GameObject("Nested real mask",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Mask)));nested.transform.SetParent(canvas.transform,false);sorting.overrideSorting=false;root.transform.SetParent(nested.transform,false);graphic.SetMaterialDirty();
            Require(AppearanceMaterialUtils.GetStencilValue(graphic.transform)>0&&!Flag(ui,"mSkinApplyDirty")&&!Flag(ui,"mDyeDirty")&&binding.Events.SequenceEqual(new[]{"apply","sync","bounds","event"}),"positive stencil applies pending skin once before dye");
            Require(independent.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1))==new Vector4(.4f,.5f,.6f,0),"deferred source dye cache keeps original dictionary reference");
            binding.Events.Clear();graphic.SetMaterialDirty();Require(binding.Events.Count==0,"dirty callback unregistered after successful stencil refresh");
            var rendererObject=Own(new GameObject("Actual submesh canvas renderer",typeof(RectTransform),typeof(CanvasRenderer)));rendererObject.transform.SetParent(root.transform,false);CanvasRenderer renderer=rendererObject.GetComponent<CanvasRenderer>();graphic.Renderers.Add(renderer);
            var huge=Own(new Mesh());huge.vertices=new[]{Vector3.zero,new Vector3(10001,0),new Vector3(0,1)};huge.triangles=new[]{0,1,2};renderer.SetMesh(huge);
            int errors=0;Application.LogCallback capture=(m,s,t)=>{if(t==LogType.Error){errors++;Require(m.Contains("home/idle_01")||m.Contains("OnCheckMultipleMeshesValid"),"original mesh error context");}};
            Application.logMessageReceived+=capture;
            try{graphic.Meshes(1,new[]{huge});graphic.Meshes(2,new[]{huge});}finally{Application.logMessageReceived-=capture;}
            Mesh read=renderer.GetMesh();Require((!read||read.vertexCount==0)&&errors==3,"huge meshes cleared and indexing exception caught with original context");
            ui.Release(true);Require(ui.IndependentMaterialDict.Count==0&&rect.sizeDelta==Vector2.zero&&!Flag(ui,"mSkinApplyDirty")&&!Flag(ui,"mDyeDirty")&&binding.Handles.All(h=>h.Releases==1),"release removes loaded references before forced unload, clears materials/rectangle/dirty state");
            int demandListeners=graphic.DemandListeners;binding.Events.Clear();Object.DestroyImmediate(ui);
            Require(graphic.MeshListeners==0&&graphic.DemandListeners==demandListeners&&binding.Events.SequenceEqual(new[]{"ref-"}),"original destroy removes mesh check/ref and retains original custom-demand subscription behavior");
            Object.DestroyImmediate(root);Object.DestroyImmediate(nested);Object.DestroyImmediate(canvas);
        }
        public sealed class GraphicProbe:MaskableGraphic,IProfessionAppearanceUIGraphic,IProfessionAppearanceAnimationState,IProfessionAppearanceTrack,IProfessionAppearanceAnimation
        {
            public ProfessionAppearanceSpineValidation.Graph Graph;public List<string> Events;
            public Component Instance=>this;public float MeshScale=>1;public readonly List<CanvasRenderer> Renderers=new List<CanvasRenderer>();public List<CanvasRenderer> canvasRenderers=>Renderers;
            public bool allowMultipleCanvasRenderers{get;set;}public ScriptableObject skeletonDataAsset{get;set;}public string startingAnimation=>"home/idle_01";
            public IProfessionAppearanceAnimationState AnimationState=>this;public IProfessionAppearanceTrack GetCurrent(int track)=>this;public IProfessionAppearanceAnimation Animation=>this;public string Name=>"home/idle_01";
            public IProfessionAppearanceSkeleton Skeleton=>Graph.Skeleton;
            public Material[] Materials;public Texture[] Textures;public Material[] MaterialsMultipleCanvasRenderers=>Materials;public Texture[] TexturesMultipleCanvasRenderers=>Textures;
            public event Action<IProfessionSkinClipGraphic> OnMeshAndMaterialsUpdated;
            public event Action<IProfessionAppearanceUIGraphic> OnCustomDemandLoading;
            public event Action<int,Mesh[],Material[],Texture[]> AssignMeshOverrideMultipleRenderers;
            public int MeshListeners=>AssignMeshOverrideMultipleRenderers?.GetInvocationList().Length??0;public int DemandListeners=>OnCustomDemandLoading?.GetInvocationList().Length??0;
            public void Initialize(bool overwrite){Events.Add("init:"+overwrite);}public void ApplyAnimation(){Events.Add("apply");}
            public void SyncSubmeshGraphicsWithCanvasRenderers(){Events.Add("sync");}public void MatchRectTransformWithBounds(){Events.Add("bounds");}
            public void Demand(){OnCustomDemandLoading?.Invoke(this);}public void Meshes(int count,Mesh[] meshes){AssignMeshOverrideMultipleRenderers?.Invoke(count,meshes,Materials,Textures);OnMeshAndMaterialsUpdated?.Invoke(this);}
            protected override void OnPopulateMesh(VertexHelper helper){helper.Clear();}
        }
        public sealed class Handle:IProfessionUnsafeAssetHandle<Texture>
        {public bool IsValid=>true;public int Status=>2;public Texture Result{get;set;}public int Releases;public void WaitForAsyncComplete(){}public void Release(){Releases++;}public event Action<IProfessionUnsafeAssetHandle<Texture>> Completed{add{value(this);}remove{}}}
        public sealed class Binding:IProfessionAppearanceUIBindings,IProfessionAppearanceUIAssetManager,IProfessionAppearanceTextureBindings
        {
            private readonly Shader shader;public readonly List<string> Events=new List<string>();public readonly List<Handle> Handles=new List<Handle>();public readonly List<Material> Materials=new List<Material>();public Texture Main,Dye;
            public Binding(Shader shader){this.shader=shader;}public IProfessionAppearanceUIAssetManager AssetManager=>this;
            public void AddTextureLoaderRef(YooAssetTextureLoader loader,Object owner){Events.Add("ref+");}public void RemoveTextureLoaderRef(YooAssetTextureLoader loader,Object owner){Events.Add("ref-");}
            public Material GetUIMaterial(){var material=Own(new Material(shader));Materials.Add(material);return material;}
            public bool TryLazyLoadSpineAnimation(IProfessionAppearanceUIGraphic graphic,string name,out object animation){Events.Add("lazy:"+name);animation=null;return true;}
            public void PlayAnimation(IProfessionAppearanceUIGraphic graphic,string name,bool loop){Events.Add("play:"+name+":"+loop);}
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid)where T:Object{Require(typeof(T)==typeof(Texture),"original texture type");var handle=new Handle{Result=guid=="main"?Main:Dye};Handles.Add(handle);return (IProfessionUnsafeAssetHandle<T>)(object)handle;}
            public Material GetAtlasMaterial(ScriptableObject atlas,int index){throw new Exception("No atlas restoration should be requested by this fixture.");}
        }
        private static SpineTextureAssetProxy Proxy(string guid){var proxy=new SpineTextureAssetProxy();JsonUtility.FromJsonOverwrite("{\"mAssetGuid\":\""+guid+"\"}",proxy);return proxy;}
        private static void Set(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        private static bool Flag(object target,string name)=>(bool)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private static T Own<T>(T value)where T:Object{owned.Add(value);return value;}
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
        private static void Finish(int code){SessionState.SetBool(Active,false);ProfessionAppearanceTextureBindings.Current=null;foreach(Object value in owned)if(value)Object.DestroyImmediate(value);owned.Clear();EditorApplication.Exit(code);}
    }
}
