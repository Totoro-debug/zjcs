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
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.EntityRadarFastKill;
using Zjcs.Offline.World.NativeMap14RadarFastKill;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RadarFastKillAcceptance
    {
        private const string Key = "Map14.RadarFastKill";
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
                Debug.Log("Map14 original radar fast-game tip Play Mode passed: native no-camp and fixed fast-game rule calls; original service caching, missing-camp guard, region/player/read order, stage redirection, no-fast-component branch and argument order; cost/combined/radar/award/input/component/index/container/loading/four-statue regression passed. Camp-positive/parser-dependent rules, original tip prefab and formal map/battle/input/UI/save/Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var catalog=Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using(var shared=new NativeMap14SharedAssemblies(catalog))
            using(var config=new NativeMap14SharedConfiguration(shared))
            {
                var queries=new NativeMap14RadarFastKillQueries(shared,Activator.CreateInstance(shared.CommonType("EntityHandler")));
                Require(!queries.IsFastGame(14,1,0,1,0,0),"Never rule did not return false");
                for(int mode=1;mode<=3;mode++)Require(queries.IsFastGame(14,1,mode,1,0,0),"original fixed fast-game rule differs: "+mode);
                var native=new NativeMap14SharedPrefabs(shared,catalog).CreateClientPrefab(6570);
                using(var view=new NativeMap14SharedViewEntity(shared,native))
                {
                    Require(!queries.TryGetCampOwnerEntity(view,out var owner) && owner==null,"original no-camp owner branch differs");
                    NativeMap14RadarFastKillQueries.BindStageFast(shared,view);
                    Require(view.GetComponent<IRadarStageFast>()==null,"non-fast statue gained a fast component");
                    var host=new GameObject("Original fast-game hint entity fixture");
                    var entity=host.AddComponent<MyEntity>();entity.Entity=view;
                    host.AddComponent<MyGridTransform>().Bind();
                    var position=entity.myGridTransform.Pos;
                    var events=new List<string>();var services=new Services(events,position);
                    var source=new Entity(events,1);var target=new Entity(events,2);
                    source.Stage.Redirect=target.Stage;target.Fast=new Fast{Mode=3};
                    entity.Entity=source;
                    var tipHost=new GameObject("Original fast-game hint component fixture");
                    var tip=tipHost.AddComponent<MapControllerEntityRadarTipFastKill>();tip.Services=services;
                    services.HasCamp=true;services.CampAvailable=false;
                    Require(!tip.CanShowTip(entity),"missing camp entity did not suppress hint");
                    Require(string.Join(",",events)=="root,position,owner,lo:901,camp","missing-camp guard did not precede region/player/rules");
                    events.Clear();services.CampAvailable=true;
                    Require(tip.CanShowTip(entity),"redirected fast stage did not show hint");
                    Require(string.Join(",",events)=="owner,lo:901,camp,region,player,stage:1,fast:2,rule:14:77:3:120:8:123456","original cached service/read/redirect/rule argument order differs: "+string.Join(",",events));
                    events.Clear();target.Fast=null;
                    Require(!tip.CanShowTip(entity) && !events.Exists(v=>v.StartsWith("rule:")),"missing redirected fast component called game rule");
                    events.Clear();services.HasCamp=false;source.Stage.Redirect=null;source.Fast=new Fast{Mode=2};
                    Require(tip.CanShowTip(entity) && string.Join(",",events)=="owner,region,player,stage:1,fast:1,rule:14:77:2:120:8:123456","direct stage or no-owner branch changed");
                    services.RuleResult=false;Require(!tip.CanShowTip(entity),"non-fast rule result did not suppress hint");
                    entity.Entity=view;
                    UnityEngine.Object.Destroy(tipHost);UnityEngine.Object.Destroy(host);yield return null;
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14RadarCostAcceptance).GetMethod("Steps",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }
        private sealed class Services:IRadarFastKillServices,IRadarFastKillRoot,IRadarFastKillPosition,IRadarFastKillQueries
        {
            private readonly List<string> events;private readonly Pos2d expected;
            public bool HasCamp,CampAvailable,RuleResult=true;
            public Services(List<string> events,Pos2d expected){this.events=events;this.expected=expected;}
            public IRadarFastKillRoot ResolveRoot(MyEntity entity){events.Add("root");return this;}
            public IRadarFastKillPosition ResolvePosition(MyEntity entity){events.Add("position");return this;}
            public IRadarFastKillQueries Queries=>this;
            public int CurrentMapClassId=>14;
            public MyEntity GetEntity(EcEntityIdentifier id){events.Add("camp");return CampAvailable?FindObject():null;}
            private MyEntity FindObject()=>UnityEngine.Object.FindObjectOfType<MyEntity>();
            public int QueryRegion(Pos2d position){Require(position==expected,"region query position differs");events.Add("region");return 77;}
            public RadarFastKillPlayer MainPlayer{get{events.Add("player");return new RadarFastKillPlayer{Level=120,SubRank=8,CombatRating=123456};}}
            public EcEntityIdentifier GetEntIdByLoEntId(int id){events.Add("lo:"+id);return new EcEntityIdentifier(14,id);}
            public bool TryGetCampOwnerEntity(ECViewEntityData entity,out RadarFastKillCampOwner owner)
            {events.Add("owner");owner=HasCamp?new RadarFastKillCampOwner{ClassId=901}:null;return HasCamp;}
            public bool IsFastGame(int map,int region,int type,int level,int rank,long combat)
            {events.Add($"rule:{map}:{region}:{type}:{level}:{rank}:{combat}");return RuleResult;}
        }
        private sealed class Entity:ECViewEntityData
        {
            private readonly List<string> events;private readonly int id;
            public readonly Stage Stage;public Fast Fast;
            public Entity(List<string> events,int id){this.events=events;this.id=id;Stage=new Stage(this);}
            public override int ClassId=>id;public override EcEntityIdentifier Id=>new EcEntityIdentifier(14,id);
            public override T GetComponent<T>()
            {if(typeof(T)==typeof(LegacyWorldStageComponentData)){events.Add("stage:"+id);return (T)(object)Stage;}if(typeof(T)==typeof(IRadarStageFast)){events.Add("fast:"+id);return (T)(object)Fast;}return default;}
        }
        private sealed class Fast:IRadarStageFast{public int Mode;public int GetMapFastType()=>Mode;}
        private sealed class Stage:LegacyWorldStageComponentData
        {
            private readonly Entity owner;public Stage Redirect;
            public Stage(Entity owner){this.owner=owner;}
            public override LegacyWorldStageComponentData InteractionRedirectStage=>Redirect;
            public override ECViewEntityData Entity=>owner;
            public override int State=>0;
            public override OfficialStageStateInfo GetCurStateInfo()=>null;
            public override int GetStateComponentIndex(StageComponentType type,int subStateIndex=0)=>-1;
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
