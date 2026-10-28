using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;


namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14NpcVisibilityPlayableAcceptance
    {
        private const string Key="Zjcs.Map14.NativeNpcVisibilityPlayables";
        private const string CatalogPath="Assets/Game/Resources/Map14/Native/native-npc-visibility-playables-catalog.asset";
        private const string Folder="Assets/Official/World/Map/14/NativeNpcVisibilityPlayables/Prefabs/Playables/DisplayPackage/Story/";
        private static readonly string[] Names={"playable_npc_hide","playable_npc_active"};
        private static readonly string[] Guids={"861c890ded796394a82cc519daf31d5e","d2ba623f311ea224e811e7b6b130ef4a"};
        private static bool started;private static int errors;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Explicit validation workspace required.");
        public static void Run()
        {
            try
            {
                Require(Path.GetFullPath(Output).Replace('\\','/').Contains("/output/validation/"),"named isolated validation workspace");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var catalog=AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if(!catalog){catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.Bindings=Names.Select((name,i)=>new NativeMap13AssetCatalog.Binding{OfficialPath=Guids[i],Asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab")}).ToArray();
                Require(catalog.Bindings.All(b=>b.Asset),"both original NPC visibility playables import");
                foreach(var binding in catalog.Bindings)
                {
                    var prefab=(GameObject)binding.Asset;Require(prefab.GetComponentsInChildren<Transform>(true).Length==(prefab.name==Names[0]?3:2),"original visibility hierarchy");
                    Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"all original component script bindings");
                }
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor",true).GetMethod("Validate").Invoke(null,null);
                SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
                var shared=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("SharedAsset",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(typeof(NativeMap13AssetCatalog));
                var pipeline=(NativeMap13AssetCatalog)shared.Invoke(null,new object[]{true});PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.defaultRenderPipeline=pipeline.Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");QualitySettings.renderPipeline=null;
                SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;started=false;errors=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            Application.logMessageReceived-=Observe;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Native NPC visibility playable verification timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;new GameObject("Native NPC visibility playable verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps=Steps();while(true){object value;try{if(!steps.MoveNext())break;value=steps.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 native NPC visibility playable Play Mode passed: both original prefabs, exact original sound reference, explicit original GUID catalog, root visibility persists after duration and stop, repeated hide/show, original named-transform refcounts and revert behavior. The original terrain is an isolated visibility target; original NPC models/animation rendering, actual sound/DSP, full story/map/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native NPC visibility playable: "+message);}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Original terrain validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);yield return null;yield return null;
            var envType=environment.GetType();var source=(GameObject)envType.GetField("Root").GetValue(environment);var agent=source.AddComponent<AttachAgent>();
            var target=new GameObject("Original sound target selection probe");var catalog=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-npc-visibility-playables-catalog");
            var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
            uint expectedEvent=0;int played=0;
            for(int replay=0;replay<2;replay++)foreach(int index in new[]{0,1})
            {
                var prefab=catalog.Load<GameObject>(Guids[index]);var instance=UnityEngine.Object.Instantiate(prefab);var sequence=instance.GetComponent<MyPlayableSequence>();var leaf=instance.GetComponentInChildren<MyPlayableActiveBone>(true);var center=new MyPlayableCenter();
                Require(sequence&&leaf&&sequence.myPlayables.Count==(index==0?2:1)&&sequence.myPlayables.All(c=>c.myTime==0),"original ordered sequence child references at zero");
                Require(leaf.GetDuration()==(index==0?.2f:.1f),"original visibility duration");
                foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;
                if(index==0){var soundPlayable=instance.GetComponentInChildren<MyPlayableSound>(true);Require(soundPlayable&&soundPlayable.SoundEvent.IsValid,"original hide event reference imports");expectedEvent=soundPlayable.SoundEvent.Id;}
                sequence.Init(source,target);sequence.TryPlay();Require(leaf.myApplyComponent==agent,"original ApplyBySource selects source AttachAgent");
                Require(source.activeSelf==(index==1),"original root hide/show applies immediately");
                for(int frame=0;frame<24;frame++){yield return null;center.Update(1f/60);center.LateUpdate();}
                Require(sequence.myStatus==MyPlayable.PlayableStatus.Stop&&leaf.myStatus==MyPlayable.PlayableStatus.Stop,"original finite sequence completes");
                Require(source.activeSelf==(index==1),"original non-reverting visibility persists after completion");sequence.Stop(true);Require(source.activeSelf==(index==1),"explicit stop retains original visibility");
                if(index==0){played++;Require(sound.Events.Count==played&&sound.Events.Last()==expectedEvent&&sound.Emitters.Last()==target,"original hide event dispatches once to target");}
                yield return new WaitForEndOfFrame();if(replay==0)
                {
                    Debug.Log("Original visibility render sample: active="+source.activeSelf+"; descendantRenderers="+source.GetComponentsInChildren<Renderer>(true).Length+"; visibleDescendants="+source.GetComponentsInChildren<Renderer>(false).Length);
                    var surface=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();RenderTexture.active=previous;camera.targetTexture=null;File.WriteAllBytes(Path.Combine(Output,"npc-visibility-"+index+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);
                }
                sequence.ResetData();center.RemoveAll();UnityEngine.Object.Destroy(instance);yield return null;
            }
            // Exercise the original registry's counter semantics independently of
            // character animation. These plain transforms have no replacement art.
            var registry=new GameObject("Isolated named-transform registry");var registryAgent=registry.AddComponent<AttachAgent>();
            var bone=new GameObject("active-bone");bone.transform.SetParent(registry.transform);var inactive=new GameObject("inactive-bone");inactive.transform.SetParent(registry.transform);inactive.SetActive(false);
            registryAgent.myBoneNameList.AddRange(new[]{"bone","inactive"});registryAgent.myBoneList.AddRange(new[]{bone.transform,inactive.transform});registryAgent.Init();
            Require(registryAgent.GetBone("bone")==bone.transform&&registryAgent.GetBone("inactive")==inactive.transform,"original serialized name/transform map");
            registryAgent.SetBoneActiveStatus("bone",false);registryAgent.SetBoneActiveStatus("bone",false);Require(!bone.activeSelf,"overlapping hides retain negative count");registryAgent.SetBoneActiveStatus("bone",true);Require(!bone.activeSelf,"one restore does not release another hide");registryAgent.SetBoneActiveStatus("bone",true);Require(bone.activeSelf,"matching restores release both hides");
            registryAgent.SetBoneActiveStatus("inactive",true);Require(inactive.activeSelf,"initially inactive bone starts counter at zero");registryAgent.SetBoneActiveStatus("inactive",false);Require(!inactive.activeSelf,"matching hide restores initial inactive state");
            var probe=new GameObject("Original reverting bone playable");var reversible=probe.AddComponent<MyPlayableActiveBone>();var so=new SerializedObject(reversible);so.FindProperty("mBoneNames").arraySize=1;so.FindProperty("mBoneNames").GetArrayElementAtIndex(0).stringValue="bone";so.FindProperty("mActive").boolValue=false;so.FindProperty("mRevertOnStop").boolValue=true;so.FindProperty("mDuration").floatValue=-1;so.ApplyModifiedPropertiesWithoutUndo();
            reversible.myTriggerList=new List<MyPlayableTriggerTimeCfg>();reversible.myFadeoutTrigerList=new List<MyPlayableTrigger>();reversible.myEndTrigerList=new List<MyPlayableTrigger>();
            var probeCenter=new MyPlayableCenter();reversible.MyPlayableCenter=probeCenter;reversible.myApplyComponent=registryAgent;reversible.Init(registry,null);reversible.TryPlay();Require(!bone.activeSelf,"original bone playable hides registered transform");reversible.Stop(true);Require(!bone.activeSelf,"original base stop waits for the next scheduler phase");yield return null;probeCenter.Update(0);probeCenter.LateUpdate();Require(bone.activeSelf,"original reversing stop restores registered transform");probeCenter.RemoveAll();
            Require(!registryAgent.TryAddBoneEntry("  ",bone.transform)&&!registryAgent.TryAddBoneEntry("bone",bone.transform)&&registryAgent.TryAddBoneEntry(" trimmed ",bone.transform),"original entry trimming and duplicate/empty rejection");registryAgent.RemoveBoneEntryAt(2);Require(registryAgent.myBoneNameList.Count==2,"original entry removal");
            registryAgent.GenerateBoneMap();Require(registryAgent.GetBone("")==registry.transform&&registryAgent.GetBone("active-bone")==bone.transform&&registryAgent.GetBone("inactive-bone")==null,"original generated map excludes inactive branch");
            registryAgent.Clear();Require(registryAgent.GetBone("")==registry.transform&&registryAgent.GetBone("active-bone")==null,"original clear retains root only");
            var attachmentSteps=VerifyAttachmentTransforms();while(attachmentSteps.MoveNext())yield return attachmentSteps.Current;
            Debug.Log("Original NPC hide event ID: "+expectedEvent+"; dispatches="+played+"; original attachment selection, direction, transforms and parent restoration passed.");
            OfficialWwiseEvent.Host=oldSound;UnityEngine.Object.Destroy(probe);UnityEngine.Object.Destroy(registry);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(source);yield return null;((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));UnityEngine.Object.Destroy(cameraObject);
        }
        private static IEnumerator VerifyAttachmentTransforms()
        {
            var source=new GameObject("Isolated source attachment transform");source.transform.position=new Vector3(10,20,0);source.transform.rotation=Quaternion.Euler(0,0,90);source.transform.localScale=new Vector3(2,2,1);
            var bone=new GameObject("attachment-bone");bone.transform.SetParent(source.transform,false);bone.transform.localPosition=new Vector3(1,2,0);bone.transform.localRotation=Quaternion.Euler(0,0,30);bone.transform.localScale=new Vector3(.5f,.75f,1);
            var agent=source.AddComponent<AttachAgent>();agent.myBoneNameList.Add("bone");agent.myBoneList.Add(bone.transform);agent.Init();
            var target=new GameObject("Isolated target attachment transform");target.transform.position=new Vector3(10,30,0);var skill=new GameObject("Skill attachment selector");var damage=new GameObject("Damage attachment selector");var parent=new GameObject("Original attachment parent");
            var probe=new GameObject("Original attachment playable");var playable=probe.AddComponent<MyPlayableAttachToBone>();var center=new MyPlayableCenter();playable.MyPlayableCenter=center;
            playable.myTriggerList=new List<MyPlayableTriggerTimeCfg>();playable.myFadeoutTrigerList=new List<MyPlayableTrigger>();playable.myEndTrigerList=new List<MyPlayableTrigger>();playable.Init(source,target,param:new Dictionary<string,object>{{"Skill",skill},{"Damage",damage}});
            Require(AttachPosCfg.GetAttachToGo(playable,EAttachTo.Source)==source&&AttachPosCfg.GetAttachToGo(playable,EAttachTo.Target)==target&&AttachPosCfg.GetAttachToGo(playable,EAttachTo.Skill)==skill&&AttachPosCfg.GetAttachToGo(playable,EAttachTo.Damage)==damage,"all four original attachment selectors");
            var position=new AttachPosCfg(EAttachTo.Source){myBoneName="bone",myOffset=new Vector3(3,1,0),myOffsetScale=true};Require(position.GetAttachToBone(playable)==bone,"original registered bone selection");position.myBoneName="absent";Require(position.GetAttachToBone(playable)==source,"original missing bone falls back to source root");
            Require(Vector3.Distance(position.CalcPos(playable),new Vector3(8,26,0))<.0001f,"original source rotation and scaled offset");position.myOffsetScale=false;position.IsRotationTowardsTarget=true;
            var offsets=new[]{new Vector3(3,1,0),new Vector3(-3,-1,0),new Vector3(-1,3,0),new Vector3(1,-3,0)};
            for(int i=0;i<offsets.Length;i++)Require(Vector3.Distance(position.CalcPos(source,null,(Zjcs.Official.StoryAttachment.Direction)i),source.transform.position+offsets[i])<.0001f,"original four direction offsets");
            var reverse=new AttachPosCfg(EAttachTo.Target){myOffset=new Vector3(3,1,0),IsRotationTowardsTarget=true};Require(Vector3.Distance(reverse.CalcPos(target,null,Zjcs.Official.StoryAttachment.Direction.Up),target.transform.position+offsets[1])<.0001f,"original target direction reverses source-to-target axis");
            position.IsRotationTowardsTarget=false;position.myLinkDir=true;position.myBoneName="";Require(Vector3.Distance(position.CalcPos(playable),source.transform.position+offsets[0])<.0001f,"original source-target link direction");
            var config=new AttachPosRotCfg{AttachTo=EAttachTo.Source,myBoneName="bone",myOffset=new Vector3(.5f,-.25f,0),myRotation=Quaternion.Euler(0,0,15),myScale=.8f};
            Require(config.CalcTransform(source,parent.transform,out var pos,out var rot,out var scale)&&Vector3.Distance(pos,bone.transform.TransformPoint(config.myOffset))<.0001f&&Quaternion.Angle(rot,bone.transform.rotation*config.myRotation)<.001f&&Vector3.Distance(scale,bone.transform.lossyScale*.8f)<.0001f,"original attachment world transform");
            Require(AttachPosRotCfg.GetBone(source,"absent")==source.transform&&AttachPosRotCfg.GetBone(null,"bone")==null,"original transform root fallback and null source");
            var follower=new GameObject("Isolated attached transform");follower.transform.SetParent(parent.transform,false);follower.transform.localPosition=new Vector3(2,4,0);playable.myApplyComponent=follower.transform;playable.myAttach=config;playable.myDuration=-1;playable.TryPlay();
            Require(follower.transform.parent==bone.transform&&Vector3.Distance(follower.transform.localPosition,config.myOffset)<.0001f&&Quaternion.Angle(follower.transform.localRotation,config.myRotation)<.001f&&Vector3.Distance(follower.transform.localScale,Vector3.one*.8f)<.0001f,"original playable attaches with exact local position/rotation/scale");
            playable.Stop(true);yield return null;center.Update(0);center.LateUpdate();Require(follower.transform.parent==parent.transform,"original stop restores captured parent");center.RemoveAll();
            UnityEngine.Object.Destroy(follower);UnityEngine.Object.Destroy(probe);UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(skill);UnityEngine.Object.Destroy(damage);UnityEngine.Object.Destroy(parent);yield return null;
        }
        private sealed class SoundProbe:IProfessionSoundHost
        {
            public readonly List<uint> Events=new List<uint>();public readonly List<GameObject> Emitters=new List<GameObject>();
            public uint PostEvent(uint eventId,GameObject emitter){Events.Add(eventId);Emitters.Add(emitter);return (uint)Events.Count;}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
