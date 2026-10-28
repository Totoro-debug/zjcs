using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRadarCost;
using Zjcs.Offline.World.NativeMap14RadarCost;
using Zjcs.Offline.World.NativeMap14EntityRadar;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RadarCostAcceptance
    {
        private const string Key = "Map14.RadarCost";
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
                Debug.Log("Map14 original radar cost query Play Mode passed: native current-stage behaviour, original cached cost identity and source filtering, type/null/empty short circuits and original status-query ordering, tip visibility and empty-init branches; combined/radar/award/input/component/index/container/loading/four-statue regression passed. Tip icon/text/animation prefab binding, formal map/input/UI/save/Player remain pending.");
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
            using(var data=new NativeMap14SharedData(shared,configuration))
            {
                yield return data.LoadAll(false);
                int queried=0; long lastContainer=0;
                var formula=Activator.CreateInstance(shared.CommonType("FormulaHandler"));
                var queries=new NativeMap14RadarStageCostQueries(shared,formula,id=>{queried++;lastContainer=id;return null;});
                var prefabs=new NativeMap14SharedPrefabs(shared,catalog);
                var native=prefabs.CreateClientPrefab(6570);
                NativeMap14SharedPrefabs.Set(native,"Id",Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),14L,6570));
                using(var view=new NativeMap14SharedViewEntity(shared,native))
                {
                    NativeMap14RadarWorldQueries.BindBody(view);
                    var touch=Component(native,shared.CommonType("StageBehTouchComponent"));
                    var source=Component(native,shared.CommonType("EntitySourceComponent"));
                    var grid=Component(native,shared.CommonType("GridTransformComponent"));
                    var cache=(IDictionary)Private(touch,"mCompCostDict");
                    int tili=(int)NativeMap14SharedPrefabs.Get(configuration.Settings,"TiliId");
                    var cost=new Dictionary<int,long>{{tili,20},{325,3}};cache.Add(0,cost);
                    Require(queries.GetStageCost(null)==null && queries.GetStageCost(grid,0)==null && queries.GetStageCost(touch,-1)==null && queried==0,"null/type/negative index branches changed");
                    var result=queries.GetStageCost(touch,0);
                    Require(ReferenceEquals(result,cost) && cost.Count==2 && queried==1 && lastContainer==14,"original cost identity/status call differs");
                    NativeMap14SharedPrefabs.Set(source,"EntitySourceType",Enum.Parse(shared.CommonType("EcEntitySourceType"),"Explore"));
                    result=queries.GetStageCost(touch,0);
                    Require(ReferenceEquals(result,cost) && !cost.ContainsKey(tili) && cost[325]==3 && queried==2,"original explore formula did not mutate same cost dictionary");
                    cost.Clear();cost.Add(tili,20);
                    NativeMap14SharedPrefabs.Set(source,"EntitySourceType",Enum.Parse(shared.CommonType("EcEntitySourceType"),"DemonInvade"));
                    result=queries.GetStageCost(touch,0);
                    Require(ReferenceEquals(result,cost) && cost.Count==0 && queried==2,"empty filtered cost failed to skip status query");
                    var stage=view.Stage.Original;
                    var info=stage.GetType().GetMethod("GetStateBehaviourComponent").Invoke(stage,new object[]{0});
                    var current=NativeMap14SharedPrefabs.Get(info,"Component");
                    var index=(int)NativeMap14SharedPrefabs.Get(info,"CompIndex");
                    Require(ReferenceEquals(current,touch) && index==0,"original statue initial stage behaviour differs");
                    Require(ReferenceEquals(queries.GetStageCost(view),cost),"entity overload did not query original current stage component");
                    var entityHost=new GameObject("Original stage cost entity fixture");
                    var entity=entityHost.AddComponent<MyEntity>();entity.Entity=view;
                    var tipHost=new GameObject("Original cost tip component fixture");tipHost.SetActive(false);
                    var tip=tipHost.AddComponent<MapControllerEntityRadarTipStageCost>();tip.CostQueries=queries;
                    Require(!tip.CanShowTip(null) && !tip.CanShowTip(entity),"cost tip empty/null visibility branches changed");
                    // Empty cost exits before accessing absent icon/text bindings.
                    tip.Init(entity);
                    cost.Add(325,3);Require(tip.CanShowTip(entity),"cost tip failed to show nonempty original dictionary");
                    entity.Entity=null;Require(!tip.CanShowTip(entity),"unbound client entity did not skip visibility");
                    UnityEngine.Object.Destroy(tipHost);UnityEngine.Object.Destroy(entityHost);yield return null;
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14RadarCombinedAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private static object Component(object entity,Type type)=>entity.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity,new object[]{type});
        private static object Private(object owner,string name)
        {
            for(var type=owner.GetType();type!=null;type=type.BaseType)
            {var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(field!=null)return field.GetValue(owner);}
            throw new InvalidDataException("Original private field absent: "+name);
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
