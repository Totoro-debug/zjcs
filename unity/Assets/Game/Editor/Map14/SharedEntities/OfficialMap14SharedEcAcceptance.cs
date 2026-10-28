using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14SharedEcAcceptance
    {
        private const string Key = "Map14.OriginalSharedEc";
        private static bool started;
        private static int errors;
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false;
            errors = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }

        public static void Run()
        {
            if (string.IsNullOrEmpty(Output) || !Directory.Exists(Output))
                throw new InvalidOperationException("An owned validation workspace is required.");
            Debug.Log("Original shared EC source validation; full ProjectTools.Validate, formal map input and Player are not executed here.");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original shared EC validation timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Original shared EC source fixture").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Observe;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error)
                {
                    var messages = new List<string>();
                    for (var current = error; current != null; current = current.InnerException)
                        messages.Add(current.GetType().FullName + ": " + current.Message);
                    Debug.LogError(string.Join("\n", messages));
                    EditorApplication.isPlaying = false;
                    yield break;
                }
                yield return value;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 unchanged original shared EC Play Mode passed: four 11-component client prefabs, original info/lookup/body/stage/lifecycle and grid-ground registration/movement/removal. Formal map input, UI, saves and Player remain unverified.");
            }
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            Require(catalog && catalog.Assemblies.Length == 7, "Original local assembly catalog is incomplete.");
            using (var assemblies = new NativeMap14SharedAssemblies(catalog))
            {
                var prefabs = new NativeMap14SharedPrefabs(assemblies, catalog);
                Func<string, object> create = name => Activator.CreateInstance(assemblies.CommonType(name));
                foreach (var classId in new[] { 6570, 6571, 6572, 6573 })
                {
                    var entity = prefabs.CreateClientPrefab(classId);
                    var components = (IList)Get(entity, "ComponentList");
                    Require((int)Get(entity, "ClassId") == classId && components.Count == 11, "Original client component closure differs.");
                    foreach (var component in components) Require(ReferenceEquals(Get(component, "Entity"), entity), "Original component owner differs.");
                    var grid = Component(entity, assemblies.CommonType("GridTransformComponent"));
                    var stage = Component(entity, assemblies.CommonType("WorldStageComponent"));
                    Require(((IList)Call(Get(grid, "Info"), "GetBodyRange")).Count == 2, "Original statue body differs.");
                    var infos = (IList)Get(Get(stage, "Info"), "StateInfos");
                    Require(infos.Count == 3, "Original statue stage configuration differs.");
                    foreach (var info in infos)
                        foreach (var substate in (IList)Get(info, "SubStateList"))
                            Require(Get(substate, "Select") == null, "Original client formatter's Select discard was lost.");

                    // Fixture host only: use the original container/ground and
                    // bind its root explicitly because global Parsers are not
                    // initialized in this isolated source test.
                    var container = create("EcContainer");
                    var root = create("EcEntity");
                    var ground = create("GridGroundComponent");
                    var groundInfo = create("GridGroundComponentInfo");
                    Set(groundInfo, "Size", Position(assemblies, 4, 4));
                    Call(groundInfo, "Init");
                    Set(ground, "_Info", groundInfo);
                    Call(root, "AddComponent", new[] { assemblies.CommonType("EcComponent") }, ground);
                    container.GetType().GetField("mRootEntity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(container, root);
                    Set(root, "Container", container);
                    Set(entity, "Container", container);
                    // The isolated fixture has no global GameSettings parser;
                    // supply the ground cache consumed by the source getter.
                    grid.GetType().GetField("mGridGround", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, ground);
                    Call(root, "Awake");
                    Call(entity, "Awake");
                    Call(entity, "Start");
                    Require((bool)Get(entity, "Awaked") && (bool)Get(entity, "Started"), "Original lifecycle flags differ.");
                    foreach (var component in components) Require((bool)Get(component, "Awaked"), "Original component did not awake.");
                    var origin = Get(grid, "Pos");
                    var posData = Call(ground, "GetPosData", new[] { assemblies.CommonType("Pos2d") }, origin);
                    Require(ContainsEntity(posData, entity), "Original Start did not register grid occupancy.");

                    foreach (var state in new[] { 2, 1, 0 })
                    {
                        Call(stage, "UpdateState", new[] { typeof(int), assemblies.CommonType("MySerializable") }, state, null);
                        Require((int)Get(stage, "State") == state && ReferenceEquals(Call(stage, "GetCurStateInfo"), infos[state]), "Original stage state lookup differs.");
                        Require((bool)Call(stage, "IsActive"), "Original stage active gate differs.");
                    }
                    var next = Position(assemblies, 1, 1);
                    Set(grid, "Pos", next);
                    Require(Get(grid, "Pos").Equals(next) && ((IList)Get(grid, "RangeList")).Count == 2, "Original grid movement did not update body positions.");
                    posData = Call(ground, "GetPosData", new[] { assemblies.CommonType("Pos2d") }, next);
                    Require(ContainsEntity(posData, entity), "Original movement did not enter grid occupancy.");
                    Call(entity, "End", new[] { assemblies.CommonType("MySerializable") }, new object[] { null });
                    Require((bool)Get(entity, "Ended") && !ContainsEntity(posData, entity), "Original End retained grid occupancy.");
                    foreach (var component in components) Require((bool)Get(component, "Ended"), "Original component did not end.");
                    Call(entity, "Destroy");
                    Debug.Log("Original shared client prefab " + classId + ": 11 components, three source states, real grid registration/movement/removal passed.");
                    yield return null;
                }
            }
            // New host must reuse already loaded modules rather than loading
            // duplicate game assemblies on every map entry.
            var before = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Console");
            using (var again = new NativeMap14SharedAssemblies(catalog))
                Require(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Console") == before, "Map reload duplicated the original shared assembly.");
        }

        private static object Position(NativeMap14SharedAssemblies assemblies, int x, int y)
        { var value = Activator.CreateInstance(assemblies.CommonType("Pos2d")); Set(value, "X", x); Set(value, "Y", y); return value; }
        private static object Component(object entity, Type type) => Call(entity, "GetComponent", new[] { typeof(Type) }, type);
        private static bool ContainsEntity(object position, object entity) =>
            position != null && Get(position, "Entitys") is IEnumerable entities && entities.Cast<object>().Contains(entity);
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
        private static object Call(object owner, string name) => NativeMap14SharedPrefabs.Call(owner, name);
        private static object Call(object owner, string name, Type[] types, params object[] values) => owner.GetType().GetMethod(name, types).Invoke(owner, values);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Runner : MonoBehaviour { }
    }
}
