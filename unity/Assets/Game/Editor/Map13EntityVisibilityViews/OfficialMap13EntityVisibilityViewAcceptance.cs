using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityRendering;
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

namespace Zjcs.Editor.Map13EntityVisibilityViews
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13EntityVisibilityViewAcceptance
    {
        private const string Key = "Map13.EntityVisibilityViews";
        private const string SourcePath = "assets/reference/official-map/map13-entity-visibility-views-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13EntityVisibilityViewAcceptance()
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
            int cases = VerifyQueries(), pixels = 0;
            var models = JsonUtility.FromJson<Models>(File.ReadAllText(Path.Combine(Repository, "assets/reference/official-map/map13-native-tree-models-source-manifest.json")));
            var anchors = JsonUtility.FromJson<Hierarchy>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-attachment-followers.json"));
            var slicedRecords = JsonUtility.FromJson<SlicedSource>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-entity-sliced-renderers.json"));
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

                    var driver = originals.objects.Single(o=>o.type == "ECViewDriver" && o.references.Any(p=>p.field == ".m_GameObject" && Identity(p)==root.key));
                    string cab = driver.key.Split(':')[0]; var refs = driver.fields.references.RefIds;
                    var data = refs.Single(v=>v.type.@class=="ECEntityInteractionShowView").data;
                    var raw = originals.objects.Single(o=>o.key==cab+":"+data.mLifeSlider.m_PathID);
                    var sliceGo = ((GameObject)mapping[Identity(raw.references.Single(p=>p.field==".m_GameObject"))]);
                    var slice = sliceGo.AddComponent<SlicedFilledRender>();var sr = slicedRecords.renderers.Single(v=>v.key==raw.key);
                    var serialized = new SerializedObject(slice); serialized.FindProperty("mMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(sr.material.Substring(6));
                    serialized.FindProperty("mSize").vector2Value=sr.fields.mSize;serialized.FindProperty("mColor").colorValue=sr.fields.mColor;
                    serialized.FindProperty("mFillMode").intValue=sr.fields.mFillMode;serialized.FindProperty("mFillOrigin").intValue=sr.fields.mFillOrigin;
                    serialized.FindProperty("mAnticlockwise").boolValue=sr.fields.mAnticlockwise!=0;serialized.FindProperty("mFillAmount").floatValue=sr.fields.mFillAmount;serialized.ApplyModifiedPropertiesWithoutUndo();slice.OnAfterDeserialize();
                    slice.BindOriginalSprite(sprites.Package(sr.package).LoadAsset<Sprite>(sr.assetName));
                    mapping[raw.key]=slice;
                    var entity = new EntityInput(); entity.Source.Entity=entity;entity.Interaction.mEntSrcComp=entity.Source;
                    var entityBinding=host.AddComponent<FixtureBinding>();entityBinding.Data=entity;
                    host.SetActive(true); binding.InitializeActive(); yield return null;
                    var list = (SpriteRendererList)mapping[cab+":"+data.mSpriteRenderers.m_PathID];
                    var supporter=new MapECViewClientVisibilitySupporter(new Lifetime(),list,null,instance.transform);supporter.Register(entityBinding);((IMyEntityStartPostEventReceiver)supporter).MyEntity_OnStartPost(entityBinding);
                    var bridge=new BridgeInput(transforms[root.transformKey].gameObject,supporter);
                    var factory=new HandlerFactory();var visibleView=new ECClientVisibilityView{EntityClientVisibilityHandlerFactory=factory};((IECView)visibleView).Init(bridge);
                    Require(!visibleView.IsVisible,"Original visibility start gate differs.");((IECView)visibleView).Load(entity,false);Require(visibleView.IsVisible,"Original empty request must be visible.");cases+=2;
                    entity.Visibility.AddClientVisibilityRequest(ClientVisibilityType.Hide|ClientVisibilityType.WaitRefreshAction);Require(factory.Last.Count==2&&!visibleView.IsVisible,"Original handler conjunction differs.");cases++;
                    var first=factory.Last[0];var second=factory.Last[1];first.Change(true);Require(visibleView.IsVisible&&!second.Visible,"Original direct handler notification ordering changed.");cases++;
                    second.Change(false);Require(!visibleView.IsVisible,"Original handler false update differs.");cases++;
                    entity.Visibility.RemoveClientVisibilityType(ClientVisibilityType.Hide|ClientVisibilityType.WaitRefreshAction);Require(visibleView.IsVisible&&factory.Returned==2,"Original handler release/return differs.");cases++;
                    int notices=0;visibleView.OnVisibilityChanged+=()=>notices++;supporter.GetFadeAnimationSupporter().ImmediateFadeOut();Require(!visibleView.IsVisible&&notices>0,"Original fade-out visibility gate differs.");supporter.GetFadeAnimationSupporter().ImmediateFadeIn();Require(visibleView.IsVisible,"Original fade-in visibility gate differs.");cases+=2;
                    ((IECView)visibleView).Unload();int calls=factory.Requests;entity.Visibility.AddClientVisibilityRequest(ClientVisibilityType.Hide);Require(!visibleView.IsVisible&&factory.Requests==calls,"Visibility unload retained event subscription.");cases++;
                    ((IECView)visibleView).Load(entity,true);Require(factory.Last.All(h=>!h.IsNew),"Original static/new handler flag differs.");((IECView)visibleView).Unload();supporter.ResetVisibility();((IMyEntityStartPostEventReceiver)supporter).MyEntity_OnStartPost(entityBinding);cases++;
                    var interaction=new ECEntityInteractionShowView();Set(interaction,"mEntitySourceTypeShows",Array.Empty<EntitySourceTypeShowNode>());Set(interaction,"mLifeSliderGo",mapping[cab+":"+data.mLifeSliderGo.m_PathID]);Set(interaction,"mLifeSlider",slice);
                    Set(interaction,"mOwnerShowObjs",data.mOwnerShowObjs.Select(p=>(GameObject)mapping[cab+":"+p.m_PathID]).ToList());var other=data.mNotOwnerShowObjs.Select(p=>(GameObject)mapping[cab+":"+p.m_PathID]).ToList();Set(interaction,"mNotOwnerShowObjs",other);Set(interaction,"mSpriteRenderers",list);Set(interaction,"mIsOwnerColor",data.mIsOwnerColor);Set(interaction,"mIsNotOwnerColor",data.mIsNotOwnerColor);((IECView)interaction).Init(bridge);
                    entity.Interaction.IsInteractionUnique=true;entity.Interaction.IsInteractionByOther=false;entity.Source.Ownership=new Map13OwnershipData{Type=EntityOwnershipType.Player,OwnerId=9};
                    var originalColors=list.mySpriteList.ToDictionary(v=>v,v=>v.color);((IECView)interaction).Load(entity,false);Require(other.All(g=>g.activeSelf),"Original non-owner gate differs.");RequireColor(list,data.mIsNotOwnerColor,originalColors);cases++;
                    var bounds = binding.Skeletons.Single().View.LocalBounds;var skeleton=binding.Skeletons.Single().transform;camera.transform.position=skeleton.TransformPoint(bounds.center)+new Vector3(0,0,-50);camera.orthographicSize=Mathf.Max(bounds.extents.y*skeleton.lossyScale.y,bounds.extents.x*skeleton.lossyScale.x)*1.3f;
                    yield return null;pixels+=Capture(camera,target,image,"native-interaction-other-"+row+".png");
                    entity.Source.Ownership.OwnerId=7;entity.Interaction.OnInteractingPlayerUpdate.FireEvent(entity.Interaction);Require(other.All(g=>!g.activeSelf),"Original owner gate differs.");RequireColor(list,data.mIsOwnerColor);cases++;
                    yield return null;pixels+=Capture(camera,target,image,"native-interaction-owner-"+row+".png");
                    entity.Interaction.InteractingPlayerIdSet.Add(7);entity.Interaction.OnInteractingPlayerUpdate.FireEvent(entity.Interaction);Require(other.All(g=>!g.activeSelf),"Original interacting-self OR differs.");cases++;
                    ((IECView)interaction).Unload();entity.Source.Ownership.OwnerId=9;entity.Interaction.InteractingPlayerIdSet.Clear();entity.Interaction.OnInteractingPlayerUpdate.FireEvent(entity.Interaction);Require(other.All(g=>!g.activeSelf),"Interaction unload retained event subscription.");cases++;
                    // Source lifespan uses original start timestamp and unmodified time equation.
                    entity.Source.Ownership.OwnerId=7;entity.Source.EntitySourceType=EcEntitySourceType.Explore;entity.Source.CreateTime=bridge.Services.Now.AddSeconds(-3);entity.Source.EntityParameters=id=>new Map13EntityDurationData{DurationWhenExplore=10};
                    ((IECView)interaction).Load(entity,false);Require(Mathf.Abs(slice.FillAmount-.7f)<.0001f&&slice.gameObject.activeInHierarchy,"Original lifespan start/fraction differs.");cases++;
                    bridge.Services.Now=bridge.Services.Now.AddSeconds(4);yield return new WaitForSeconds(1.1f);Require(Mathf.Abs(slice.FillAmount-.3f)<.0001f,"Original lifespan coroutine did not update.");cases++;
                    ((IECView)interaction).Unload();bridge.StopAll();supporter.Unregister();UnityEngine.Object.Destroy(host);yield return null;row++;
                }
            }
            Require(cases>=40&&!error,"Original presentation checks/errors differ.");var proof=new Proof{source_sha256=Hash(bytes),editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),cases=cases,rendered_pixels=pixels,play_mode=Application.isPlaying,coverage="Original visibility/interaction views, exact source tree fields, ownership color/gates, original source-size lifespan renderer and coroutine, event release; explicit diagnostic handler/state/time inputs. Formal entity/UI/handlers/gameplay/input/save/Player remain pending."};File.WriteAllText(Path.Combine(Output,"native-entity-visibility-views-execution.json"),JsonUtility.ToJson(proof,true));Debug.Log("Map13 original visibility/interaction views passed: "+cases+" checks, "+pixels+" pixels.");
        }
        private static int VerifyQueries()
        {
            var data=new Map13EntitySourceQuery();Require(!data.IsMine(EntityOwnershipType.Player,7)&&data.GetDuration()==-1,"Original empty ownership/time guards differ.");data.Ownership=new Map13OwnershipData{Type=EntityOwnershipType.Player,OwnerId=7};Require(data.IsMine(EntityOwnershipType.Player,7)&&!data.IsMine(EntityOwnershipType.Entity,7),"Original ownership type/id differs.");
            data.CreateTime=new DateTime(2026,1,1);data.EntitySourceType=EcEntitySourceType.Explore;data.Entity=new EntityInput();data.EntityParameters=id=>null;Require(data.GetDuration()==-1,"Original missing explore config guard differs.");data.EntityParameters=id=>new Map13EntityDurationData{DurationWhenExplore=11};Require(data.GetDuration()==11,"Original explore duration differs.");data.EntitySourceType=EcEntitySourceType.ActivityRefresh;Require(data.GetDuration()==-1,"Original missing activity guard differs.");data.Activity=new Map13WorldActivityData{ActivityClassId=100};data.ActivityParameters=id=>new Map13EntityDurationData{EntityDuration=0};Require(data.GetDuration()==-1,"Original zero activity duration differs.");data.ActivityParameters=id=>new Map13EntityDurationData{EntityDuration=9};Require(data.GetDuration()==9,"Original activity duration differs.");
            var interaction=new Map13EntityInteractionQuery{mEntSrcComp=data};Require(interaction.CanInteract(7),"Original nonunique empty set differs.");interaction.InteractingPlayerIdSet.Add(7);Require(!interaction.CanInteract(7)&&interaction.IsPlayerInteracting(7),"Original nonunique already-interacted rule differs.");interaction.IsInteractionUnique=true;Require(!interaction.CanInteract(7),"Original unique occupied rule differs.");interaction.InteractingPlayerIdSet.Clear();Require(interaction.CanInteract(7)&&!interaction.CanInteract(8),"Original unique ownership differs.");interaction.IsInteractionByOther=true;Require(interaction.CanInteract(8),"Original interaction-by-other option differs.");return 12;
        }
        private static int Capture(Camera camera,RenderTexture target,Texture2D image,string name){camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=previous;int pixels=image.GetPixels32().Count(c=>c.a>0);Require(pixels>100,"Original interaction image is empty.");File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());return pixels;}
        private static void RequireColor(SpriteRendererList list,Color color,Dictionary<SpriteRenderer,Color> original=null){foreach(var sprite in list.mySpriteList){var expected=original!=null&&original[sprite]!=Color.white?original[sprite]:color;Require(Vector4.Distance(sprite.color,expected)<.0001f,"Original source interaction color differs: "+sprite.name+"/actual="+sprite.color+"/expected="+expected);}var skeleton=list.SkeletonRenderer.Skeleton;Require(Mathf.Abs(skeleton.R-color.r)<.0001f&&Mathf.Abs(skeleton.G-color.g)<.0001f&&Mathf.Abs(skeleton.B-color.b)<.0001f,"Original skeleton interaction tint differs.");}
        private sealed class EntityInput:ECViewEntityData
        {public readonly Map13VisibilityData Visibility=new Map13VisibilityData();public readonly Map13EntitySourceQuery Source=new Map13EntitySourceQuery();public readonly Map13EntityInteractionQuery Interaction=new Map13EntityInteractionQuery();public override int ClassId=>8363;public override EcEntityIdentifier Id=>default;public override T GetComponent<T>()=>typeof(T)==typeof(Map13VisibilityData)?(T)(object)Visibility:typeof(T)==typeof(Map13EntitySourceQuery)?(T)(object)Source:typeof(T)==typeof(Map13EntityInteractionQuery)?(T)(object)Interaction:default;}
        private sealed class Clock:Map13PresentationServices,IServiceProvider{public DateTime Now=new DateTime(2026,1,1);public override long PlayerId=>7;public override DateTime GetDateTime()=>Now;public object GetService(Type type)=>type==typeof(Map13PresentationServices)?this:null;}
        private sealed class Lifetime:IECViewMapRootLifetime{public bool Destroying=>false;}
        private sealed class BridgeInput:IECViewBridge,IEntityFadeAnimationBridge
        {public readonly Clock Services=new Clock();private readonly Dictionary<long,Coroutine> running=new Dictionary<long,Coroutine>();private long last;private readonly Runner runner;public BridgeInput(GameObject owner,IClientVisibilitySupporter supporter){GameObject=owner;ClientVisibilitySupporter=supporter;runner=owner.AddComponent<Runner>();}public GameObject GameObject{get;}public IServiceProvider ServiceProvider=>Services;public CancellationToken DestroyCancellationToken=>GameObject.GetCancellationTokenOnDestroy();public IClientVisibilitySupporter ClientVisibilitySupporter{get;}public IEntityFadeAnimationBridge EntityFadeAnimationBridge=>this;public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter=>ClientVisibilitySupporter.GetFadeAnimationSupporter();public T GetView<T>()where T:class,IECView=>null;public long StartCoroutine(IEnumerator routine){long id=++last;running[id]=runner.StartCoroutine(routine);return id;}public void StopCoroutine(long id){if(running.TryGetValue(id,out var value)){runner.StopCoroutine(value);running.Remove(id);}}public void StopAll(){foreach(var value in running.Values)if(value!=null)runner.StopCoroutine(value);running.Clear();}}
        private sealed class Handler:IClientVisibilityHandler{public ClientVisibilityType VisibilityType{get;set;}public bool Visible{get;private set;}public ECViewEntityData Entity{get;private set;}public bool IsNew;public event Action<bool> OnVisibleChanged;public void Init(ECViewEntityData entity,bool isNew){Entity=entity;IsNew=isNew;Visible=false;}public void Change(bool value){Visible=value;OnVisibleChanged?.Invoke(value);}public void Release(){Entity=null;}}
        private sealed class HandlerFactory:IEntityClientVisibilityHandlerFactory{public List<Handler> Last=new List<Handler>();public int Returned,Requests;public void GetHandlers(ClientVisibilityType type,ref List<IClientVisibilityHandler> handlers){Requests++;Last=new List<Handler>();foreach(ClientVisibilityType flag in Enum.GetValues(typeof(ClientVisibilityType)))if(flag!=0&&type.HasFlag(flag)){var handler=new Handler{VisibilityType=flag};Last.Add(handler);handlers.Add(handler);}}public IClientVisibilityHandler GetHandler(ClientVisibilityType type)=>throw new NotSupportedException();public void ReturnHanlder(IClientVisibilityHandler handler){Require(handler.Entity==null,"Original handler was not released before return.");Returned++;}}
        public sealed class FixtureBinding:ECViewEntityBinding,IMap13EntityVisibilityBinding{public ECViewEntityData Data;public override ECViewEntityData Entity=>Data;public IClientVisibilitySupporter ClientVisibilitySupporter{get;private set;}public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value){ClientVisibilitySupporter=value;}public override void AddStartReceiver(IMyEntityStartEventReceiver value){}public override void RemoveStartReceiver(IMyEntityStartEventReceiver value){}public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value){}public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value){}public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value){}public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value){}public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value){}public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value){}}
        private static bool Belongs(Node node,string root,Dictionary<string,Node> nodes){while(true){if(node.transformKey==root)return true;if(string.IsNullOrEmpty(node.parentTransformKey))return false;node=nodes[node.parentTransformKey];}}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        private static void SetEmptyList(object target,string field){var f=target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance);f.SetValue(target,Activator.CreateInstance(f.FieldType));}
        private static bool HasSubscription(object target,string field,object subscriber)=>((Delegate)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target))?.GetInvocationList().Any(d=>ReferenceEquals(d.Target,subscriber))??false;
        private static void CheckCount(List<GameObject> items,long count){for(int i=0;i<items.Count;i++)Require(items[i].activeSelf==(i<count),"Original apple count/order differs at "+i);}
        private static SpriteRendererList.State FadeState(SpriteRendererList list)=>(SpriteRendererList.State)typeof(SpriteRendererList).GetField("mCurrentState",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(list);
        private static string Identity(Reference reference)=>reference.cab.ToLowerInvariant()+":"+reference.path_id;
        private static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Runner:MonoBehaviour{private void LateUpdate(){SlicedFilledRenderService.Current.LateUpdate();}}
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
        [Serializable]private sealed class RefData{public Pointer[] mItemGos,mItemCountRangeConfigs,mOwnerShowObjs,mNotOwnerShowObjs;public Pointer mLifeSliderGo,mLifeSlider,mSpriteRenderers;public Color mIsOwnerColor,mIsNotOwnerColor;public Pointer mSpriteRendererList,mAnimator;public AttachConfigs Configs;}
        [Serializable]private sealed class Pointer{public long m_PathID;}
        [Serializable]private sealed class AttachConfigs{public AttachConfig[] Values;}
        [Serializable]private sealed class AttachConfig{public string BoneName;public Pointer Bone;}
        [Serializable]private sealed class SlicedSource{public SlicedRecord[] renderers;}
        [Serializable]private sealed class SlicedRecord{public string key,material,package,assetName;public SlicedFields fields;}
        [Serializable]private sealed class SlicedFields{public int mFillMode,mFillOrigin,mAnticlockwise;public Vector2 mSize;public Color mColor;public float mFillAmount;}
        [Serializable]private sealed class Proof{public string source_sha256,editor,backend,coverage;public int cases,rendered_pixels;public bool play_mode;}
    }
}
