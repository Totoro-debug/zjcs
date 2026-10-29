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
    public static class OfficialMap14ClientViewBindingsAcceptance
    {
        private const string Key = "Map14.ClientViewBindings";
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
                Debug.Log("Map14 stable client view composition Play Mode passed: original map-display BindPre/Remove consumes live original position, area, ground and logical tag through the same stable native view, ordinary component/visibility/action/award projections installed before client binding; native original factory/action/display/statue/radar/input/container regressions passed. Formal host/UI/player/save/Windows Player remain pending.");
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
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14060))
                using(var data=new NativeMap14DisplayData(shared))
                {
                    var bindings=new ViewBindings(shared,data);
                    using(var state=new NativeMap14ClientEntityState(shared,root,source,configuration,1,()=>false,bindComponents:bindings.Bind))
                    {
                        var original=root.Instantiate(source.GetCommonPrefab(6106));
                        var local=source.GetContainerPrefab(201);var localGrid=Component(shared,local,"LoGridTransformComponent");
                        var localInfo=NativeMap14SharedPrefabs.Get(localGrid,"Info");var grid=Component(shared,original,"GridTransformComponent");
                        NativeMap14SharedPrefabs.Set(grid,"Pos",NativeMap14SharedPrefabs.Get(localInfo,"Pos"));
                        NativeMap14SharedPrefabs.Set(grid,"Direction",NativeMap14SharedPrefabs.Get(localInfo,"Dir"));
                        NativeMap14SharedPrefabs.Set(grid,"FlipX",NativeMap14SharedPrefabs.Get(localInfo,"FlipX"));
                        NativeMap14SharedPrefabs.Set(original,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14060L,6106));
                        Require((bool)root.Container.GetType().GetMethod("AddEntity").Invoke(root.Container,new[]{original}),"original entity registration failed");
                        NativeMap14SharedPrefabs.Call(original,"Awake");root.Start();data.SetLogicalEntity(201,NativeMap14SharedPrefabs.Get(original,"Id"));
                        var view=state.Wrap(original);var display=MapDisplayServices.State(view);
                        Require(ReferenceEquals(view,state.Wrap(original))&&ReferenceEquals(display,NativeMap14DisplayEntityState.Bind(view,data)),"display composition replaced stable view/state identity");
                        bindings.Bind(view);Require(ReferenceEquals(display,MapDisplayServices.State(view)),"repeated composition changed display identity");
                        using(var foreignData=new NativeMap14DisplayData(shared))
                        {
                            bool rejected=false;try{NativeMap14DisplayEntityState.Bind(view,foreignData);}catch(InvalidOperationException){rejected=true;}
                            Require(rejected&&ReferenceEquals(display,MapDisplayServices.State(view)),"a foreign display scope replaced the original binding");
                        }
                        Require(display.EntityTag=="\u5c01\u9b54\u5ce1"&&display.GridPosition.Equals(view.Grid.Pos),"original tag/position did not reach stable display input");
                        var node=new GameObject("Original map display consumer");node.SetActive(false);
                        var owner=node.AddComponent<Zjcs.Official.EntityLifecycle.MyEntity>();var adapter=node.AddComponent<NativeMyEntityBinding>();
                        var selector=node.AddComponent<MyMapDisplay>();typeof(MyMapDisplay).GetField("mMyEntity",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(selector,adapter);
                        var displayCatalog=Resources.Load<NativeMap14DisplayCatalog>("Map14/Native/map-display-catalog");bool destroying=false;
                        var queries=new NativeMap14DisplayQueries(shared,root,data,node.transform,displayCatalog.Selector,()=>destroying,()=>false);
                        var manager=new DisplayRecorder();selector.MapRootManagerProxy=queries;selector.Queries=queries;selector.MyMapPositionHandler=queries;selector.MapDisplayManager=manager;
                        try
                        {
                            node.SetActive(true);owner.Bind(null,view,false);
                            Require(ReferenceEquals(owner.Entity,view)&&ReferenceEquals(adapter.Entity,view)&&manager.Acquires==1,"original owner BindPre did not consume the same native view");
                            Require(manager.ClassId==6106&&manager.MapId==14&&manager.Area==queries.GetAreaTag(14,queries.QueryArea(view.Grid.Pos))&&manager.Ground==queries.GetGridGroundType(view.Grid.Pos)&&manager.Tag==display.EntityTag&&manager.Content==node.transform,"original MyMapDisplay source arguments differ");
                            owner.Remove(null);owner.OnGameObjectPoolReset();Require(manager.Recycles==1,"original owner Remove did not recycle its display");
                            NativeMap14SharedPrefabs.Set(original,"Removing",true);owner.Bind(null,view,false);Require(manager.Acquires==1,"original removing guard acquired another display");owner.OnGameObjectPoolReset();
                            NativeMap14SharedPrefabs.Set(original,"Removing",false);owner.Bind(null,view,false);Require(manager.Acquires==2,"original removing reset did not restore acquisition");
                            destroying=true;owner.Remove(null);owner.OnGameObjectPoolReset();Require(manager.Recycles==1,"original destroying branch recycled its display");
                        }
                        finally{UnityEngine.Object.Destroy(node);}
                        yield return null;
                        Require(view.GetComponent<Visibility.Map13VisibilityData>() is NativeMap14VisibilityData,"visibility composition is absent");
                        var action=Activator.CreateInstance(shared.CommonType("EcEntity"));var raw=Activator.CreateInstance(shared.CommonType("ActionComponent"));
                        action.GetType().GetMethod("AddComponent",new[]{shared.CommonType("EcComponent")}).Invoke(action,new[]{raw});
                        NativeMap14SharedPrefabs.Set(raw,"ApplyId",NativeMap14SharedPrefabs.Get(original,"Id"));
                        var actionView=state.Wrap(action);Require(actionView.GetComponent<ActionComponentData>().ApplyId==view.Id,"action projection was not installed before original consumers");
                        var award=Activator.CreateInstance(shared.CommonType("EcEntity"));raw=Activator.CreateInstance(shared.CommonType("StageResultAwardItemComponent"));
                        award.GetType().GetMethod("AddComponent",new[]{shared.CommonType("EcComponent")}).Invoke(award,new[]{raw});
                        Require(state.Wrap(award).GetComponent<VisibilityAwardItemData>() is NativeMap14VisibilityAwardItemData,"award projection was not installed before original consumers");
                    }
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14VisibilityHandlersAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Component(NativeMap14SharedAssemblies shared,object entity,string type)=>entity.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity,new object[]{shared.CommonType(type)});
        private sealed class DisplayRecorder:IMapDisplayManager
        {
            public int Acquires,Recycles,ClassId,MapId;public string Area,Tag;public Zjcs.Official.World.Map13Entities.GroundType Ground;public Transform Content;
            private readonly Token token=new Token();
            public IECViewDriver Acquire(int classId,int mapClassId,string areaTag,Zjcs.Official.World.Map13Entities.GroundType groundType,string tag,Transform content)
            {Acquires++;ClassId=classId;MapId=mapClassId;Area=areaTag;Ground=groundType;Tag=tag;Content=content;return token;}
            public void Recycle(int classId,int mapClassId,string areaTag,Zjcs.Official.World.Map13Entities.GroundType groundType,string tag,IECViewDriver driver)
            {Require(classId==ClassId&&mapClassId==MapId&&areaTag==Area&&groundType==Ground&&tag==Tag&&ReferenceEquals(driver,token),"original cached display recycle arguments differ");Recycles++;}
            private sealed class Token:IECViewDriver
            {
                public int InstanceID=>6106;
                public void MyEntity_OnStart(ECViewEntityBinding entity,bool isNew){}
                public void MyEntity_OnRemove(ECViewEntityBinding entity){}
            }
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
