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
using Zjcs.Official.World.Map13TreePlayables;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.World.Map13EntityFadeHandlers;
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

namespace Zjcs.Editor.Map13TreePlayables
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13TreePlayableAcceptance
    {
        private const string Key = "Map13.TreePlayables";
        private const string SourcePath = "assets/reference/official-map/map13-native-tree-playable-prefabs-source-manifest.json";
        private static bool started, error;
        private static int fadeChecks;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13TreePlayableAcceptance()
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
            foreach (var entry in source.inputs.Concat(source.outputs).Concat(source.tools??Array.Empty<Entry>()))
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository, entry.path))) == entry.sha256, "State-view dependency changed: " + entry.path);
            string runtimeHash=VerifyReceipt("map13-tree-playable-runtime-source-manifest.json");
            string fadeHash=VerifyReceipt("map13-entity-fade-handlers-source-manifest.json");
            int birthCases=0, loopCases=0, dissolveCases=0, particles=0, pixels=0;
            fadeChecks=0;
            Runner.Center=new MyPlayableCenter();
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
                    foreach (var config in attachment.Configs.Values) { Require(agent.GetBone(config.BoneName)==config.Bone,"Original attachment registration differs."); }
                    foreach (var item in refs.Where(r=>r.type.@class == "ECAnimatorView"))
                    {
                        var animator = new ECAnimatorView(); var expected = (Animator)mapping[cab+":"+item.data.mAnimator.m_PathID]; Set(animator,"mAnimator",expected);
                        ((IECView)animator).Init(bridge); ((IECView)animator).Load(entity,false); yield return null;
                        Require(animator.AnimationController && animator.AnimationController == host.GetComponent<Zjcs.Official.StoryAnimation.AnimationController>() && animator.AnimationController.myAnimator == expected && animator.AnimationController.myInited,"Original animator view initialization/parent/controller differs.");
                        ((IECView)animator).Unload();
                    }
                    var awards=host.AddComponent<Map13AwardCountBinding>();awards.Stage=entity.Stage;awards.Awards=entity.Awards;
                    var bounds = binding.Skeletons.Single().View.LocalBounds; var skeleton = binding.Skeletons.Single().transform;
                    camera.transform.position = skeleton.TransformPoint(bounds.center)+new Vector3(0,0,-50); camera.orthographicSize = Mathf.Max(bounds.extents.y*skeleton.lossyScale.y,bounds.extents.x*skeleton.lossyScale.x)*1.5f;
                    var countData=refs.Single(r=>r.type.@class=="ECMapAwardItemCountSwitcherView").data;
                    var items=countData.mItemGos.Select(p=>(GameObject)mapping[cab+":"+p.m_PathID]).ToList();
                    var countView=new ECMapAwardItemCountSwitcherView();Set(countView,"mItemGos",items);SetEmptyList(countView,"mItemCountRangeConfigs");Set(countView,"mSpriteRendererList",(SpriteRendererList)mapping[cab+":"+countData.mSpriteRendererList.m_PathID]);((IECView)countView).Init(bridge);
                    foreach(long count in new long[]{0,1,2,3,4,5,6,7,8,9,10,13})
                    {
                        entity.Awards.Info.AwardItemInfos[0]=new StageSingleAwardItemInfo{ItemId=1001,Count=count};
                        ((IECView)countView).Load(entity,false);
                        var birth=Play("Treevine_birth_playable",host);
                        var animator=birth.GetComponentInChildren<MyPlayableAnimator>(true);
                        Require(animator.myTriggerParam=="Start_"+(count>=1&&count<=10?count:10),"Original birth count trigger differs: "+count);
                        Require(animator.myApplyComponent==host.GetComponent<AnimationController>(),"Original birth did not bind the entity animation controller.");
                        for(int frame=0;frame<4;frame++)yield return null;
                        birth.Stop(true);Runner.Center.Update(0);Runner.Center.LateUpdate();UnityEngine.Object.Destroy(birth.gameObject);yield return null;birthCases++;
                        ((IECView)countView).Unload();
                    }
                    foreach(long count in new long[]{1,4,10})
                    {
                        entity.Awards.Info.AwardItemInfos[0]=new StageSingleAwardItemInfo{ItemId=1001,Count=count};((IECView)countView).Load(entity,false);
                        var loop=Play("Treevine_loop_playable",host);var animator=loop.GetComponentInChildren<MyPlayableAnimator>(true);
                        Require(animator.myTriggerParam=="Loop_"+count,"Original loop count trigger differs.");
                        var attaches=loop.GetComponentsInChildren<MyPlayableAttachToBone>(true);Require(attaches.Length==11,"Original loop attachment inventory differs.");
                        foreach(var effect in attaches){var targetBone=agent.GetBone(effect.myAttach.myBoneName);Require(targetBone&&effect.myApplyComponent.transform.parent==targetBone,"Original loop did not attach to source bone: "+effect.myAttach.myBoneName);}
                        for(int frame=0;frame<12;frame++)yield return null;
                        if(count==4){pixels+=Capture(camera,target,image,"native-tree-loop-"+row+".png");particles+=attaches.Sum(a=>a.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Length);}
                        loop.Stop(true);Runner.Center.Update(0);Runner.Center.LateUpdate();UnityEngine.Object.Destroy(loop.gameObject);yield return null;loopCases++;((IECView)countView).Unload();
                    }
                    entity.Awards.Info.AwardItemInfos[0]=new StageSingleAwardItemInfo{ItemId=1001,Count=4};((IECView)countView).Load(entity,false);
                    var dissolveSequence=Play("Treevine_dissolve1_playable",host);
                    var material=dissolveSequence.GetComponentInChildren<MyPlayableMaterialPropertyBlock>(true);
                    Require(material.FloatPropertyName=="_DissolveThreshold"&&Mathf.Abs(material.Duration-.8333333134651184f)<.000001f,"Original dissolve material configuration differs.");
                    Require(material.myApplyComponent==agent.GetBone("Tree").GetComponent<Renderer>(),"Original dissolve did not use the registered Tree renderer.");
                    Require(material.FloatCurve.keys.Length==2&&material.ColorCurve.colorKeys.Length==2&&material.ColorCurve.alphaKeys.Length==2,"Original native curve/gradient import differs.");
                    Require(Mathf.Abs(material.FloatCurve.Evaluate(.5f)-.5f)<.000001f&&material.ColorCurve.Evaluate(.5f)==Color.white,"Original dissolve curves differ: float="+material.FloatCurve.Evaluate(.5f)+", color="+material.ColorCurve.Evaluate(.5f));
                    foreach(string bone in new[]{"Apple1","Apple2","Apple3","Apple4","Apple5","Apple6","Apple7","Apple8","Apple9","Apple10","UI"})Require(!agent.GetBone(bone).gameObject.activeSelf,"Original dissolve active-bone rule differs: "+bone);
                    var block=new MaterialPropertyBlock();float threshold=0;
                    for(int frame=0;frame<60&&threshold<.35f;frame++){yield return null;((Renderer)material.myApplyComponent).GetPropertyBlock(block);threshold=block.GetFloat("_DissolveThreshold");}
                    Require(threshold>=.35f&&threshold<1,"Original material dissolve did not advance.");
                    pixels+=Capture(camera,target,image,"native-tree-dissolve-"+row+".png");particles+=dissolveSequence.GetComponentInChildren<MyPlayableFollowBone>(true).myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Length;
                    for(int frame=0;frame<90&&threshold<.99f;frame++){yield return null;((Renderer)material.myApplyComponent).GetPropertyBlock(block);threshold=block.GetFloat("_DissolveThreshold");}
                    Require(threshold>=.99f,"Original material dissolve did not finish.");
                    dissolveSequence.Stop(true);Runner.Center.Update(0);Runner.Center.LateUpdate();UnityEngine.Object.Destroy(dissolveSequence.gameObject);yield return null;dissolveCases++;
                    yield return VerifyFadeHandlers(host,entity,refs);
                    ((IECView)countView).Unload();((IECView)attachment).Unload();UnityEngine.Object.Destroy(host);yield return null;row++;
                }
            }
            Require(birthCases==24&&loopCases==6&&dissolveCases==2&&particles==54&&fadeChecks>=40&&!error,"Original tree performance coverage/errors differ: births="+birthCases+", loops="+loopCases+", dissolves="+dissolveCases+", particles="+particles+", fades="+fadeChecks+", error="+error);
            Runner.Center.RemoveAll();Runner.Center=null;
            var proof=new Proof{source_sha256=Hash(bytes),runtime_source_sha256=runtimeHash,fade_source_sha256=fadeHash,birth_cases=birthCases,loop_cases=loopCases,dissolve_cases=dissolveCases,particles=particles,fade_checks=fadeChecks,rendered_pixels=pixels,editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),play_mode=Application.isPlaying,coverage="All three original native performance prefabs and original fade-out destruction handlers run with original tree models, registered anchors, animators and explicit diagnostic reward-count/map-front inputs. Formal enclosing entity, gameplay/input/save and Player remain pending."};
            File.WriteAllText(Path.Combine(Output,"native-tree-playables-execution.json"),JsonUtility.ToJson(proof,true));Debug.Log("Map13 original tree performances passed: "+birthCases+" births, "+loopCases+" loops, "+dissolveCases+" dissolves, "+particles+" particles.");
        }
        private static MyPlayableSequence Play(string name,GameObject source)
        {
            var prefab=Resources.Load<GameObject>("Map13/Native/TreePlayables/"+name);Require(prefab,"Original performance prefab absent: "+name);
            var holder=new GameObject("Inactive original performance injection");holder.SetActive(false);
            var instance=UnityEngine.Object.Instantiate(prefab,holder.transform,false);
            Require(instance.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c),"Original performance has a missing script.");
            foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=Runner.Center;
            holder.SetActive(true);instance.transform.SetParent(null,false);UnityEngine.Object.Destroy(holder);
            var sequence=instance.GetComponent<MyPlayableSequence>();sequence.Init(source,source);sequence.TryPlay();
            Require(sequence.myStatus==MyPlayable.PlayableStatus.Play,"Original performance did not play: "+name);return sequence;
        }
        private static int Capture(Camera camera,RenderTexture target,Texture2D image,string name)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=previous;
            int visible=image.GetPixels32().Count(c=>c.a>0);Require(visible>100,"Original tree performance rendered no pixels.");File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());return visible;
        }
        private static IEnumerator VerifyFadeHandlers(GameObject host,EntityInput entity,RefId[] refs)
        {
            var source=refs.Single(r=>r.type.@class=="EcMapEntityFadeOutDelayDestoryHandler").data;
            var prefab=Resources.Load<GameObject>("Map13/Native/TreePlayables/Treevine_dissolve1_playable");
            var owner=host.AddComponent<FadeOwner>();owner.Data=entity;int priorCalls=0;Func<bool> prior=()=>{priorCalls++;return false;};owner.FadeOutAction=prior;
            var map=new MapRead();var supporter=new FadeSupporter(host,entity);
            var handler=new EcMapEntityFadeOutDelayDestoryHandler{FadeOutPlayable=prefab,FadeOutTime=source.FadeOutTime,WhenMapFront=source.WhenMapFront!=0};
            handler.Init(supporter,map);handler.Visible();
            CheckFade(owner.FadeOutAction.GetInvocationList().Length==2,"Original fade handler did not append its callback.");
            CheckFade(!owner.FadeOutAction()&&priorCalls==1&&supporter.Played.Count==0,"Original uninitialized map gate/delegate ordering differs.");
            map.initialized=true;CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==0,"Original missing loading-panel gate differs.");
            map.panel=new Map13LoadingPanelState{SplashStatus=SplashStatus.Hide};CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==0,"Original map-front gate differs.");
            map.front=true;
            foreach(var splash in new[]{SplashStatus.Showing,SplashStatus.Show,SplashStatus.Hiding}){map.panel.SplashStatus=splash;CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==0,"Original loading splash gate differs.");}
            map.panel.SplashStatus=SplashStatus.Hide;handler.Invisible();CheckFade(owner.FadeOutAction()&&supporter.Played.Count==0,"Original invisible entity should finish without starting an effect.");
            handler.Visible();float started=Time.time;CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==1,"Original visible fade should start once and wait.");
            CheckFade(supporter.Played[0].myFadeOutDuration==source.FadeOutTime&&source.FadeOutTime==2.8299999237060547f,"Original configured fade-out duration differs.");
            CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==1,"Original repeated fade callback restarted the effect.");
            while(Time.time-started<source.FadeOutTime+.05f)yield return null;
            CheckFade(owner.FadeOutAction()&&supporter.Played.Count==1,"Original fade did not finish after its source deadline.");
            CheckFade((float)typeof(EcMapEntityFadeOutDelayDestoryHandler).GetField("mEndTime",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(handler)==float.MaxValue,"Original fade deadline did not reset.");
            handler.Release();CheckFade(owner.FadeOutAction==prior,"Original fade Release removed another callback or leaked its own.");
            foreach(var played in supporter.Played)if(played)UnityEngine.Object.Destroy(played.gameObject);supporter.Played.Clear();yield return null;
            handler.Init(supporter,map);handler.Visible();handler.SetAppendFadeOut(Resources.Load<GameObject>("Map13/Native/TreePlayables/Treevine_loop_playable"),.08f);
            started=Time.time;CheckFade(!owner.FadeOutAction()&&supporter.Played.Count==1,"Original appended effect did not start.");
            CheckFade(supporter.Played[0].myFadeOutDuration==source.FadeOutTime,"Original first appended effect fade duration differs.");
            while(Time.time-started<.12f){yield return null;owner.FadeOutAction();}
            CheckFade(supporter.Played.Count==2,"Original appended effect did not transition to its main effect.");
            CheckFade(supporter.Played[1].myFadeOutDuration==0,"Original second effect incorrectly inherited the first effect's fade duration.");
            while(Time.time-started<source.FadeOutTime+.13f)yield return null;
            CheckFade(owner.FadeOutAction()&&supporter.Played.Count==2,"Original appended fade did not finish at the combined deadline.");
            handler.Release();CheckFade(owner.FadeOutAction==prior,"Original appended fade Release leaked its callback.");
            handler.WhenMapFront=false;map.initialized=false;map.panel=null;handler.Init(supporter,map);handler.Invisible();
            CheckFade(owner.FadeOutAction(),"Original disabled map-front condition was still applied.");handler.Release();
            owner.FadeOutAction-=prior;CheckFade(owner.FadeOutAction==null,"Original fade callback list did not restore cleanly.");
            foreach(var played in supporter.Played)if(played)UnityEngine.Object.Destroy(played.gameObject);UnityEngine.Object.Destroy(owner);yield return null;
        }
        private static void CheckFade(bool value,string message){Require(value,message);fadeChecks++;}
        private static string VerifyReceipt(string name)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(Repository,"assets/reference/official-map",name));var source=JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(bytes));
            foreach(var entry in source.inputs.Concat(source.outputs).Concat(source.tools??Array.Empty<Entry>()).Concat(source.assemblies??Array.Empty<Entry>()))
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository,entry.path)))==entry.sha256,"Original performance dependency changed: "+entry.path);
            return Hash(bytes);
        }
        private sealed class MapRead:IMap13FadeMapState,IServiceProvider
        {
            public bool initialized,front;public Map13LoadingPanelState panel;
            public bool Initialized=>initialized;public bool IsFront=>front;public Map13LoadingPanelState LoadingPanel=>panel;
            public object GetService(Type type)=>type==typeof(IMap13FadeMapState)?this:null;
        }
        private sealed class FadeSupporter:IECPlayableSupporter
        {
            public readonly List<MyPlayable> Played=new List<MyPlayable>();
            public FadeSupporter(GameObject source,ECViewEntityData entity){Source=source;EcEntity=entity;}
            public IClientVisibilitySupporter ClientVisibilitySupporter=>null;public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter=>null;
            public ECViewEntityData EcEntity{get;}public GameObject Source{get;}public bool IsNewEntity=>false;
            public T GetComponent<T>()=>EcEntity.GetComponent<T>();public T GetECView<T>()where T:ECViewBase=>null;
            public MyPlayable PlayPlayable(GameObject prefab,GameObject source,GameObject target){Require(source==Source&&target==Source,"Original fade source/target identity differs.");var value=Play(prefab.name,source);Played.Add(value);return value;}
        }
        private sealed class FadeOwner:ECViewEntityBinding,IMap13EntityFadeOutBinding
        {
            public ECViewEntityData Data;public override ECViewEntityData Entity=>Data;public Func<bool> FadeOutAction{get;set;}
            public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value)=>throw new InvalidOperationException("Unexpected fixture visibility setter");
            public override void AddStartReceiver(IMyEntityStartEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture start receiver");
            public override void RemoveStartReceiver(IMyEntityStartEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture start receiver");
            public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture remove receiver");
            public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture remove receiver");
            public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture post-start receiver");
            public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture post-start receiver");
            public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture pre-remove receiver");
            public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value)=>throw new InvalidOperationException("Unexpected fixture pre-remove receiver");
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
        private sealed class Runner:MonoBehaviour{public static MyPlayableCenter Center;private void Update()=>Center?.Update(Time.deltaTime);private void LateUpdate()=>Center?.LateUpdate();}
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
        [Serializable]private sealed class Source{public Entry[] inputs,outputs,tools,assemblies;}
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
        [Serializable]private sealed class RefData{public Pointer[] mItemGos,mItemCountRangeConfigs;public Pointer mSpriteRendererList,mAnimator;public AttachConfigs Configs;public float FadeOutTime;public int WhenMapFront;}
        [Serializable]private sealed class Pointer{public long m_PathID;}
        [Serializable]private sealed class AttachConfigs{public AttachConfig[] Values;}
        [Serializable]private sealed class AttachConfig{public string BoneName;public Pointer Bone;}
        [Serializable]private sealed class Proof{public string source_sha256,runtime_source_sha256,fade_source_sha256,editor,backend,coverage;public int birth_cases,loop_cases,dissolve_cases,particles,fade_checks,rendered_pixels;public bool play_mode;}
    }
}
