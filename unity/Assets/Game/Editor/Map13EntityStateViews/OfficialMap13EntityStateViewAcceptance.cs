using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityStateViews;
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

namespace Zjcs.Editor.Map13EntityStateViews
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13EntityStateViewAcceptance
    {
        private const string Key = "Map13.EntityStateViews";
        private const string SourcePath = "assets/reference/official-map/map13-entity-state-views-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13EntityStateViewAcceptance()
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
            int queries = VerifySourceQueries(), originalAnimators = 0, originalAttachments = 0, countCases = 0, fadeCycles = 0, pixels = 0;
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
                    host.SetActive(true); binding.InitializeActive(); yield return null;
                    var driver = originals.objects.Single(o=>o.type == "ECViewDriver" && o.references.Any(p=>p.field == ".m_GameObject" && Identity(p)==root.key));
                    string cab = driver.key.Split(':')[0]; var refs = driver.fields.references.RefIds;
                    var entity = new EntityInput(); var bridge = new BridgeInput(transforms[root.transformKey].gameObject);
                    var attachData = refs.Single(r=>r.type.@class == "ECAttachAgentView").data;
                    var attachment = new ECAttachAgentView { Configs = new ECAttachAgentView.AttachAgentConfigs { Values = attachData.Configs.Values.Select(c=>new ECAttachAgentView.AttachAgentConfig { BoneName=c.BoneName,Bone=(Transform)mapping[cab+":"+c.Bone.m_PathID] }).ToList() } };
                    ((IECView)attachment).Init(bridge); ((IECView)attachment).Load(entity,false);
                    var agent = host.GetComponent<AttachAgent>(); Require(agent, "Original attachment view did not create the registry on the entity parent.");
                    foreach (var config in attachment.Configs.Values) { Require(agent.GetBone(config.BoneName)==config.Bone,"Original attachment registration differs."); originalAttachments++; }
                    foreach (var item in refs.Where(r=>r.type.@class == "ECAnimatorView"))
                    {
                        var animator = new ECAnimatorView(); var expected = (Animator)mapping[cab+":"+item.data.mAnimator.m_PathID]; Set(animator,"mAnimator",expected);
                        ((IECView)animator).Init(bridge); ((IECView)animator).Load(entity,false); yield return null;
                        Require(animator.AnimationController && animator.AnimationController == host.GetComponent<Zjcs.Official.StoryAnimation.AnimationController>() && animator.AnimationController.myAnimator == expected && animator.AnimationController.myInited,"Original animator view initialization/parent/controller differs.");
                        ((IECView)animator).Unload(); originalAnimators++;
                    }
                    var data = refs.Single(r=>r.type.@class == "ECMapAwardItemCountSwitcherView").data;
                    Require(data.mItemCountRangeConfigs.Length == 0,"Source tree now contains range rules; extend source binding.");
                    var items = data.mItemGos.Select(p=>(GameObject)mapping[cab+":"+p.m_PathID]).ToList(); Require(items.Count==10,"Original apple inventory differs.");
                    var list = (SpriteRendererList)mapping[cab+":"+data.mSpriteRendererList.m_PathID];
                    var countView = new ECMapAwardItemCountSwitcherView(); Set(countView,"mItemGos",items); SetEmptyList(countView,"mItemCountRangeConfigs"); Set(countView,"mSpriteRendererList",list); ((IECView)countView).Init(bridge);
                    foreach (long count in new long[]{0,1,4,10,13})
                    {
                        entity.Awards.Info.AwardItemInfos[0] = new StageSingleAwardItemInfo { ItemId=1001,Count=count };
                        ((IECView)countView).Load(entity,false); CheckCount(items,count); countCases++;
                        int events = 0; Action done = ()=>events++; list.OnFadeOutCompleted += done; list.OnFadeInCompleted += done;
                        list.FadeOut(.04f); CheckCount(items,0); for(int frame=0;events<1&&frame<60;frame++)yield return null;
                        Require(events==1 && list.Alpha==0,"Original count-view fade-out did not complete.");
                        list.FadeIn(.04f); CheckCount(items,0); for(int frame=0;(events<2 || FadeState(list)!=SpriteRendererList.State.FadeIned)&&frame<60;frame++)yield return null;
                        Require(events==2 && list.Alpha==1,"Original count-view fade-in did not complete: count="+count+", events="+events+", alpha="+list.Alpha+", active="+list.isActiveAndEnabled+", in="+list.IsIn+", state="+typeof(SpriteRendererList).GetField("mCurrentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(list)); CheckCount(items,count); fadeCycles++;
                        list.OnFadeOutCompleted -= done; list.OnFadeInCompleted -= done; ((IECView)countView).Unload();
                        Require(!HasSubscription(list,"OnFadeInCompleted",countView) && !HasSubscription(list,"OnFadeInStart",countView) && !HasSubscription(list,"OnFadeOutStart",countView),"Original count-view Unload left event subscriptions.");
                    }
                    entity.Awards.AwardItemInfos = new List<StageSingleAwardItemInfo>{new StageSingleAwardItemInfo{ItemId=2001,Count=4}};
                    ((IECView)countView).Load(entity,false); CheckCount(items,4); countCases++; yield return null;
                    var bounds = binding.Skeletons.Single().View.LocalBounds; var skeleton = binding.Skeletons.Single().transform;
                    camera.transform.position = skeleton.TransformPoint(bounds.center)+new Vector3(0,0,-50); camera.orthographicSize = Mathf.Max(bounds.extents.y*skeleton.lossyScale.y,bounds.extents.x*skeleton.lossyScale.x)*1.3f;
                    yield return null; camera.Render(); var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=previous;
                    int visible=image.GetPixels32().Count(c=>c.a>0);Require(visible>100,"Original count-view rendered no pixels.");pixels+=visible;File.WriteAllBytes(Path.Combine(Output,"native-count-view-"+row+".png"),image.EncodeToPNG());
                    ((IECView)countView).Unload(); ((IECView)attachment).Unload(); UnityEngine.Object.Destroy(host); yield return null; row++;
                }
            }
            Require(originalAnimators==4 && originalAttachments>20 && countCases==12 && fadeCycles==10 && !error,"Original state-view coverage/errors differ.");
            var proof=new Proof{source_sha256=Hash(bytes),queries=queries,original_animators=originalAnimators,original_attachments=originalAttachments,count_cases=countCases,fade_cycles=fadeCycles,rendered_pixels=pixels,editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),play_mode=Application.isPlaying,coverage="Original source attachment/animator/reward-count views run through IECView.Init/Load/Unload with exact original Model sprites/anchors and fade coroutines. State/config/result readers use explicit diagnostic inputs; outer EC/UI, gameplay/state-event host, input/save and Player are pending."};
            File.WriteAllText(Path.Combine(Output,"native-entity-state-views-execution.json"),JsonUtility.ToJson(proof,true));Debug.Log("Map13 original state views passed: "+queries+" queries, "+originalAnimators+" animators, "+originalAttachments+" attachments, "+countCases+" counts, "+fadeCycles+" fade cycles.");
        }
        private static int VerifySourceQueries()
        {
            int cases=0;var sub=new SubStageStateInfo{Conditions=new List<StageCompCfg>{new StageCompCfg{CompType=StageComponentType.ResultAwardItem,CompIndex=5}},Behaviour=new StageCompCfg{CompType=StageComponentType.ResultAwardItem,CompIndex=6},Results=new List<StageCompCfg>{new StageCompCfg{CompType=StageComponentType.ResultAwardItem,CompIndex=7}}};
            var query=new Map13WorldStageQuery{Info=new Map13StageStateQueryData{StateInfos=new List<StageStateInfo>{new StageStateInfo{SubStateList=new List<SubStageStateInfo>{sub}}}}};
            Require(query.GetStateComponentIndex(StageComponentType.ResultAwardItem)==5,"Source condition priority differs.");cases++;sub.Conditions.Clear();Require(query.GetStateComponentIndex(StageComponentType.ResultAwardItem)==6,"Source behaviour priority differs.");cases++;
            sub.Behaviour.CompType=StageComponentType.BehTouch;Require(query.GetStateComponentIndex(StageComponentType.ResultAwardItem)==7,"Source result priority differs.");cases++;Require(query.GetStateComponentIndex(StageComponentType.None)==-1&&query.GetStateComponentIndex(StageComponentType.ResultAwardItem,1)==-1,"Source missing/none substate guards differ.");cases+=2;
            query.State=-1;Require(query.GetCurStateInfo()==null&&query.GetStateComponentIndex(StageComponentType.ResultAwardItem)==-1,"Source negative state guard differs.");cases++;query.State=1;Require(query.GetCurStateInfo()==null,"Source state upper bound differs.");cases++;
            var awards=new Map13AwardItemQuery{Info=new Map13AwardItemQueryData{AwardItemInfos=new List<StageSingleAwardItemInfo>{new StageSingleAwardItemInfo{ItemId=1001,Count=3}}}};
            Require(awards.TryGetAwardItemInfo(0,out var result)&&result.ItemId==1001&&result.Count==3,"Source config award fallback differs.");cases++;
            awards.AwardItemInfos=new List<StageSingleAwardItemInfo>{new StageSingleAwardItemInfo{ItemId=2001,Count=7}};Require(awards.TryGetAwardItemInfo(0,out result)&&result.ItemId==2001&&result.Count==7,"Source explicit result priority differs.");cases++;
            awards.AwardItemInfos[0]=default;Require(awards.TryGetAwardItemInfo(0,out result)&&result.ItemId==1001&&result.Count==3,"Source empty result fallback differs.");cases++;Require(!awards.TryGetAwardItemInfo(1,out result),"Source award upper bound differs.");cases++;
            awards.AwardItemInfos=null;Require(!awards.TryGetAwardItemInfo(-1,out result),"Source config negative index guard differs.");cases++;
            awards.AwardItemInfos=new List<StageSingleAwardItemInfo>();bool threw=false;try{awards.TryGetAwardItemInfo(-1,out result);}catch(ArgumentOutOfRangeException){threw=true;}Require(threw,"Source override-list negative index ordering changed.");cases++;
            return cases;
        }
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
        private sealed class EntityInput:ECViewEntityData
        {
            public readonly Map13WorldStageQuery Stage=new Map13WorldStageQuery{Info=new Map13StageStateQueryData{StateInfos=new List<StageStateInfo>{new StageStateInfo{SubStateList=new List<SubStageStateInfo>{new SubStageStateInfo{Results=new List<StageCompCfg>{new StageCompCfg{CompType=StageComponentType.ResultAwardItem,CompIndex=0}}}}}}}};
            public readonly Map13AwardItemQuery Awards=new Map13AwardItemQuery{Info=new Map13AwardItemQueryData{AwardItemInfos=new List<StageSingleAwardItemInfo>{new StageSingleAwardItemInfo{ItemId=1001,Count=0}}}};
            public override int ClassId=>8363;public override EcEntityIdentifier Id=>default;
            public override T GetComponent<T>()=>typeof(T)==typeof(Map13WorldStageQuery)?(T)(object)Stage:typeof(T)==typeof(Map13AwardItemQuery)?(T)(object)Awards:default;
        }
        private sealed class BridgeInput:IECViewBridge
        {
            public BridgeInput(GameObject owner){GameObject=owner;}public GameObject GameObject{get;}public IServiceProvider ServiceProvider=>null;public CancellationToken DestroyCancellationToken=>GameObject.GetCancellationTokenOnDestroy();public IClientVisibilitySupporter ClientVisibilitySupporter=>null;public IEntityFadeAnimationBridge EntityFadeAnimationBridge=>null;
            public T GetView<T>()where T:class,IECView=>null;public long StartCoroutine(IEnumerator routine)=>throw new InvalidOperationException("Unexpected EC coroutine");public void StopCoroutine(long id)=>throw new InvalidOperationException("Unexpected EC coroutine");
        }
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
        [Serializable]private sealed class RefData{public Pointer[] mItemGos,mItemCountRangeConfigs;public Pointer mSpriteRendererList,mAnimator;public AttachConfigs Configs;}
        [Serializable]private sealed class Pointer{public long m_PathID;}
        [Serializable]private sealed class AttachConfigs{public AttachConfig[] Values;}
        [Serializable]private sealed class AttachConfig{public string BoneName;public Pointer Bone;}
        [Serializable]private sealed class Proof{public string source_sha256,editor,backend,coverage;public int queries,original_animators,original_attachments,count_cases,fade_cycles,rendered_pixels;public bool play_mode;}
    }
}
