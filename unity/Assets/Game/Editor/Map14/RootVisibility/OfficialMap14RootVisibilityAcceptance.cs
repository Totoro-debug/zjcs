using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.MapRootVisibility;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14RootVisibility;
using Zjcs.Offline.World.NativeMap14ClientEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using ClientContainer=Zjcs.Official.EntityContainers.MyContainer;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14RootFog;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RootVisibilityAcceptance
    {
        private const string Key = "Map14.RootVisibility";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            else Debug.Log("Explicit entity container assembly isolation; full ProjectTools.Validate is not executed.");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            Application.logMessageReceived -= Observe;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original entity container check timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original entity container check").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object value;
                try
                {
                    var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
                    value = top.Current;
                    if (value is IEnumerator nested && !(value is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original root visibility Play Mode passed: original MyGridVisibilityAgent bind/start/end with actual deferred container Init; original native update/single/multiple unlock events, duplicates and removal order, queued reveal, source-position override/transfer/UI/native branches, target notifications and complete teardown; fog/root/container/display regressions passed. Formal root/player/transfer views, map interaction/save and Windows Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var catalog=Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using(var shared=new NativeMap14SharedAssemblies(catalog))
            using(var configuration=new NativeMap14SharedConfiguration(shared))
            using(var source=new NativeMap14SharedData(shared,configuration))
            {
                yield return source.LoadAll(false);
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14100))
                using(var state=new NativeMap14ClientEntityState(shared,root,source,configuration,1,()=>false,bindComponents:new NativeMap14RootVisibilityBindings(shared).Bind))
                {
                    root.Start();var view=state.Wrap(root.Root);var input=view.GetComponent<FogVisibilityAgentData>();Require(input!=null,"native root visibility missing");
                    var native=input.Original;var nativeHandle=NativeMap14SharedPrefabs.Get(native,"Handle");
                    // Explicit serialized fixture state, not an offline new-game policy.
                    // Original eight-byte compression means all visible; include one
                    // visible run so the other fixture cells remain concealed.
                    var fixture=Activator.CreateInstance(shared.CommonType("MapGroundGridBoolData"),280,280);fixture.GetType().GetMethod("SetPosValue").Invoke(fixture,new object[]{0,0,true});
                    native.GetType().GetField("mVisibilityData",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(native,fixture);
                    native.GetType().GetProperty("Width").GetSetMethod(true).Invoke(native,new object[]{280});native.GetType().GetProperty("Height").GetSetMethod(true).Invoke(native,new object[]{280});
                    Require((int)NativeMap14SharedPrefabs.Get(native,"Width")==280&&!input.GetPosVisibility(140,140)&&input.GetPosVisibility(0,0),"original serialized fixture visibility was not restored");
                    int beforeUpdate=Count(native,"OnPosVisibilityUpdate"),beforeOne=Count(native,"OnOneCellUnlock"),beforeMulti=Count(nativeHandle,"OnMultiCellsUnlock");
                    var cameraGo=new GameObject("Root visibility render fixture camera");var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=8;camera.aspect=540f/960;camera.transform.position=new Vector3(5,5,-10);
                    var host=new GameObject("Root visibility typed input fixture");host.SetActive(false);
                    var holder=new GameObject("Original client container root fixture");holder.SetActive(false);var container=holder.AddComponent<ClientContainer>();container.myEntitySlot=holder.transform;container.SharedServices=state;container.MyContainerHandler=new Registry();
                    var owner=host.AddComponent<MyEntity>();var agent=host.AddComponent<MyGridVisibilityAgent>();
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/14/RootFog/Prefabs/RootFog.prefab");var fogGo=UnityEngine.Object.Instantiate(prefab,host.transform,false);var fog=fogGo.GetComponent<CloudController2>();fog.Platform=new Platform(camera);agent.CloudController=fog;
                    agent.SetUnlockCellSrcPosOverridePos(new Pos2d(140,140));
                    try
                    {
                        holder.SetActive(true);host.SetActive(true);owner.Bind(container,view,false);
                        var loader=new Loader(owner,()=>Require(!container.Inited&&Count(native,"OnPosVisibilityUpdate")==beforeUpdate+1&&Count(native,"OnOneCellUnlock")==beforeOne,"original deferred initialization order differs"));
                        yield return container.Init(state,loader);
                        Require(owner.myStarted&&container.Inited&&Count(native,"OnOneCellUnlock")==beforeOne+1&&Count(nativeHandle,"OnMultiCellsUnlock")==beforeMulti+1,"original native unlock listeners not installed after Init");
                        var seen=new List<Pos2d>();agent.OnPosVisibilityUpdate+=(p,pre,post)=>{Require(!pre&&post,"native visibility values changed");seen.Add(p);};
                        var positions=new[]{new Pos2d(140,140),new Pos2d(141,140),new Pos2d(142,140)};
                        foreach(var p in positions)Require(fog.Contains(p),"fixture cell already revealed");
                        Unlock(shared,native,positions[0],false);Require(!fog.Contains(positions[0])&&seen.Count==1,"native immediate cell unlock did not remove cloud");
                        Time.timeScale=0;Unlock(shared,native,positions[1],true);Require(fog.Contains(positions[1])&&((IDictionary)Field(fog,"mFadingClouds")).Contains(positions[1]),"native animated unlock did not reveal");
                        agent.BanCloudControllerReveal(true);Unlock(shared,native,positions[2],true);Require(((ICollection)Field(agent,"mBanCloudPosList")).Count==1&&!((IDictionary)Field(fog,"mFadingClouds")).Contains(positions[2]),"original reveal queue differs");
                        agent.BanCloudControllerReveal(false);Require(((ICollection)Field(agent,"mBanCloudPosList")).Count==0&&((IDictionary)Field(fog,"mFadingClouds")).Contains(positions[2]),"original queued reveal not flushed");
                        var order=new List<int>();Action<Pos2d,bool> a=(p,b)=>order.Add(1),b=(p,v)=>order.Add(2);input.OnOneCellUnlock+=a;input.OnOneCellUnlock+=b;input.OnOneCellUnlock+=a;input.OnOneCellUnlock-=a;
                        Unlock(shared,native,new Pos2d(143,140),false);Require(order.Count==2&&order[0]==1&&order[1]==2,"native duplicate remove/order semantics differ");input.OnOneCellUnlock-=a;input.OnOneCellUnlock-=b;
                        int multi=0;input.Handle.OnMultiCellsUnlock+=(p,animated)=>{Require(animated&&p.Count>0,"original multi event payload differs");multi++;};
                        var entity=root.Instantiate(source.GetCommonPrefab(6570));NativeMap14SharedPrefabs.Set(entity,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14100L,100));var grid=Component(shared,entity,"GridTransformComponent");NativeMap14SharedPrefabs.Set(grid,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),243,130));
                        Require((bool)root.Container.GetType().GetMethod("AddEntity").Invoke(root.Container,new[]{entity})&&multi==1,"actual original container start did not dispatch multi unlock");
                        int target=0;agent.OnTargetPosRemove+=p=>target++;agent.AddTargetPos(positions[0]);agent.OnTargetRemove(positions[0]);Require(target==1&&agent.RemoveTargetPos(positions[0]),"original target notification differs");agent.OnTargetRemove(positions[0]);Require(target==1,"removed target still notified");
                        var player=new Player{Entity=owner};agent.Player=player;agent.SetUnlockCellSrcPosOverridePos(new Pos2d(7,8));Require(Origin(agent)==new Pos2d(7,8)&&player.Reads==0,"override did not precede player access");
                        agent.SetUnlockCellSrcPosOverridePos(null);player.Transfer=new Transfer();Require(Origin(agent)==new Pos2d(3,4),"original transfer component origin differs");player.Transfer=null;player.Ui=new Pos2d(5,6);Require(Origin(agent)==new Pos2d(5,6),"original UI character fallback differs");player.Ui=null;Require(Origin(agent)==new Pos2d(1,2),"original native character fallback differs");
                        // Validate the actual original character FindAction/GridTransform consumed ABI.
                        var configManager=configuration.GetType().GetProperty("Manager").GetValue(configuration);var pack=configManager.GetType().GetMethod("GetParser",new[]{typeof(Type)}).Invoke(configManager,new[]{shared.CommonType("MapRootEntityLiteInfoPackParser")});int cls=(int)pack.GetType().GetMethod("GetMapCharacterEntityClassId").Invoke(pack,new object[]{14});var characterEntity=root.Instantiate(source.GetCommonPrefab(cls));var character=Component(shared,characterEntity,"CharacterComponent");
                        var nativePlayer=new NativeMap14FogPlayerInputs(shared,()=>character,id=>null,e=>null,()=>null);Require(ReferenceEquals(nativePlayer.Character,nativePlayer.Character),"native character view identity differs");var actionId=nativePlayer.Character.FindWorldTransferAction();var characterPosition=nativePlayer.Character.GridPos;Require(characterPosition==view.Grid?.Pos||characterPosition==new Pos2d(0,0),"native character position was replaced");
                        yield return null;yield return null;
                        container.RemoveAllEntities();Require(Count(native,"OnPosVisibilityUpdate")==beforeUpdate&&Count(native,"OnOneCellUnlock")==beforeOne&&Count(nativeHandle,"OnMultiCellsUnlock")==beforeMulti+1,"original End retained listeners");
                        view.Dispose();Require(Count(nativeHandle,"OnMultiCellsUnlock")==beforeMulti,"view disposal retained direct native event lease");
                    }
                    finally{Time.timeScale=1;UnityEngine.Object.Destroy(holder);UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(cameraGo);}
                    yield return null;
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14RootFogAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static Pos2d Origin(MyGridVisibilityAgent a)=>(Pos2d)a.GetType().GetMethod("GetUnlockCellSrcPos",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(a,null);
        private static void Unlock(NativeMap14SharedAssemblies s,object native,Pos2d p,bool animated){var pos=Activator.CreateInstance(s.CommonType("Pos2d"),p.X,p.Y);Require((bool)native.GetType().GetMethod("UnlockOneCell").Invoke(native,new object[]{pos,animated}),"original cell was already unlocked");}
        private static object Component(NativeMap14SharedAssemblies s,object owner,string name)=>owner.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(owner,new object[]{s.CommonType(name)});
        private static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        private static int Count(object owner,string name)=>((Delegate)Field(owner,name))?.GetInvocationList().Length??0;
        private sealed class Platform:IRootFogPlatform{public Camera MapCamera{get;}public bool IsMapScene=>true;public Platform(Camera camera){MapCamera=camera;}}
        private sealed class Player:IFogVisibilityPlayer{public int Reads;public MyEntity Entity;public IFogTransferClient Transfer;public Pos2d? Ui;public FogCharacterData Character{get{Reads++;return new Character();}}public MyEntity GetEntity(EcEntityIdentifier id)=>Entity;public IFogTransferClient GetTransferClient(MyEntity e)=>Transfer;public Pos2d? MyMeUiGridPos=>Ui;}
        private sealed class Character:FogCharacterData{public override Pos2d GridPos=>new Pos2d(1,2);public override EcEntityIdentifier FindWorldTransferAction()=>new EcEntityIdentifier(14100,1);}
        private sealed class Transfer:IFogTransferClient{public FogTransferData Component=>new TransferData();}
        private sealed class TransferData:FogTransferData{public override Pos2d Pos=>new Pos2d(3,4);}
        private sealed class Loader:IEntityLoadHandler
        {
            private readonly MyEntity owner;private readonly Action check;public Loader(MyEntity owner,Action check){this.owner=owner;this.check=check;}
            public IEnumerator LoadForInit(Action<MyEntity> callback){callback(owner);check();yield break;}
            public void PreLoadGameObject(int cls,Action<MyEntity> callback=null){throw new NotSupportedException("Fixture loads the root only");}
            public void CreateMyEntity(ECViewEntityData entity,bool fresh,Action<MyEntity> callback){}
            public void OnBeforeRemoveAllEntities(){}public void DestroyMyEntity(MyEntity value){value.OnGameObjectPoolReset();}public void Dispose(){}
        }
        private sealed class Registry:IMyContainerHandler
        {
            public ClientContainer MyContainer{get;private set;}public IClientEntityContainer Container{get;private set;}public EntityGridGroundData GridGround=>null;public MyEntity MyRootEntity=>MyContainer.MyRootEntity;public ECViewEntityData RootEntity=>Container.RootEntity;
            public void RegisterContainer(ClientContainer view,IClientEntityContainer source){MyContainer=view;Container=source;}public void UnregisterContainer(){MyContainer=null;Container=null;}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
