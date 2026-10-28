using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;


using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.EntityRadarCombined;




namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14RadarCombinedAcceptance
    {
        private const string Key = "Map14.RadarCombined";
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
                Debug.Log("Map14 original combined radar tip Play Mode passed: child filtering and original pooled reuse, scaled LateUpdate hide, callback/cleanup ordering, immediate hide, show cancellation and disable/re-enable completion; radar/award/input/component/index/container/loading/four-statue regression passed. Original tip prefabs, specialized children and formal input/UI/save/Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }

        private static IEnumerator Steps()
        {
            var host = new GameObject("Original combined tip fixture");
            var entity = host.AddComponent<MyEntity>(); entity.Entity = new BodyEntity();
            var managerHost = new GameObject("Original combined tip pool fixture");
            var manager = managerHost.AddComponent<MapControllerEntityRadarTipManager>();
            var childPrefabHost = new GameObject("Original child component fixture"); childPrefabHost.SetActive(false);
            var childPrefab = childPrefabHost.AddComponent<ChildTip>(); childPrefab.Allowed = true;
            var hiddenPrefabHost = new GameObject("Filtered child component fixture"); hiddenPrefabHost.SetActive(false);
            var hiddenPrefab = hiddenPrefabHost.AddComponent<ChildTip>(); hiddenPrefab.Allowed = false;
            var combinedHost = new GameObject("Original combined component fixture"); combinedHost.SetActive(false);
            var combined = combinedHost.AddComponent<MapControllerEntityRadarTipCombinate>();
            Set(combined,"mMapControllerEntityRadarTipPrefabs",new List<MapControllerEntityRadarTipBase>{hiddenPrefab,childPrefab});
            var previous = StoryPlayableInstantiation.Resolve; var previousScale = Time.timeScale;
            var oldCapture = Time.captureFramerate; Time.captureFramerate = 0;
            try
            {
                StoryPlayableInstantiation.Resolve = scope => new Factory();
                Require(combined.CanShowTip(entity),"combined filter did not accept enabled child");
                childPrefab.Allowed = false; Require(!combined.CanShowTip(entity),"combined filter did not reject all children"); childPrefab.Allowed = true;
                combinedHost.SetActive(true); combined.Init(entity); combined.Show();
                var children = Children(combined); Require(children.Count == 1,"filtered child was instantiated");
                var child = (ChildTip)children[0];
                Require(child.Initialized == 1 && child.Shown == 1 && ReferenceEquals(child.MyEntity,entity),"child init/show order or entity differs");
                combined.Init(entity); Require(ReferenceEquals(Children(combined)[0],child) && child.Initialized == 2,"reinitialization did not recycle/reuse child");
                int callbacks = 0; bool clearedBeforeCallback = false;
                Time.timeScale = 0;
                combined.Hide(false, value => { callbacks++; clearedBeforeCallback = Children(combined).Count == 0; });
                Require(callbacks == 0 && child.Hidden == 1 && !child.Immediate,"delayed hide completed synchronously or child flag changed");
                yield return new WaitForSecondsRealtime(.65f);
                Require(callbacks == 0 && Children(combined).Count == 1,"scaled delay ran while time scale was zero");
                Time.timeScale = 1;
                float deadline = Time.realtimeSinceStartup + 3;
                while (callbacks == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Require(callbacks == 1 && clearedBeforeCallback && !child.gameObject.activeSelf && Get(combined,"mCancellationToken") == null,"delayed cleanup/callback/pool order differs");
                combined.Init(entity); combined.Show();
                Require(ReferenceEquals(Children(combined)[0],child),"delayed hide failed to return original child to pool");
                combined.Hide(true, value => callbacks++);
                Require(callbacks == 2 && child.Immediate && Children(combined).Count == 1,"original immediate branch changed child cleanup or callback");
                combined.Show(); combined.Hide(false, value => callbacks++); combined.Show();
                yield return new WaitForSecondsRealtime(.7f);
                Require(callbacks == 2 && Children(combined).Count == 1 && Get(combined,"mCancellationToken") == null,"Show failed to cancel delayed cleanup/callback");
                combined.Hide(false, value => callbacks++); combinedHost.SetActive(false);
                yield return new WaitForSecondsRealtime(.7f);
                Require(callbacks == 2 && Children(combined).Count == 1,"disabled delay fired completion");
                combinedHost.SetActive(true);
                Require(callbacks == 3 && Children(combined).Count == 1,"re-enable did not run original fade completion without clearing children");
                combined.Init(entity); combined.Show();
                Require(ReferenceEquals(Children(combined)[0],child),"re-enable/reinit failed original reuse");
                combined.Hide(false, value => callbacks++);
                combined.Hide(false, value => callbacks++);
                deadline = Time.realtimeSinceStartup + 3;
                while (callbacks == 3 && Time.realtimeSinceStartup < deadline) yield return null;
                Require(callbacks == 4 && Children(combined).Count == 0,"repeated hide did not cancel earlier task or duplicated callback");
            }
            finally
            {
                StoryPlayableInstantiation.Resolve = previous; Time.timeScale = previousScale; Time.captureFramerate = oldCapture;
                UnityEngine.Object.Destroy(combinedHost); UnityEngine.Object.Destroy(childPrefabHost); UnityEngine.Object.Destroy(hiddenPrefabHost);
                UnityEngine.Object.Destroy(managerHost); UnityEngine.Object.Destroy(host);
            }
            yield return null;
            yield return (IEnumerator)typeof(OfficialMap14EntityRadarAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,null);
        }
        private static object Get(object owner,string name) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static void Set(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
        private static List<MapControllerEntityRadarTipBase> Children(object owner) => (List<MapControllerEntityRadarTipBase>)Get(owner,"mMapControllerEntityRadarTips");
        private sealed class ChildTip : MapControllerEntityRadarTipBase
        {
            public bool Allowed; public int Initialized,Shown,Hidden; public bool Immediate;
            public override RadarTipType TipType => RadarTipType.StageCamp;
            public override bool CanShowTip(MyEntity entity) => Allowed;
            public override void Init(MyEntity entity) { base.Init(entity); Initialized++; }
            public override void Show() { base.Show(); Shown++; }
            public override void Hide(bool immidietaly,Action<MapControllerEntityRadarTipBase> callback=null)
            { base.Hide(immidietaly,callback); Hidden++; Immediate=immidietaly; }
        }
        private sealed class BodyEntity : ECViewEntityData,IRadarBodyInfo
        {
            public override int ClassId => 6570; public override EcEntityIdentifier Id => new EcEntityIdentifier(14,1);
            public override T GetComponent<T>() => this is T value ? value : default;
            public Vector2 RealCenter => Vector2.zero;
            public IEnumerable<RadarBodyPosInfo> BodyRangeInfos => new[]{new RadarBodyPosInfo{Pos=new Pos2d(0,0)}};
        }
        private sealed class Factory : IStoryPlayableInstanceFactory
        {
            public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays)
            { Require(!prefab.activeSelf,"factory fixture must be inactive before injection"); var result=UnityEngine.Object.Instantiate(prefab,parent,worldPositionStays);result.name=prefab.name;return result; }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
