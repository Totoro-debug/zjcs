using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionFollowerValidation
    {
        private const string Active="Zjcs.ProfessionFollower.Active";private static bool ready;
        static ProfessionFollowerValidation(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;};}
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
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+".Deadline",0))throw new TimeoutException("Follower validation timed out.");if(!ready||!EditorApplication.isPlaying)return;
                Check();Debug.Log("Game-owned attachment following passed in Play Mode: original parent/local versus external/world transforms, RectTransform bone scaling, custom/draw-order Z and history, rotation/reflection/scale flags, attachment identity visibility, UI/world visibility and spacing, rebuild subscriptions, clear/disable/destroy lifecycle, effect/skeleton part initialization, original animation mapping/cache and failed-initialization state. Bone/slot/graphic/resource endpoints are fixtures; native production actor and full profession UI remain unbound.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check()
        {
            var target=new GameObject("Native follower endpoint fixture");var graphic=target.AddComponent<GraphicProbe>();
            target.transform.position=new Vector3(10,20,4);target.transform.localScale=new Vector3(2,3,5);target.transform.rotation=Quaternion.Euler(0,0,30);
            var go=new GameObject("Original UI follower fixture",typeof(RectTransform));go.SetActive(false);go.transform.SetParent(target.transform,false);var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(4,5);
            var follower=go.AddComponent<SkeletonGraphicFollower>();follower.SkeletonGraphic=graphic;follower.SlotName="slot";go.SetActive(true);follower.ForceUpdate();
            Vector(rect.localPosition,new Vector3(8,15,2.5f),"direct parent: RectTransform-sized bone position and UI custom Z x100");Angle(rect.localRotation,Quaternion.Euler(0,0,90),"direct parent rotation from bone axes");Vector(rect.localScale,new Vector3(-2,3,1),"original local scale");Require(graphic.Listeners==1,"Awake/enable registers exactly one rebuild callback");
            Require(ReferenceEquals(follower.Bone,graphic.Data.Bone)&&ReferenceEquals(follower.SkeletonAnimation,graphic),"source bone fallback from slot and animation endpoint identity");
            follower.FollowBoneWorldFlip=true;follower.ForceUpdate();Vector(rect.localScale,new Vector3(-2,3,1),"positive matrix determinant retains absolute Y scale");
            graphic.Data.Bone.B=1;follower.ForceUpdate();Vector(rect.localScale,new Vector3(-2,-3,1),"negative matrix determinant flips Y using absolute local scale");
            follower.FollowLocalScale=false;follower.FollowSkeletonTransformFlipX=false;rect.localScale=Vector3.one;follower.ForceUpdate();Vector(rect.localScale,Vector3.one,"both scale flags false preserve scale");
            follower.FollowSkeletonTransformFlipX=true;target.transform.localScale=new Vector3(-2,3,5);follower.ForceUpdate();Vector(rect.localScale,new Vector3(2,-3,1),"skeleton transform flip X applies independently of FollowLocalScale");
            target.transform.localScale=new Vector3(2,3,5);graphic.Data.Bone.D=0;follower.FollowBoneWorldFlip=false;follower.FollowLocalScale=true;follower.FollowSkeletonTransformFlipX=false;
            var external=new GameObject("External parent");external.transform.position=new Vector3(-20,-10,2);rect.SetParent(external.transform,false);follower.Initialize();follower.ForceUpdate();
            Vector(rect.position,new Vector3(target.transform.TransformPoint(new Vector3(8,15,0)).x,target.transform.TransformPoint(new Vector3(8,15,0)).y,16.5f),"external parent world transform with absolute Z scale");Angle(rect.rotation,Quaternion.Euler(0,0,120),"external world rotation combines skeleton and bone degrees");
            target.transform.localScale=new Vector3(-2,3,5);follower.ForceUpdate();Angle(rect.rotation,Quaternion.Euler(0,0,-60),"reflected skeleton transform reverses bone world angle");
            graphic.Custom=false;follower.ZFront=false;follower.Initialize();follower.ForceUpdate();Close(rect.position.z,6.5f,"draw-order slot index one, back half spacing");
            graphic.Data.Order=new IProfessionFollowerSlot[]{graphic.Data.Slot,graphic.Data.Other};follower.ForceUpdate();Close(rect.position.z,1.5f,"changed draw order searches and caches slot index zero");
            graphic.Data.Order=new IProfessionFollowerSlot[]{graphic.Data.Other,graphic.Data.Slot};follower.ForceUpdate();Close(rect.position.z,6.5f,"slot history restores previous index");
            follower.FollowZSpacing=false;follower.FollowRotation=false;rect.rotation=Quaternion.Euler(0,0,17);follower.ForceUpdate();Close(rect.position.z,4,"disabled Z follows transform point");Angle(rect.rotation,Quaternion.Euler(0,0,17),"disabled rotation preserves world rotation");
            graphic.Mode=0;graphic.Invisible=0;var previous=rect.position;graphic.Data.Bone.X=40;follower.ForceUpdate();Vector(rect.position,previous,"matching invisible mode prevents follow");graphic.Invisible=3;follower.ForceUpdate();Require(rect.position!=previous,"always-update mode 3 follows");
            previous=rect.position;graphic.Data.Bone.Active=false;graphic.Data.Bone.X=50;follower.ForceUpdate();Vector(rect.position,previous,"inactive bone prevents follow");graphic.Data.Bone.Active=true;
            var visible=new GameObject("Attachment gated object");visible.transform.SetParent(go.transform,false);follower.Gos=new[]{visible};follower.AttachmentName="held";graphic.Data.Slot.Value=new object();follower.Initialize();follower.ForceUpdate();Require(!visible.activeSelf,"different attachment identity disables followed objects");
            graphic.Data.Slot.Value=graphic.Data.Attachment;follower.ForceUpdate();Require(visible.activeSelf,"matching non-null attachment enables followed objects");graphic.Data.Slot.Value=null;follower.ForceUpdate();Require(!visible.activeSelf,"null current attachment disables followed objects");
            go.SetActive(false);Require(graphic.Listeners==0,"disable unregisters rebuild");go.SetActive(true);Require(graphic.Listeners==1,"reenable registers once");var changed=new SkeletonData();graphic.Data=changed;graphic.Rebuild();Require(ReferenceEquals(follower.Bone,changed.Bone),"rebuild initializes against new skeleton identity");
            follower.Clear();Require(graphic.Listeners==0&&follower.Bone==null&&follower.SkeletonAnimation==null&&follower.SkeletonGraphic==null,"clear unregisters and clears original endpoints");
            Object.DestroyImmediate(go);Object.DestroyImmediate(external);
            var worldGo=new GameObject("Original world follower fixture");worldGo.SetActive(false);var world=worldGo.AddComponent<SkeletonRendererFollower>();world.SkeletonRenderer=graphic;world.SlotName="slot";worldGo.SetActive(true);world.ForceUpdate();Close(world.transform.position.z,4.075f,"world spacing retains source units without UI x100");
            var effect=new GameObject("Original effect part fixture").AddComponent<AppearanceEffectPart>();effect.Follower=world;effect.Init(target);world.ForceUpdate();Require(ReferenceEquals(effect.SkeletonFollower,world),"effect delegates target initialization and force update to original follower");Object.DestroyImmediate(effect.gameObject);Object.DestroyImmediate(worldGo);Require(graphic.Listeners==0,"destroy unregisters world follower");
            CheckSkeletonPart();Object.DestroyImmediate(target);
        }
        private static void CheckSkeletonPart()
        {
            var go=new GameObject("Original animated part fixture");var animated=go.AddComponent<AnimatedProbe>();var part=go.AddComponent<AppearanceSkeletonPart>();part.Skeleton=go;part.SkinType=SkinType.backDecoration;var bindings=new PartBindings();part.ConfigurePart(bindings);
            Require(part.HasAnim&&part.AnimDict.Count==2&&ReferenceEquals(part.AnimDict["walk"],bindings.Config.GetPartConfig(SkinType.backDecoration).AnimConfigList[0]),"original target-name lookup expands source animation keys and retains config identity");
            part.TryPlayAnim("walk");part.TryPlayAnim("unknown");Require(animated.Last=="walk:True","original application passes requested source name, not mapped target name, and loops");
            animated.Names=Array.Empty<string>();Require(part.HasAnim&&part.AnimDict.Count==2,"animation dictionary remains cached");
            var failed=go.AddComponent<AppearanceSkeletonPart>();failed.Skeleton=go;failed.SkinType=SkinType.hair;failed.ConfigurePart(bindings);animated.Names=new[]{"target"};bool threw=false;try{_ = failed.HasAnim;}catch(NullReferenceException){threw=true;}
            Require(threw&&!failed.HasAnim,"source initialization flag is set before missing configuration error and prevents retry");
            var empty=go.AddComponent<AppearanceSkeletonPart>();empty.Skeleton=go;empty.ConfigurePart(bindings);animated.Names=Array.Empty<string>();Require(!empty.HasAnim,"empty native animation list returns before configuration access");
            Object.DestroyImmediate(go);Object.DestroyImmediate(bindings.Config);
        }
        public sealed class Bone:IProfessionFollowerBone
        {
            public bool Active{get;set;}=true;public float X=2,Y=3;public float WorldX=>X;public float WorldY=>Y;public float WorldRotationX=>90;public float ScaleX=>-2;public float ScaleY=>3;public float A=>0;public float B{get;set;}=-1;public float C=>1;public float D{get;set;}=0;
        }
        public sealed class Slot:IProfessionFollowerSlot
        {public IProfessionFollowerBone Bone{get;set;}public int Index=>1;public float zOrder=>2;public object Value;public object Attachment=>Value;}
        public sealed class SkeletonData:IProfessionFollowerSkeleton
        {
            public readonly Bone Bone=new Bone();public readonly Slot Slot=new Slot(),Other=new Slot();public readonly object Attachment=new object();public IProfessionFollowerSlot[] Order;
            public SkeletonData(){Slot.Bone=Bone;Slot.Value=Attachment;Other.Bone=Bone;Order=new IProfessionFollowerSlot[]{Other,Slot};}
            public IProfessionFollowerSlot[] DrawOrder=>Order;public IProfessionFollowerBone FindBone(string name)=>name=="bone"?Bone:null;public IProfessionFollowerSlot FindSlot(string name)=>name=="slot"?Slot:null;public object GetAttachment(int index,string name)=>index==1&&name=="held"?Attachment:null;
        }
        public sealed class GraphicProbe:MonoBehaviour,IProfessionFollowerGraphic,IProfessionFollowerRenderer
        {
            public SkeletonData Data=new SkeletonData();public IProfessionFollowerSkeleton Skeleton=>Data;public bool Custom=true;public int Mode,Invisible=3;public bool UseCustomZSpacing=>Custom;public int UpdateWhenInvisible=>Invisible;public int UpdateMode=>Mode;public float ZSpacing=>0.01f;
            private Action<IProfessionFollowerAnimation> rebuild;public int Listeners=>rebuild?.GetInvocationList().Length??0;
            public event Action<IProfessionFollowerAnimation> OnAnimationRebuild{add{rebuild+=value;}remove{rebuild-=value;}}
            public void Rebuild(){rebuild?.Invoke(this);}
        }
        public sealed class AnimatedProbe:MonoBehaviour,IProfessionPartAnimatedSkeleton
        {public string[] Names={"target","unused"};public string Last;public string[] AnimationNames=>Names;public void PlayAnimation(string name,bool loop){Last=name+":"+loop;}}
        private sealed class PartBindings:IProfessionAppearancePartBindings
        {
            public AppearanceConfig Config{get;}=ScriptableObject.CreateInstance<AppearanceConfig>();
            public PartBindings(){typeof(AppearanceConfig).GetField("mPartConfigs",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Config,new List<AppearancePartConfig>{new AppearancePartConfig{SkinType=SkinType.backDecoration,AnimConfigList=new List<PartAnimConfig>{new PartAnimConfig{SourceAnim="walk",TargetAnim="target"},new PartAnimConfig{SourceAnim="run",TargetAnim="target"}}}});}
            public bool HasEffect(string name)=>throw new InvalidOperationException("No resource operations in follower fixture.");public IProfessionAppearancePartAsset LoadPrefab(string path)=>throw new InvalidOperationException("No resource operations in follower fixture.");public GameObject Instantiate(GameObject prefab,Transform parent,bool world)=>throw new InvalidOperationException("No resource operations in follower fixture.");
        }
        private static void Vector(Vector3 actual,Vector3 expected,string reason)=>Require((actual-expected).sqrMagnitude<0.00001f,reason+" "+actual+" != "+expected);
        private static void Angle(Quaternion actual,Quaternion expected,string reason)=>Require(Quaternion.Angle(actual,expected)<0.001f,reason);
        private static void Close(float actual,float expected,string reason)=>Require(Mathf.Abs(actual-expected)<0.0001f,reason+" "+actual+" != "+expected);
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("Follower validation failed: "+reason);}
        private static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.Exit(code);}
    }
}
