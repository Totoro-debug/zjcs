using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityGridViews;
using Zjcs.Official.World.NativeTerrain.Common;
using Direction = Zjcs.Official.World.NativeTerrain.Common.Direction;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Offline.World.NativeMap13EntityViews;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StorySourceFade;

namespace Zjcs.Editor.Map13EntityGridViews
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13EntityGridViewAcceptance
    {
        private const string Key = "Map13.EntityGridViews";
        private const string SourcePath = "assets/reference/official-map/map13-entity-grid-views-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13EntityGridViewAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
                PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetString(Key + ".source", Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Native Map13 entity state-view verification timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original Map13 entity state-view verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count != 0)
            {
                object next;
                try
                {
                    var step = stack.Peek(); if (!step.MoveNext()) { stack.Pop(); continue; }
                    next = step.Current;
                    if (next is IEnumerator nested && !(next is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (!error) SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            byte[] bytes = File.ReadAllBytes(Path.Combine(Repository, SourcePath));
            Require(Hash(bytes) == SessionState.GetString(Key + ".source", ""), "State-view source changed during entry.");
            var source = JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(bytes));
            foreach (var entry in source.inputs.Concat(source.outputs))
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository, entry.path))) == entry.sha256, "State-view dependency changed: " + entry.path);
            int cases = 0, pixels = 0;
            var models = JsonUtility.FromJson<Models>(File.ReadAllText(Path.Combine(Repository, "assets/reference/official-map/map13-native-tree-models-source-manifest.json")));
            var anchors = JsonUtility.FromJson<Hierarchy>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-attachment-followers.json"));
            var originals = JsonUtility.FromJson<Originals>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-prop-hierarchies.json"));
            var light = new GameObject("Original count-view diagnostic illumination").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
            var camera = new GameObject("Original count-view camera").AddComponent<Camera>(); camera.orthographic = true; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.08f,.08f,0); camera.nearClipPlane = .01f; camera.farClipPlane = 1000; camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(512,512,24); target.Create(); camera.targetTexture = target; var image = new Texture2D(512,512,TextureFormat.RGBA32,false);
            var spriteCatalog = Resources.Load<NativeMap13EntitySpriteCatalog>("Map13/Native/entity-sprite-catalog");
            using (var animations = new NativeMap13AnimatorLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/map13-prop-animators.windows.bundle.bytes"), AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/execution-cases.json"), "65211c5a2dd5a677d26a0f37f88a518c306e7968425048cab263b54e0951bd4f"))
            using (var sprites = new NativeMap13EntitySpriteLibrary(spriteCatalog))
            using (var dissolve = new NativeMap13TreeModelAnimatorLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/model-animator.windows.bundle.bytes"), AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/catalog.json")))
            {
                int row = 0;
                foreach (var model in models.models)
                {
                    var selected = anchors.nodes.Single(n => n.key == model.source); var root = anchors.nodes.Single(n => n.transformKey == selected.parentTransformKey);
                    var all = anchors.nodes.ToDictionary(n => n.transformKey);
                    var nodes = anchors.nodes.Where(n => Belongs(n, root.transformKey, all)).ToArray();
                    var host = new GameObject("Explicit EC entity consumption fixture"); host.SetActive(false);
                    var transforms = new Dictionary<string,Transform>();
                    foreach (var node in nodes) { var go = new GameObject(node.name); go.SetActive(false); transforms[node.transformKey] = go.transform; }
                    foreach (var node in nodes)
                    {
                        var tf = transforms[node.transformKey]; tf.SetParent(node == root ? host.transform : transforms[node.parentTransformKey], false);
                        tf.SetLocalPositionAndRotation(node.pose.m_LocalPosition,node.pose.m_LocalRotation); tf.localScale = node.pose.m_LocalScale; tf.gameObject.layer = node.layer;
                    }
                    UnityEngine.Object.DestroyImmediate(transforms[selected.transformKey].gameObject);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(model.path.Substring(6)); var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,transforms[root.transformKey]);
                    var mapping = new Dictionary<string,UnityEngine.Object>(); var ids = model.original_keys.Select((key,i) => new { key, id = 100001L+i }).ToDictionary(v=>v.id,v=>v.key);
                    foreach (var tf in instance.GetComponentsInChildren<Transform>(true))
                    foreach (var value in new UnityEngine.Object[]{tf.gameObject}.Concat(tf.GetComponents<Component>().Cast<UnityEngine.Object>()))
                    {
                        var original = PrefabUtility.GetCorrespondingObjectFromSource(value);
                        if (original && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long id) && ids.TryGetValue(id,out string key)) mapping[key] = value;
                    }
                    foreach (var node in nodes) if (transforms[node.transformKey]) { mapping[node.key] = transforms[node.transformKey].gameObject; mapping[node.transformKey] = transforms[node.transformKey]; }
                    var binding = instance.GetComponent<NativeMap13EntityModelBinding>();
                    var external = binding.ExternalFadeTargets.ToDictionary(t=>t.GameObjectKey,t=>(GameObject)mapping[t.GameObjectKey]); binding.Prepare(animations,sprites,external,dissolve.Controllers);
                    foreach (var node in nodes) if (transforms[node.transformKey]) transforms[node.transformKey].gameObject.SetActive(node.active);
                    var entityBinding=host.AddComponent<FixtureBinding>();var movement=host.AddComponent<FixtureMovement>();
                    host.SetActive(true); binding.InitializeActive(); yield return null;
                    var driver = originals.objects.Single(o=>o.type=="ECViewDriver" && o.references.Any(v=>v.field==".m_GameObject"&&Identity(v)==root.key));var data=driver.fields.references.RefIds.Single(v=>v.type.@class=="ECGridTransformView").data;
                    Require(data.mFlipTransform.m_PathID==0&&data.mClientOverrideEntityDimensionBounds.Length==0,"Original tree now requires nonempty flip/body binding.");var grid=new ECGridTransformView();Set(grid,"mUpdatePosImmediately",data.mUpdatePosImmediately!=0);Set(grid,"mSetZ",data.mSetZ!=0);Set(grid,"mOffsetZ",data.mOffsetZ);Set(grid,"mOffsetZPerX",data.mOffsetZPerX);Set(grid,"mClientOverrideEntityDimensionBounds",new List<Pos2d>());
                    var entity=new EntityInput();entityBinding.Data=entity;var bridge=new BridgeInput(transforms[root.transformKey].gameObject,grid);((IECView)grid).Init(bridge);((IECView)grid).Load(entity,false);
                    var q=entity.Grid;Require(Vector3.Distance(grid.GridToPosition(new Pos2d(2,4)),new Vector3(-17.5f,-25.5f,0))<.0001f,"Original grid plus fractional center differs.");Require(grid.PositionToGrid(new Vector3(-17.5f,-25.5f,0))==new Pos2d(2,4),"Original truncated center conversion differs.");cases+=2;
                    grid.ResetPosition();Require(Vector3.Distance(host.transform.localPosition,new Vector3(-17.5f,-25.5f,-26.018f))<.0001f,"Original source depth/center/x offset differs.");cases++;
                    movement.Moving=true;int events=0;Pos2d previous=default;grid.OnPosUpdated+=(v,pre)=>{Require(!movement.Moving&&movement.Stops==1,"Original movement must stop before position event.");events++;previous=pre;};q.Pos=new Pos2d(3,5);q.PosUpdatedEvent.FireEvent(q,new Pos2d(2,4));Require(events==1&&previous==new Pos2d(2,4)&&Vector3.Distance(host.transform.localPosition,new Vector3(-16.5f,-24.5f,-25.017f))<.0001f,"Original immediate grid update/event differs.");cases++;
                    foreach(Direction dir in Enum.GetValues(typeof(Direction))){q.Direction=dir;grid.ResetRotation();float angle=dir==Direction.Up?0:dir==Direction.Left?90:dir==Direction.Down?180:270;Require(Quaternion.Angle(host.transform.localRotation,Quaternion.Euler(0,0,angle))<.001f,"Original direction rotation differs.");cases++;}
                    q.FlipX=true;grid.ResetScale();Require(host.transform.localScale==new Vector3(-1,1,1),"Original root flip differs.");q.FlipX=false;grid.ResetScale();Require(host.transform.localScale==Vector3.one,"Original root unflip differs.");cases+=2;host.transform.localRotation=Quaternion.identity;
                    Require(ReferenceEquals(grid.GetClientChooseBodyRange(),q.Info.GetBodyRange()),"Original body-range fallback identity differs.");var ranges=new List<Pos2d>{new Pos2d(8,9)};Set(grid,"mClientOverrideEntityDimensionBounds",ranges);Require(ReferenceEquals(grid.GetClientChooseBodyRange(),ranges),"Original client override identity differs.");cases+=2;
                    Require(ECGridTransformView.GetFlipX(Vector3.zero,new Vector3(0,9,0))==null&&ECGridTransformView.GetFlipX(Vector3.zero,Vector3.left)==true&&ECGridTransformView.GetFlipX(Vector3.zero,Vector3.right)==false,"Original horizontal flip tri-state differs.");Require(ECGridTransformView.GridToVector3(new Pos2d(3,-4))==new Vector3(3,-4,0),"Original unit grid vector differs.");cases+=4;
                    var flip=new GameObject("Explicit flip-branch diagnostic target");flip.transform.SetParent(host.transform,false);flip.transform.localScale=new Vector3(2,3,4);Set(grid,"mFlipTransform",flip.transform);grid.SetScaleByFlipX(true);grid.SetScaleByFlipX(true);Require(flip.transform.localScale==new Vector3(-2,3,4),"Original flip must be idempotent and preserve magnitude.");grid.SetScaleByFlipX(false);Require(flip.transform.localScale==new Vector3(2,3,4),"Original child unflip differs.");cases+=2;
                    var transformView=new ECTransformView();((IECView)transformView).Init(bridge);((IECView)transformView).Load(entity,false);Set(grid,"mECTransformView",transformView);int positionEvents=0;transformView.OnPosUpdated+=v=>positionEvents++;grid.SetPosition(new Vector3(4,5,6));Require(positionEvents==1&&transformView.Position==host.transform.localPosition&&Mathf.Abs(transformView.Position.z-4.5035f)<.0001f,"Original transform-view forwarding/depth differs.");cases++;
                    Set(transformView,"mSetZ",true);transformView.Position=new Vector3(1,2,9);Require(transformView.Position==new Vector3(1,2,2),"Original transform source z override differs.");cases++;Set(transformView,"mSetZ",false);Set(grid,"mECTransformView",null);
                    // Check original ECTransform initialization when no grid view is present.
                    var transformBridge=new BridgeInput(transforms[root.transformKey].gameObject,null);var standalone=new ECTransformView();((IECView)standalone).Init(transformBridge);((IECView)standalone).Load(entity,false);Require(host.transform.localPosition==entity.Transform.Position&&host.transform.localScale==entity.Transform.Scale&&Quaternion.Angle(host.transform.localRotation,entity.Transform.Rotation)<.001f,"Original non-grid transform initialization differs.");cases++;((IECView)standalone).Unload();
                    host.transform.localScale=Vector3.one;host.transform.localRotation=Quaternion.identity;grid.ResetPosition();var bounds=binding.Skeletons.Single().View.LocalBounds;var skeleton=binding.Skeletons.Single().transform;camera.transform.position=skeleton.TransformPoint(bounds.center)+new Vector3(0,0,-50);camera.orthographicSize=Mathf.Max(bounds.extents.y*skeleton.lossyScale.y,bounds.extents.x*skeleton.lossyScale.x)*1.3f;
                    yield return null;camera.Render();var active=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=active;int count=image.GetPixels32().Count(v=>v.a>0);Require(count>100,"Original grid model render is empty.");pixels+=count;File.WriteAllBytes(Path.Combine(Output,"native-grid-view-"+row+".png"),image.EncodeToPNG());
                    ((IECView)grid).Unload();var position=host.transform.localPosition;q.Pos=new Pos2d(99,99);q.PosUpdatedEvent.FireEvent(q,new Pos2d(3,5));Require(host.transform.localPosition==position&&events==1,"Original grid unload retained event receiver.");cases++;((IECView)transformView).Unload();UnityEngine.Object.Destroy(host);yield return null;row++;
                }
            }
            Require(cases==44&&!error,"Original grid coverage/errors differ: "+cases);var proof=new Proof{source_sha256=Hash(bytes),editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),cases=cases,rendered_pixels=pixels,play_mode=Application.isPlaying,coverage="Complete original grid/transform source views with both source tree configurations, actual native transforms, movement-stop/event order, fractional-center/depth conversion, directions, flip/body-range branches, transform forwarding, unload and original models rendered. Ground/body/movement are explicit diagnostic inputs; formal map input/save/Player pending."};File.WriteAllText(Path.Combine(Output,"native-entity-grid-views-execution.json"),JsonUtility.ToJson(proof,true));Debug.Log("Map13 original grid/transform views passed: "+cases+" checks, "+pixels+" pixels.");
        }
        private sealed class EntityInput:ECViewEntityData
        {public readonly Map13GridTransformData Grid=new Map13GridTransformData{Pos=new Pos2d(2,4),Info=new BodyInfo{RealCenter=new Vector2(.5f,.5f)},GridGround=new Ground()};public readonly Map13TransformData Transform=new Map13TransformData{Position=new Vector3(7,8,9),Rotation=Quaternion.Euler(0,0,45),Scale=new Vector3(2,3,1)};public override int ClassId=>8363;public override EcEntityIdentifier Id=>default;public override T GetComponent<T>()=>typeof(T)==typeof(Map13GridTransformData)?(T)(object)Grid:typeof(T)==typeof(Map13TransformData)?(T)(object)Transform:default;}
        private sealed class Ground:Map13GridGroundData{public override Vector3 GridToPos(Pos2d grid)=>new Vector3(-20+grid.X,-30+grid.Y,0);public override Pos2d PosToGrid(Vector3 position)=>new Pos2d(Mathf.FloorToInt(position.x+20),Mathf.FloorToInt(position.y+30));}
        private sealed class BodyInfo:Map13GridTransformInfo{private readonly List<Pos2d> body=new List<Pos2d>{new Pos2d(0,0),new Pos2d(1,0)};public override List<Pos2d> GetBodyRange()=>body;}
        public sealed class FixtureMovement:Map13MovementAgent{public bool Moving;public int Stops;public override bool IsMoving=>Moving;public override void Stop(){Moving=false;Stops++;}}
        public sealed class FixtureBinding:ECViewEntityBinding{public ECViewEntityData Data;public override ECViewEntityData Entity=>Data;public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value){}public override void AddStartReceiver(IMyEntityStartEventReceiver value){}public override void RemoveStartReceiver(IMyEntityStartEventReceiver value){}public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value){}public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value){}public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value){}public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value){}public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value){}public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value){}}
        private sealed class BridgeInput:IECViewBridge{private readonly ECGridTransformView grid;public BridgeInput(GameObject owner,ECGridTransformView value){GameObject=owner;grid=value;}public GameObject GameObject{get;}public IServiceProvider ServiceProvider=>null;public CancellationToken DestroyCancellationToken=>GameObject.GetCancellationTokenOnDestroy();public IClientVisibilitySupporter ClientVisibilitySupporter=>null;public IEntityFadeAnimationBridge EntityFadeAnimationBridge=>null;public T GetView<T>()where T:class,IECView=>grid as T;public long StartCoroutine(IEnumerator routine)=>throw new NotSupportedException();public void StopCoroutine(long id)=>throw new NotSupportedException();}
        private static bool Belongs(Node node,string root,Dictionary<string,Node> nodes){while(true){if(node.transformKey==root)return true;if(string.IsNullOrEmpty(node.parentTransformKey))return false;node=nodes[node.parentTransformKey];}}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        private static void SetEmptyList(object target,string field){var f=target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance);f.SetValue(target,Activator.CreateInstance(f.FieldType));}
        private static bool HasSubscription(object target,string field,object subscriber)=>((Delegate)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target))?.GetInvocationList().Any(d=>ReferenceEquals(d.Target,subscriber))??false;
        private static void CheckCount(List<GameObject> items,long count){for(int i=0;i<items.Count;i++)Require(items[i].activeSelf==(i<count),"Original apple count/order differs at "+i);}
        private static SpriteRendererList.State FadeState(SpriteRendererList list)=>(SpriteRendererList.State)typeof(SpriteRendererList).GetField("mCurrentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(list);
        private static string Identity(Reference reference)=>reference.cab.ToLowerInvariant()+":"+reference.path_id;
        private static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Runner:MonoBehaviour{}
        [Serializable]private sealed class Source{public Entry[] inputs,outputs;}
        [Serializable]private sealed class Entry{public string path,sha256;}
        [Serializable]private sealed class Models{public Model[] models;}
        [Serializable]private sealed class Model{public string source,path;public string[] original_keys;}
        [Serializable]private sealed class Hierarchy{public Node[] nodes;}
        [Serializable]private sealed class Node{public string key,transformKey,parentTransformKey,name;public int layer;public bool active;public Pose pose;}
        [Serializable]private sealed class Pose{public Vector3 m_LocalPosition,m_LocalScale;public Quaternion m_LocalRotation;}
        [Serializable]private sealed class Originals{public Original[] objects;}
        [Serializable]private sealed class Original{public string key,type;public Fields fields;public Reference[] references;}
        [Serializable]private sealed class Reference{public string field,cab,path_id;}
        [Serializable]private sealed class Fields{public ManagedRefs references;}
        [Serializable]private sealed class ManagedRefs{public RefId[] RefIds;}
        [Serializable]private sealed class RefId{public RefType type;public RefData data;}
        [Serializable]private sealed class RefType{public string @class;}
        [Serializable]private sealed class RefData{public Pointer[] mItemGos,mItemCountRangeConfigs;public int mUpdatePosImmediately,mSetZ;public float mOffsetZ,mOffsetZPerX;public Pointer mFlipTransform;public Pos2d[] mClientOverrideEntityDimensionBounds;public Pointer mSpriteRendererList,mAnimator;public AttachConfigs Configs;}
        [Serializable]private sealed class Pointer{public long m_PathID;}
        [Serializable]private sealed class AttachConfigs{public AttachConfig[] Values;}
        [Serializable]private sealed class AttachConfig{public string BoneName;public Pointer Bone;}
        [Serializable]private sealed class Proof{public string source_sha256,editor,backend,coverage;public int cases,rendered_pixels;public bool play_mode;}
    }
}
