using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap14DisplayData;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14DisplayDataAcceptance
    {
        private const string Key = "Map14.DisplayData";
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
                Debug.Log("Map14 original display data Play Mode passed: exact original map, area and logical-tag table parsing/postprocessing, local parser scope restoration, original logical identity map and empty/nonempty/container isolation branches, live original display position/removing reads; native visibility/four-statue/cost/radar/input/container/loading and rendered frames regressions passed. Formal map/player/UI/input/save and Windows Player remain pending.");
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
                using(var root=new NativeMap14SharedRoot(shared,configuration,source,14001))
                {
                    var previous=GConfig.FileLoader.sFileLoader;
                    var parsersType=shared.Shared.GetType("Parsers",true);var previousParsers=parsersType.GetField("Instance").GetValue(null);
                    using(var data=new NativeMap14DisplayData(shared))
                    {
                        Require(ReferenceEquals(previous,GConfig.FileLoader.sFileLoader)&&ReferenceEquals(previousParsers,parsersType.GetField("Instance").GetValue(null)),"display parser scope changed global bindings");
                        Require((int)NativeMap14SharedPrefabs.Get(data.GetMapInfo(14),"MapGroupId")==14,"original map group differs");
                        Require((string)NativeMap14SharedPrefabs.Get(data.GetAreaInfo(14,1),"AreaTag")=="Bamboo","original area tag differs");
                        Require(data.GetMapInfo(-1)==null && data.GetAreaInfo(14,-1)==null,"original missing row behavior differs");
                        var displayCatalog=Resources.Load<NativeMap14DisplayCatalog>("Map14/Native/map-display-catalog");
                        var mapObject=new GameObject("Display query fixture root");bool destroying=false,forbid=false;
                        try
                        {
                            var queries=new NativeMap14DisplayQueries(shared,root,data,mapObject.transform,displayCatalog.Selector,()=>destroying,()=>forbid);
                            Require(queries.CurrentMapClassId==14 && queries.MapRoot==mapObject.transform && ReferenceEquals(queries.Selector,displayCatalog.Selector),"original display root/selector binding differs");
                            Require(queries.GetMapGroupId(14)==14 && queries.GetAreaTag(14,1)=="Bamboo","original display map/area query differs");
                            Require((int)queries.GetGridGroundType(new Pos2d(0,0))==2,"original ground query differs");
                            var pos=Activator.CreateInstance(shared.CommonType("Pos2d"),0,0);
                            int area=(int)root.AreaAgent.GetType().GetMethod("GridToArea").Invoke(root.AreaAgent,new[]{pos});
                            Require(queries.QueryArea(new Pos2d(0,0))==area,"original area-agent query differs");
                            Require(!queries.Destroying&&!queries.IsMapForbidLowMemoryDeal(),"explicit host state forwarding differs");
                            destroying=forbid=true;Require(queries.Destroying&&queries.IsMapForbidLowMemoryDeal(),"explicit host state was copied or ignored");
                        }
                        finally {UnityEngine.Object.Destroy(mapObject);}
                        var original=root.Instantiate(source.GetCommonPrefab(6106));
                        var gridType=shared.CommonType("GridTransformComponent");
                        var local=source.GetContainerPrefab(201);
                        var localGrid=local.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(local,new object[]{shared.CommonType("LoGridTransformComponent")});
                        var localInfo=NativeMap14SharedPrefabs.Get(localGrid,"Info");
                        var originalGrid=original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(original,new object[]{gridType});
                        NativeMap14SharedPrefabs.Set(originalGrid,"Pos",NativeMap14SharedPrefabs.Get(localInfo,"Pos"));
                        NativeMap14SharedPrefabs.Set(originalGrid,"Direction",NativeMap14SharedPrefabs.Get(localInfo,"Dir"));
                        NativeMap14SharedPrefabs.Set(originalGrid,"FlipX",NativeMap14SharedPrefabs.Get(localInfo,"FlipX"));
                        NativeMap14SharedPrefabs.Set(original,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14001L,6106));
                        Require((bool)root.Container.GetType().GetMethod("AddEntity").Invoke(root.Container,new[]{original}),"original display-data entity registration failed");
                        NativeMap14SharedPrefabs.Call(original,"Awake");
                        root.Start();
                        using(var view=new NativeMap14SharedViewEntity(shared,original))
                        {
                            var state=new NativeMap14DisplayEntityState(view,data);
                            Require(state.EntityTag==string.Empty,"unmapped original identity produced a tag");
                            data.SetLogicalEntity(201,NativeMap14SharedPrefabs.Get(original,"Id"));
                            Require(state.EntityTag=="\u5c01\u9b54\u5ce1","original logical entity tag differs");
                            Require(state.GridPosition.Equals(view.Grid.Pos)&&state.Removing==(bool)NativeMap14SharedPrefabs.Get(original,"Removing"),"original display state differs");
                            NativeMap14SharedPrefabs.Set(original,"Removing",true);Require(state.Removing,"display removing flag retained a stale copy");
                            NativeMap14SharedPrefabs.Set(original,"Removing",false);Require(!state.Removing,"display removing reset was not forwarded");
                            var pos=state.GridPosition;
                            NativeMap14SharedPrefabs.Set(view.Grid.Original,"Pos",Activator.CreateInstance(shared.CommonType("Pos2d"),pos.X+1,pos.Y));
                            Require(state.GridPosition.X==pos.X+1 && state.GridPosition.Y==pos.Y,"display position retained a stale copy");
                            var foreign=Activator.CreateInstance(shared.CommonType("EcEntity"));var container=Activator.CreateInstance(shared.CommonType("EcContainer"));
                            NativeMap14SharedPrefabs.Set(container,"ClassId",14);NativeMap14SharedPrefabs.Set(container,"Id",14002L);
                            NativeMap14SharedPrefabs.Set(foreign,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14002L,6106));
                            Require((bool)container.GetType().GetMethod("AddEntity").Invoke(container,new[]{foreign}),"foreign-container fixture registration failed");
                            Require(data.GetEntityTag(foreign)==string.Empty,"logical identity crossed original container boundary");
                            NativeMap14SharedPrefabs.Call(container,"RemoveAllEntities");
                        }
                    }
                    Require(ReferenceEquals(previous,GConfig.FileLoader.sFileLoader)&&ReferenceEquals(previousParsers,parsersType.GetField("Instance").GetValue(null)),"display parser disposal changed global bindings");
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14NativeVisibilityAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
