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
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14ClientEntities;

using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14ClientComponentsAcceptance
    {
        private const string Key = "Map14.ClientComponentss";
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
                Debug.Log("Map14 original client components Play Mode passed: all eight original client components bind native component/info identities on all four statues, source/ownership/click queries, native configured/runtime reward and periodicity queries, lifecycle and rebinding; entity index/shared state/client container/loading/four-statue regression passed. Remaining root components, formal native prefabs/input/UI/save and Player remain pending.");
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
                {
                    var bindings = new NativeMap14ClientComponentBindings(shared);
                    using (var state = new NativeMap14ClientEntityState(shared, root, data, configuration, 123, () => true, bindComponents: bindings.Bind))
                    {
                        var host = new GameObject("Original thin client component binding fixture"); host.SetActive(false);
                        var entity = host.AddComponent<MyEntity>();
                        var source = host.AddComponent<MyEntitySource>();
                        var level = host.AddComponent<MyWorldEntityLevel>();
                        host.AddComponent<MyStageBehTouch>();
                        var extra = host.AddComponent<MyStageResultAwardExtraProp>();
                        var status = host.AddComponent<MyStageResultAwardStatusEffect>();
                        host.AddComponent<MyStageResultModifyHpPercent>();
                        var periodicity = host.AddComponent<MyStageConPlayerPeriodicityInteraction>();
                        host.AddComponent<MyStageResultPeriodicityRandomAward>();
                        object previousSource = null;
                        for (int id = 6570; id < 6574; id++)
                        {
                            var view = state.Instantiate(state.GetCommonPrefab(id));
                            entity.Bind(null, view, false);
                            Require(entity.Components.Count == 8, "eight original client components are absent");
                            foreach (var component in entity.Components)
                            {
                                var original = component.GetEcComponent() as EntityClientComponentData;
                                Require(original != null && ReferenceEquals(Get(original.Original, "Entity"),
                                    ((Zjcs.Offline.World.NativeMap14SharedViews.NativeMap14SharedViewEntity)view).Original), "native component owner identity differs");
                                Require(ReferenceEquals(original.ComponentInfo, Get(original.Original, "_Info")), "original info identity differs");
                                if (!(component is IEmptyInfoMyComponent))
                                    Require(ReferenceEquals(component.GetType().GetProperty("Info").GetValue(component), original.ComponentInfo), "generic original Info lookup differs");
                            }
                            Require(!ReferenceEquals(previousSource, source.Component), "lifecycle rebind retained previous component"); previousSource = source.Component;
                            state.Source(view).Ownership = new EntityOwnershipData { Type = EntityOwnershipType.Player, OwnerId = 123 };
                            Require(source.Component.IsMine(EntityOwnershipType.Player, 123) && !source.Component.IsMine(EntityOwnershipType.Player, 124) && source.Component.CanClick(),
                                "original ownership/click query differs");
                            var originalLevel = level.Component.Original;
                            Require(level.Component.Level == (float)Get(originalLevel, "Level") && level.Component.SubRank == Convert.ToInt32(Get(originalLevel, "SubRank")) &&
                                level.Component.LevelType == Convert.ToInt32(Get(originalLevel, "LevelType")), "native level projection differs");
                            Require(Equals(extra.GetAwardExtraPropInfo(0), Call(extra.Component.Original, "GetAwardExtraPropInfo", 0)), "configured nullable reward boxing differs");
                            Require(extra.GetAwardExtraPropInfo(-1) == null, "negative extra-prop index did not preserve null");
                            Require(ReferenceEquals(status.GetAwardStatusEffectInfo(0), Call(status.Component.Original, "GetStatusEffectInfo", 0)), "configured status award identity differs");
                            for (int index = -1; index < 3; index++)
                                Require(periodicity.GetPeriodictyInteractionId(index) == (int)Call(periodicity.Component.Original, "GetPeriodictyInteractionId", index), "configured periodicity query differs");
                            var periodicField = periodicity.Component.Original.GetType().GetField("PeriodicityInteractionGroupIds");
                            periodicField.SetValue(periodicity.Component.Original, new List<int> { 77, 88 });
                            Require(periodicity.GetPeriodictyInteractionId(-1) == -1 && periodicity.GetPeriodictyInteractionId(0) == 77 &&
                                periodicity.GetPeriodictyInteractionId(1) == 88 && periodicity.GetPeriodictyInteractionId(2) == -1, "runtime periodicity override differs");
                            var nativeStatus = Activator.CreateInstance(shared.CommonType("StageResultAwardStatusEffectComponentData"));
                            var effects = new List<int> { 41, 42 }; nativeStatus.GetType().GetField("mAwardStatusEffects").SetValue(nativeStatus, effects);
                            var nativeStatuses = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nativeStatus.GetType())); nativeStatuses.Add(nativeStatus);
                            status.Component.Original.GetType().GetProperty("AwardStatusEffectInfos").SetValue(status.Component.Original, nativeStatuses);
                            Require(ReferenceEquals(status.GetAwardStatusEffectInfo(0), nativeStatus) && ReferenceEquals(Get(status.GetAwardStatusEffectInfo(0), "mAwardStatusEffects"), effects),
                                "runtime status award object/list identity differs");
                            var nativeExtra = Activator.CreateInstance(shared.CommonType("AwardExtraPropInfo"));
                            var props = Activator.CreateInstance(typeof(List<>).MakeGenericType(shared.CommonType("AwardExtraProp")));
                            nativeExtra.GetType().GetField("AwardExtraProps").SetValue(nativeExtra, props);
                            var nativeExtras = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nativeExtra.GetType())); nativeExtras.Add(nativeExtra);
                            extra.Component.Original.GetType().GetProperty("AwardExtraPropInfos").SetValue(extra.Component.Original, nativeExtras);
                            Require(ReferenceEquals(Get(extra.GetAwardExtraPropInfo(0), "AwardExtraProps"), props), "runtime nullable award original list identity differs");
                            bindings.Bind((Zjcs.Offline.World.NativeMap14SharedViews.NativeMap14SharedViewEntity)view);
                            Require(ReferenceEquals(source.Component, view.GetComponent<EntitySourceComponentData>()), "repeat component binding replaced original wrapper");
                            entity.MyStart(); Require(entity.myStarted, "original components did not start");
                            entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset();
                        }
                        UnityEngine.Object.Destroy(host); yield return null;
                    }
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14EntityIndexAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name).Invoke(owner, arguments);
        private sealed class Runner : MonoBehaviour { }
    }
}
