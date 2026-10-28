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
using Zjcs.Official.EntityAwardHook;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14EntityAwardHook;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityAwardHookAcceptance
    {
        private const string Key = "Map14.EntityAwardHook";
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
                Debug.Log("Map14 original entity award hook Play Mode passed: unchanged original AwardManager priority/history handling, native message/source identities, exact entity matching, idempotent hook/unhook, ordered deferred replay, handler removal on destroy and no reward application; world input/component/index/container/loading/statue display regression passed. Formal award/player/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            {
                var original = Activator.CreateInstance(shared.Shared.GetType("AwardManager", true));
                using (var manager = new NativeMap14EntityAwardManager(shared, original))
                {
                    var a = Message(shared, 14, 6570); var b = Message(shared, 14, 6570); var c = Message(shared, 14, 6570);
                    var otherEntity = Message(shared, 14, 6571); var otherMap = Message(shared, 15, 6570);
                    var noSource = Activator.CreateInstance(shared.CommonType("PlayerAwardMsg"));
                    var notMap = Activator.CreateInstance(shared.CommonType("PlayerAwardMsg"));
                    Set(notMap, "SourceData", Activator.CreateInstance(shared.CommonType("AwardSourceData")));
                    var history = (IList)GetPrivate(original, "mMsgList");
                    foreach (var message in new[] { noSource, notMap, otherEntity, a, b }) manager.ShowAward(manager.Wrap(message));
                    Require(history.Count == 5, "original history differs before registration");
                    var host = new GameObject("Original entity award hook fixture");
                    var entity = host.AddComponent<MyEntity>(); entity.Entity = new EntityData();
                    var hook = host.AddComponent<EntityAwardItemHook>(); hook.AwardManager = manager; SetPrivate(hook, "mEntity", entity);
                    hook.Hook(); hook.Hook();
                    var retained = (IList)GetPrivate(hook, "mPlayerAwardMsgs");
                    Require(history.Count == 3 && retained.Count == 2 && ReferenceEquals(((EntityAwardMessageData)retained[0]).Original, a) &&
                        ReferenceEquals(((EntityAwardMessageData)retained[1]).Original, b), "original history capture/order differs");
                    Require(ReferenceEquals(manager.Wrap(a), retained[0]), "native message wrapper identity changed");
                    var source = (EntityMapAwardSourceData)manager.Wrap(a).SourceData;
                    Require(ReferenceEquals(source.Original, NativeMap14SharedPrefabs.Get(a, "SourceData")) && source.EntityId.ContainerId == 14 && source.EntityId.EntityId == 6570,
                        "native map source identity differs");
                    manager.ShowAward(manager.Wrap(c)); manager.ShowAward(manager.Wrap(otherMap));
                    Require(retained.Count == 3 && history.Count == 4, "entity/map matching differs");
                    var dataA = NativeMap14SharedPrefabs.Get(a, "DataList");
                    hook.Unhook(); hook.Unhook();
                    Require(history.Count == 7 && ReferenceEquals(history[4], a) && ReferenceEquals(history[5], b) && ReferenceEquals(history[6], c), "deferred original replay order differs");
                    Require(ReferenceEquals(dataA, NativeMap14SharedPrefabs.Get(a, "DataList")) && GetPrivate(hook, "mPlayerAwardMsgs") == null,
                        "hook changed native reward data or kept released messages");
                    hook.Hook(); Require(history.Count == 4 && ((IList)GetPrivate(hook, "mPlayerAwardMsgs")).Count == 3, "rehook history capture differs");
                    UnityEngine.Object.Destroy(host); yield return null;
                    // Original OnDestroy unregisters without replaying retained messages.
                    var d = Message(shared, 14, 6570); manager.ShowAward(manager.Wrap(d));
                    Require(history.Count == 5 && ReferenceEquals(history[4], d), "destroyed hook retained a handler or replayed buffered messages");
                    var calls = new List<string>();
                    Func<EntityAwardMessageData, bool> low = message => { calls.Add("low"); return true; };
                    Func<EntityAwardMessageData, bool> high = message => { calls.Add("high"); return true; };
                    manager.RegisterHandler(low, EntityAwardHandlerPriority.Low);
                    manager.RegisterHandler(high, EntityAwardHandlerPriority.High);
                    calls.Clear(); manager.ShowAward(manager.Wrap(Message(shared, 14, 1)));
                    Require(calls.SequenceEqual(new[] { "high" }), "original descending priority/first handler consumption differs");
                    manager.RemoveHandler(high); calls.Clear(); manager.ShowAward(manager.Wrap(Message(shared, 14, 2)));
                    Require(calls.SequenceEqual(new[] { "low" }), "original handler removal differs");
                    manager.RemoveHandler(low);
                    int duplicates = 0; Func<EntityAwardMessageData, bool> duplicate = message => { duplicates++; return false; };
                    manager.RegisterHandler(duplicate, EntityAwardHandlerPriority.Medium); manager.RegisterHandler(duplicate, EntityAwardHandlerPriority.Low);
                    duplicates = 0; manager.ShowAward(manager.Wrap(Message(shared, 14, 3))); Require(duplicates == 2, "original duplicate registration differs");
                    manager.RemoveHandler(duplicate); duplicates = 0; manager.ShowAward(manager.Wrap(Message(shared, 14, 4))); Require(duplicates == 1, "original first duplicate removal differs");
                    manager.RemoveHandler(duplicate);
                    var mutable = manager.Wrap(noSource); Require(mutable.SourceData == null, "null source differs");
                    var replacement = NativeMap14SharedPrefabs.Get(a, "SourceData"); Set(noSource, "SourceData", replacement);
                    Require(ReferenceEquals(((EntityMapAwardSourceData)mutable.SourceData).Original, replacement), "changed native source was not read");
                    Set(noSource, "SourceData", null); Require(mutable.SourceData == null, "cleared native source was not read");
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14WorldInputAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static object Message(NativeMap14SharedAssemblies shared, long map, int entity)
        {
            var result = Activator.CreateInstance(shared.CommonType("PlayerAwardMsg"));
            var source = Activator.CreateInstance(shared.CommonType("AsMapEntity"));
            Set(source, "EntityId", Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), map, entity));
            Set(result, "SourceData", source); return result;
        }
        private static object GetPrivate(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void SetPrivate(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
        private sealed class EntityData : ECViewEntityData
        {
            public override int ClassId => 6570;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(14, 6570);
            public override T GetComponent<T>() => default;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
