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
using Zjcs.Official.EntityContainers;
using Zjcs.Offline.World.NativeMap14ClientEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using ClientContainer = Zjcs.Official.EntityContainers.MyContainer;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14ClientEntityStateAcceptance
    {
        private const string Key = "Map14.ClientEntityStates";
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
                Debug.Log("Map14 native client state Play Mode passed: original container start/destroy/prepare/replace dispatch, stable entity identity across identifier replacement, native dictionary projection and source/ownership/grid writes; original client container/loading/four-statue regression passed. Movement, formal world host/input/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var configuration = new NativeMap14SharedConfiguration(shared))
            using (var data = new NativeMap14SharedData(shared, configuration))
            {
                yield return data.LoadAll(false);
                using (var root = new NativeMap14SharedRoot(shared, configuration, data, 14))
                using (var state = new NativeMap14ClientEntityState(shared, root, data, configuration, 123, () => true))
                {
                    Require(state.Id == 14 && state.ClassId == 14 && !state.Destroying && state.IsMapForbidLowMemoryDeal,
                        "native container identity or explicit memory policy differs");
                    Require(state.UseGameObjectPool == (bool)Get(configuration.Settings, "ECIsUseGameObjectPool"), "native pool setting differs");
                    var rootView = state.RootEntity;
                    Require(ReferenceEquals(rootView, state.RootEntity) && ReferenceEquals(state.EntityDict[rootView.Id], rootView), "root projection identity differs");
                    root.Start();
                    var calls = new List<string>();
                    var source = Activator.CreateInstance(shared.CommonType("EcEntity"));
                    var target = Activator.CreateInstance(shared.CommonType("EcEntity"));
                    source.GetType().GetField("ClassId").SetValue(source, 900001);
                    target.GetType().GetField("ClassId").SetValue(target, 900001);
                    var view = state.Wrap(source); var other = state.Wrap(target);
                    state.SetIdentifier(view, new EcEntityIdentifier(14, 900001));
                    state.SetIdentifier(other, new EcEntityIdentifier(14, 900002));
                    var previous = view.Id;
                    using (state.SubscribeStartEntity((entity, fresh) =>
                    {
                        Require(ReferenceEquals(entity, view) && fresh && (bool)Get(source, "Started") &&
                            ReferenceEquals(state.EntityDict[entity.Id], view), "start was not dispatched after native registration/start");
                        calls.Add("start");
                    }))
                    using (state.SubscribePrepareReplaceEntity((entity, replacement) =>
                    {
                        Require(ReferenceEquals(entity, view) && ReferenceEquals(replacement, other) && entity.Id == previous,
                            "prepare replacement identity/order differs"); calls.Add("prepare");
                    }))
                    using (state.SubscribeReplaceEntity((entity, old) =>
                    {
                        Require(ReferenceEquals(entity, view) && old == previous && entity.Id == other.Id &&
                            !state.EntityDict.ContainsKey(previous) && ReferenceEquals(state.EntityDict[entity.Id], view),
                            "replacement lost existing wrapper or native dictionary ordering"); calls.Add("replace");
                    }))
                    using (state.SubscribeDestroyEntity((entity, userData) =>
                    {
                        Require(ReferenceEquals(entity, view) && userData == null && state.IsRemoving(entity) &&
                            (bool)Get(source, "Ended") && !state.EntityDict.ContainsKey(entity.Id), "destroy was not dispatched after native removal/end");
                        calls.Add("destroy");
                    }))
                    {
                        Require(state.ActiveSubscriptions == 4, "four native event leases are absent");
                        Require((bool)Call(root.Container, "AddEntity", new[] { shared.CommonType("EcEntity") }, source), "native registration failed");
                        Call(root.Container, "PrepareReplaceEntity", new[] { shared.CommonType("EcEntity"), shared.CommonType("EcEntity") }, source, target);
                        Call(root.Container, "ReplaceEntity", new[] { shared.CommonType("EcEntity"), shared.CommonType("EcEntity") }, source, target);
                        Call(root.Container, "RemoveEntityImmediate", new[] { shared.CommonType("EcEntity"), shared.CommonType("MySerializable") }, source, null);
                        Require(calls.SequenceEqual(new[] { "start", "prepare", "replace", "destroy" }), "original event sequence differs");
                    }
                    Require(state.ActiveSubscriptions == 0, "native event leases leaked");
                    var prefab = state.GetCommonPrefab(6570);
                    Require(ReferenceEquals(prefab, state.GetCommonPrefab(6570)), "prefab projection identity differs");
                    var freshEntity = state.Instantiate(prefab);
                    Require(!ReferenceEquals(freshEntity, prefab) && freshEntity.ClassId == 6570 && !state.HasGridGround(freshEntity), "native fresh entity differs");
                    state.SetContainer(freshEntity, state); state.SetIdentifier(freshEntity, new EcEntityIdentifier(14, -1));
                    var sourceState = state.Source(freshEntity); var grid = state.Grid(freshEntity);
                    Require(sourceState != null && grid != null, "official statue source/grid component is absent");
                    sourceState.EntitySourceType = EcEntitySourceType.None;
                    sourceState.Ownership = new EntityOwnershipData { Type = EntityOwnershipType.Player, OwnerId = 123 };
                    grid.Pos = new Pos2d(5, 6);
                    Require(sourceState.EntitySourceType == EcEntitySourceType.None && sourceState.Ownership.Type == EntityOwnershipType.Player &&
                        sourceState.Ownership.OwnerId == 123 && grid.Pos.X == 5 && grid.Pos.Y == 6 && freshEntity.Id == new EcEntityIdentifier(14, -1),
                        "native source/ownership/grid/identifier write failed");
                    Require(state.HasGridGround(freshEntity), "native grid did not resolve original container ground");
                    state.SubscribeReplaceEntity((_, __) => { }); state.Dispose(); state.Dispose();
                    Require(state.ActiveSubscriptions == 0, "state disposal retained native event lease");
                    bool rejected = false; try { state.Wrap(source); } catch (ObjectDisposedException) { rejected = true; }
                    Require(rejected, "disposed state accepted an entity");
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14EntityContainerAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static object Call(object owner, string name, Type[] parameters, params object[] arguments) => owner.GetType().GetMethod(name, parameters).Invoke(owner, arguments);
        private sealed class Runner : MonoBehaviour { }
    }
}
