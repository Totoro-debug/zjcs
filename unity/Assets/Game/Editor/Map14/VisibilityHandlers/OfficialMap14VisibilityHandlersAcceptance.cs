using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap14VisibilityHandlers;
using Zjcs.Offline.World.NativeMap14Visibility;
using Zjcs.Offline.World.NativeMap14ClientEntities;
using Zjcs.Official.EntityVisibilityHandlers;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using ClientContainer=Zjcs.Official.EntityContainers.MyContainer;
using Visibility=Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14VisibilityHandlersAcceptance
    {
        private const string Key = "Map14.VisibilityHandlers";
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
                Debug.Log("Map14 original visibility handler factory/action consumer Play Mode passed: original enum selection/forbidden flags/FIFO reuse, source hide lifecycle, actual MyAction/native apply identity with original container add event, matching/nonmatching/old/new action branches, real original one-second safety wait and cancellation, native award readiness/event identity/order/receiver teardown; display-data/native visibility/four-statue/cost/radar/input/container/loading and current rendered frames regressions passed. Formal action prefab/effect lifecycle and map/player/UI/input/save/Player remain pending.");
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
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14050))
                using(var state=new NativeMap14ClientEntityState(shared,root,source,configuration,1,()=>false))
                {
                    root.Start();var node=new GameObject("Original visibility handler client container");
                    var container=node.AddComponent<ClientContainer>();container.SharedContainer=state;container.SharedServices=state;container.MyContainerHandler=new Registry();
                    var scope=new NativeMap14VisibilityHandlerScope(container);var factory=(Visibility.IEntityClientVisibilityHandlerFactory)scope.Factory;
                    var target=Entity(shared,14050,7);var visibility=Activator.CreateInstance(shared.CommonType("VisibilityComponent"));Add(shared,target,visibility);
                    var targetView=state.Wrap(target);var nativeVisibility=NativeMap14VisibilityData.Bind(shared,targetView);
                    var owner=Owner(container,node.transform,targetView,"Original target");
                    try
                    {
                        Require(factory.GetHandler(Visibility.ClientVisibilityType.NPCVisibility)==null&&factory.GetHandler(Visibility.ClientVisibilityType.ConditionVisibility)==null&&factory.GetHandler(Visibility.ClientVisibilityType.None)==null,"original forbidden/empty flags produced handlers");
                        var all=new List<Visibility.IClientVisibilityHandler>();factory.GetHandlers((Visibility.ClientVisibilityType)31,ref all);
                        Require(all.Count==3&&all[0] is ClientVisibilityHideHanlder&&all[1] is ClientVisibilityWaitActionHandler&&all[2] is ClientVisibilityWaitAwardItemInitedHandler,"original enum order/handler selection differs");
                        var first=all[1];first.Init(targetView,false);Require(first.Visible,"existing entity action wait was hidden");first.Release();factory.ReturnHanlder(first);
                        var second=factory.GetHandler(Visibility.ClientVisibilityType.WaitRefreshAction);Require(ReferenceEquals(second,first),"original handler FIFO did not reuse the released instance");
                        var another=factory.GetHandler(Visibility.ClientVisibilityType.WaitRefreshAction);Require(!ReferenceEquals(another,second),"borrowed handler was issued twice");
                        second.Init(targetView,false);another.Init(targetView,false);second.Release();another.Release();factory.ReturnHanlder(second);factory.ReturnHanlder(another);
                        Require(ReferenceEquals(factory.GetHandler(Visibility.ClientVisibilityType.WaitRefreshAction),second)&&ReferenceEquals(factory.GetHandler(Visibility.ClientVisibilityType.WaitRefreshAction),another),"original handler return order differs");
                        var hide=all[0];hide.Init(targetView,false);Require(!hide.Visible&&ReferenceEquals(hide.Entity,targetView),"original hide initialization differs");hide.Release();Require(ReferenceEquals(hide.Entity,targetView),"original hide release unexpectedly cleared base entity");factory.ReturnHanlder(hide);
                        var noAward=all[2];noAward.Init(targetView,false);Require(noAward.Visible,"missing award component did not immediately show");noAward.Release();factory.ReturnHanlder(noAward);
                        var wait=second;int changed=0;wait.OnVisibleChanged+=_=>changed++;wait.Init(targetView,true);
                        Require(!wait.Visible&&Receivers(container.EntityAddEvent)==1,"new entity action wait did not subscribe");
                        var wrong=Action(shared,state,container,node.transform,8,new EcEntityIdentifier(14050,999));
                        Require(!wait.Visible,"nonmatching original action apply identity showed the target");
                        var matching=Action(shared,state,container,node.transform,9,targetView.Id);
                        Require(wait.Visible&&changed>0,"matching original MyAction/container add event did not show the target");wait.Release();
                        Require(wait.Entity==null&&Receivers(container.EntityAddEvent)==0&&Token(wait)==null,"action release retained source owner/receiver/token");
                        wait.Init(targetView,true);Require(!wait.Visible,"new entity safety wait started visible");
                        float deadline=Time.realtimeSinceStartup+2;
                        while(!wait.Visible&&Time.realtimeSinceStartup<deadline)yield return null;
                        Require(wait.Visible&&Token(wait)==null&&Receivers(container.EntityAddEvent)==1,"original one-second safety wait/token/subscription behavior differs");wait.Release();
                        wait.Init(targetView,true);wait.Release();int releasedChanges=changed;
                        float until=Time.realtimeSinceStartup+1.1f;while(Time.realtimeSinceStartup<until)yield return null;
                        Require(changed==releasedChanges&&Receivers(container.EntityAddEvent)==0,"cancelled original safety wait changed released handler state");
                        var awardEntity=Entity(shared,14050,10);var award=Activator.CreateInstance(shared.CommonType("StageResultAwardItemComponent"));var awardVisibility=Activator.CreateInstance(shared.CommonType("VisibilityComponent"));
                        var info=Activator.CreateInstance(shared.CommonType("StageResultAwardItemComponentInfo"));
                        NativeMap14SharedPrefabs.Set(info,"AwardItemInfos",Activator.CreateInstance(typeof(List<>).MakeGenericType(shared.CommonType("StageSingleAwardItemInfo"))));
                        NativeMap14SharedPrefabs.Set(award,"_Info",info);Add(shared,awardEntity,award);Add(shared,awardEntity,awardVisibility);
                        var awardView=state.Wrap(awardEntity);var awardData=NativeMap14VisibilityAwardItemData.Bind(shared,awardView);var awardFlags=NativeMap14VisibilityData.Bind(shared,awardView);
                        Require(ReferenceEquals(awardData,NativeMap14VisibilityAwardItemData.Bind(shared,awardView))&&Receivers(NativeMap14SharedPrefabs.Get(award,"OnStageResultAwardItemUpdated"))==1,"native award projection identity/receiver differs");
                        var ready=factory.GetHandler(Visibility.ClientVisibilityType.WaitAwardItemInited);ready.Init(awardView,false);
                        Require(!ready.Visible&&Receivers(awardData.OnStageResultAwardItemUpdated)==1,"pending original award did not subscribe/hide");
                        awardFlags.ClientVisibilityType=Visibility.ClientVisibilityType.WaitAwardItemInited;
                        var order=new List<string>();ready.OnVisibleChanged+=_=>order.Add("ready");var observer=new FlagObserver(order);awardFlags.OnClientVisibilityUpdate+=observer;
                        award.GetType().GetMethod("SetAwardItemData").Invoke(award,new object[]{325,1L});
                        Require(ready.Visible&&awardFlags.ClientVisibilityType==Visibility.ClientVisibilityType.None&&string.Join(",",order)=="ready,flags"&&Receivers(awardData.OnStageResultAwardItemUpdated)==0,"original award update/visibility event order or unsubscribe differs");
                        ready.Release();Require(ready.Entity==null,"award release retained owner");awardFlags.OnClientVisibilityUpdate-=observer;
                        var immediate=factory.GetHandler(Visibility.ClientVisibilityType.WaitAwardItemInited);immediate.Init(awardView,false);Require(immediate.Visible&&Receivers(awardData.OnStageResultAwardItemUpdated)==0,"ready original award did not immediately show");immediate.Release();
                        awardView.Dispose();Require(Receivers(NativeMap14SharedPrefabs.Get(award,"OnStageResultAwardItemUpdated"))==0,"view disposal retained native award subscription");
                    }
                    finally
                    {
                        NativeMap14SharedPrefabs.Set(root.Container,"Destroying",true);UnityEngine.Object.Destroy(node);
                    }
                    yield return null;
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14DisplayDataAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Entity(NativeMap14SharedAssemblies shared,long containerId,int entityId)
        {
            var entity=Activator.CreateInstance(shared.CommonType("EcEntity"));NativeMap14SharedPrefabs.Set(entity,"ClassId",6570);
            NativeMap14SharedPrefabs.Set(entity,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),containerId,entityId));return entity;
        }
        private static void Add(NativeMap14SharedAssemblies shared,object entity,object component)
        {entity.GetType().GetMethod("AddComponent",new[]{shared.CommonType("EcComponent")}).Invoke(entity,new[]{component});}
        private static Zjcs.Official.EntityLifecycle.MyEntity Owner(ClientContainer container,Transform parent,ECViewEntityData view,string name,bool action=false)
        {
            var node=new GameObject(name);node.transform.SetParent(parent,false);var owner=node.AddComponent<Zjcs.Official.EntityLifecycle.MyEntity>();
            if(action)node.AddComponent<MyAction>();owner.Bind(container,view,false);
            typeof(ClientContainer).GetMethod("OnCreatedEntity",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(container,new object[]{owner});return owner;
        }
        private static Zjcs.Official.EntityLifecycle.MyEntity Action(NativeMap14SharedAssemblies shared,NativeMap14ClientEntityState state,ClientContainer container,Transform parent,int id,EcEntityIdentifier apply)
        {
            var entity=Entity(shared,14050,id);var action=Activator.CreateInstance(shared.CommonType("ActionComponent"));Add(shared,entity,action);
            NativeMap14SharedPrefabs.Set(action,"ApplyId",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),apply.ContainerId,apply.EntityId));
            var view=state.Wrap(entity);var projection=NativeMap14ActionComponentData.Bind(shared,view);
            Require(projection.ApplyId==apply&&ReferenceEquals(projection,NativeMap14ActionComponentData.Bind(shared,view)),"original action projection identity/apply data differs");
            return Owner(container,parent,view,"Original MyAction consumer",true);
        }
        private static object Token(Visibility.IClientVisibilityHandler handler)=>handler.GetType().GetField("mCancellationTokenSource",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(handler);
        private static int Receivers(object dispatcher)
        {
            for(var type=dispatcher.GetType();type!=null;type=type.BaseType)
            {
                var field=type.GetField("mRecordEventReceivers",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(field!=null)return ((ICollection)field.GetValue(dispatcher)).Count;
            }
            throw new MissingFieldException("Original receiver storage is absent.");
        }
        private sealed class FlagObserver:Visibility.IEcEntityClientVisibilityUpdatedEventReceiver
        {
            private readonly List<string> order;public FlagObserver(List<string> order){this.order=order;}
            public void EntityClientVisibilityComponent_Updated(Visibility.Map13VisibilityData value){order.Add("flags");}
        }
        private sealed class Registry:IMyContainerHandler
        {
            public ClientContainer MyContainer=>null;public IClientEntityContainer Container=>null;public EntityGridGroundData GridGround=>null;
            public Zjcs.Official.EntityLifecycle.MyEntity MyRootEntity=>null;public ECViewEntityData RootEntity=>null;
            public void RegisterContainer(ClientContainer container,IClientEntityContainer state){}
            public void UnregisterContainer(){}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}

