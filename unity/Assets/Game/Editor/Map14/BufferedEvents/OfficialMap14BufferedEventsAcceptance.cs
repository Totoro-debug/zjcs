using System;
using System.Linq;
using Zjcs.Official.EntityBufferedEvents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Offline.World.NativeMap14BufferedEvents;
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
using Zjcs.Official.MapDisplays;
using Zjcs.Offline.World.NativeMap14DisplayData;
using Zjcs.Offline.World.NativeMap14EntityLifecycle;
using ViewBindings=Zjcs.Offline.World.NativeMap14ClientViewBindings.NativeMap14ClientViewBindings;
using ClientContainer=Zjcs.Official.EntityContainers.MyContainer;
using Visibility=Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14BufferedEventsAcceptance
    {
        private const string Key = "Map14.BufferedEvents";
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
                Debug.Log("Map14 original buffered entity events Play Mode passed: original signatures, inactive last-context buffering, activation replay to multiple subscribers, active dispatch and unsubscribe, actual native stage/player-removal events, native entity remove/add, dispatcher FIFO reuse and complete teardown; stable view/factory/display/statue/radar/input/container regressions passed. Formal map/display/player/UI/save/Windows Player remain pending.");
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
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14070))
                using(var state=new NativeMap14ClientEntityState(shared,root,source,configuration,1,()=>false))
                {
                    root.Start();var original=Add(shared,root,source,6570,100);var view=state.Wrap(original);var stage=view.Stage.Original;
                    var player=Component(shared,original,"EntityPlayerInteractionComponent");var stageEvent=NativeMap14SharedPrefabs.Get(stage,"StateUpdateEvent");var playerEvent=NativeMap14SharedPrefabs.Get(player,"OnInteractingPlayerRemove");
                    var containerEvent=NativeMap14SharedPrefabs.Get(root.Container,"AddEntityEvent");int previous=Receivers(containerEvent);
                    using(var inputs=new NativeMap14BufferEventInputs(shared,root,state))
                    {
                        Require(Receivers(containerEvent)==previous+1,"native add receiver was not registered");
                        using(var module=new EcEntityBufferEventHandleModule{Inputs=inputs})
                        {
                            module.Initialize();module.Initialize();Require(!module.Active&&Receivers(stageEvent)==1&&Receivers(playerEvent)==1,"original inactive initialization or dispatcher registration differs");
                            var dispatcherValues=((IDictionary)Field(module,"mDispatchers")).Values.Cast<object>().ToArray();Require(dispatcherValues.Length==2,"original dispatcher type filtering differs");
                            var a=new List<EntityWorldStageUpdateEventMessage>();var b=new List<EntityWorldStageUpdateEventMessage>();var players=new List<long>();var api=(IEcEntityBufferEventHandleModule)module;
                            var first=api.Subscribe<EntityWorldStageUpdateEventMessage>(view,a.Add);var second=api.Subscribe<EntityWorldStageUpdateEventMessage>(view,b.Add);var playerLease=api.Subscribe<EntityPlayerInteractionComponentPlayerRemoveEventMessage>(view,value=>players.Add(value.PlayerId));
                            Update(shared,stage,1);Update(shared,stage,2);RemovePlayer(player,41);RemovePlayer(player,42);
                            Require(a.Count==0&&b.Count==0&&players.Count==0&&((IDictionary)Field(module,"mBufferEventContexts")).Count==2,"inactive original events were dispatched or not replaced per signature");
                            module.Activate();Require(module.Active&&a.Count==1&&b.Count==1&&a[0].PreState==1&&ReferenceEquals(a[0],b[0])&&ReferenceEquals(a[0].StageComp,view.Stage)&&players.SequenceEqual(new[]{42L}),"activation did not replay the last original contexts to each subscriber");
                            Require(((IDictionary)Field(module,"mBufferEventContexts")).Count==0,"activation retained original buffered contexts");
                            Update(shared,stage,0);Require(a.Count==2&&b.Count==2&&a[1].PreState==2,"active original stage event was not delivered immediately");
                            first.Dispose();first.Dispose();Update(shared,stage,1);Require(a.Count==2&&b.Count==3,"original handle unsubscribe differs");
                            module.Deactivate();Update(shared,stage,2);var late=new List<EntityWorldStageUpdateEventMessage>();var lateLease=api.Subscribe<EntityWorldStageUpdateEventMessage>(view,late.Add);Require(late.Count==0&&b.Count==3,"inactive subscribe dispatched buffered context");
                            module.Activate();Require(b.Count==4&&late.Count==1&&ReferenceEquals(b[3],late[0]),"reactivation subscriber replay differs");
                            second.Dispose();lateLease.Dispose();Update(shared,stage,0);var unbuffered=new List<EntityWorldStageUpdateEventMessage>();var noReplay=api.Subscribe<EntityWorldStageUpdateEventMessage>(view,unbuffered.Add);Require(unbuffered.Count==0,"active context without subscribers was retained");noReplay.Dispose();playerLease.Dispose();
                            root.Container.GetType().GetMethod("RemoveEntityImmediate",new[]{shared.CommonType("EcEntity"),shared.CommonType("MySerializable")}).Invoke(root.Container,new object[]{original,null});
                            Require(Receivers(stageEvent)==0&&Receivers(playerEvent)==0&&((IDictionary)Field(module,"mDispatchers")).Count==0,"native removal retained original dispatcher subscriptions");
                            original=Add(shared,root,source,6571,101);view=state.Wrap(original);stage=view.Stage.Original;player=Component(shared,original,"EntityPlayerInteractionComponent");
                            var reused=((IDictionary)Field(module,"mDispatchers")).Values.Cast<object>().ToArray();Require(reused.Length==2&&reused.All(value=>dispatcherValues.Any(old=>ReferenceEquals(old,value))),"original removed dispatchers were not reused");
                            int reusedCalls=0;var reuseLease=api.Subscribe<EntityWorldStageUpdateEventMessage>(view,value=>{Require(ReferenceEquals(value.StageComp,view.Stage),"reused dispatcher retained the previous entity");reusedCalls++;});Update(shared,stage,1);Require(reusedCalls==1,"reused dispatcher did not consume new native stage events");
                            module.Dispose();reuseLease.Dispose();
                            Require(Receivers(NativeMap14SharedPrefabs.Get(stage,"StateUpdateEvent"))==0&&Receivers(NativeMap14SharedPrefabs.Get(player,"OnInteractingPlayerRemove"))==0&&((IDictionary)Field(module,"mDispatchers")).Count==0&&((IDictionary)Field(module,"mDispatcherPool")).Count==0,"original dispose retained subscriptions or dispatcher pools");
                        }
                    }
                    Require(Receivers(containerEvent)==previous,"native buffer map disposal retained add receiver");
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14ClientViewBindingsAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Add(NativeMap14SharedAssemblies shared,NativeMap14SharedRoot root,NativeMap14SharedData source,int classId,int id)
        {
            var original=root.Instantiate(source.GetCommonPrefab(classId));NativeMap14SharedPrefabs.Set(original,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14070L,id));
            var grid=Component(shared,original,"GridTransformComponent");NativeMap14SharedPrefabs.Set(grid,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),243,130));
            if(!((IEnumerable)NativeMap14SharedPrefabs.Get(original,"ComponentList")).Cast<object>().Any(value=>value.GetType()==shared.CommonType("EntityPlayerInteractionComponent")))original.GetType().GetMethod("AddComponent",new[]{shared.CommonType("EcComponent")}).Invoke(original,new[]{Activator.CreateInstance(shared.CommonType("EntityPlayerInteractionComponent"))});
            Require((bool)root.Container.GetType().GetMethod("AddEntity").Invoke(root.Container,new[]{original}),"original dynamic entity add failed");
            return original;
        }
        private static object Component(NativeMap14SharedAssemblies shared,object entity,string type)=>entity.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity,new object[]{shared.CommonType(type)});
        private static void Update(NativeMap14SharedAssemblies shared,object stage,int value)=>stage.GetType().GetMethod("UpdateState",new[]{typeof(int),shared.CommonType("MySerializable")}).Invoke(stage,new object[]{value,null});
        private static void RemovePlayer(object component,long player)
        {Require((bool)component.GetType().GetMethod("AddInteractingPlayer").Invoke(component,new object[]{player}),"original player add failed");Require((bool)component.GetType().GetMethod("RemoveInteractingPlayer").Invoke(component,new object[]{player}),"original player removal failed");}
        private static object Field(object owner,string name)=>owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        private static int Receivers(object dispatcher)
        {for(var type=dispatcher.GetType();type!=null;type=type.BaseType){var field=type.GetField("mRecordEventReceivers",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(field!=null)return ((ICollection)field.GetValue(dispatcher)).Count;}throw new MissingFieldException("Original receiver storage is absent.");}
        private sealed class Runner:MonoBehaviour{}
    }
}
