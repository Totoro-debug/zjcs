using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private const string Key = "Zjcs.MainlineWorldStartup";
        private static IEnumerator routine;
        private static int errors;

        public static void RunConfiguration()
        {
            SessionState.SetBool(Key + ".configuration", true);
            Run();
        }

        public static void RunClientServices()
        {
            SessionState.SetBool(Key + ".client-services", true);
            Run();
        }

        public static void RunClientPlayer()
        {
            SessionState.SetBool(Key + ".client-player", true);
            Run();
        }

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            Hook();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived += Observe;
            EditorApplication.playModeStateChanged += Mode;
        }

        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredPlayMode) return;
            routine = SessionState.GetBool(Key + ".client-player", false) ? VerifyClientPlayer()
                : SessionState.GetBool(Key + ".client-services", false) ? VerifyClientServices() : Verify();
            EditorApplication.update += Advance;
        }

        private static void Advance()
        {
            try
            {
                if (routine.MoveNext()) return;
                if (errors != 0) throw new InvalidDataException("Original entity startup logged errors: " + errors);
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Finish(int result)
        {
            (routine as IDisposable)?.Dispose();
            EditorApplication.update -= Advance;
            Application.logMessageReceived -= Observe;
            SessionState.EraseBool(Key);
            SessionState.EraseBool(Key + ".configuration");
            SessionState.EraseBool(Key + ".client-services");
            SessionState.EraseBool(Key + ".client-player");
            EditorApplication.Exit(result);
        }

        private static IEnumerator Verify()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            if (!catalog) throw new InvalidDataException("The original shared assemblies are not bound.");
            int maps = 0, total = 0, movers = 0;
            bool configurationOnly = SessionState.GetBool(Key + ".configuration", false);
            foreach (int map in new[] { 10, 11, 12, 13, 14, 15, 16 })
            foreach (int status in new[] { 1, 2 })
            {
                int errorsBefore = errors;
                using (var world = new MainlineSharedWorld(catalog, map, 4000 + map * 10 + status))
                {
                    VerifyTables(world);
                    var load = world.Load(_ => status);
                    while (load.MoveNext()) yield return load.Current;
                    var container = world.Root.Container;
                    var originals = ((IDictionary)NativeMap14SharedPrefabs.Get(container, "EntityDict"))
                        .Values.Cast<object>().ToArray();
                    if (!world.IsLoaded || world.IsReady || originals.Length == 0)
                        throw new InvalidDataException("Original snapshot did not wait for client startup.");
                    foreach (var entity in originals)
                    {
                        var mover = entity.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                            .Invoke(entity, new object[] { world.Assemblies.CommonType("GridMoverComponent") });
                        if (mover == null) continue;
                        var info = NativeMap14SharedPrefabs.Get(mover, "Info");
                        int mask = (int)NativeMap14SharedPrefabs.Get(info, "MoveMask");
                        var parser = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("EntityGridMoverInfoParser"));
                        var walkable = parser.GetType().GetMethod("GetWalkableGroundTypes");
                        var grounds = ((IEnumerable)walkable.Invoke(parser, new[] { NativeMap14SharedPrefabs.Get(info, "WalkableGroundTypeKey") }))
                            .Cast<object>().Select(Convert.ToInt32).ToArray();
                        int expected = grounds.Aggregate(0, (value, ground) => value | (1 << ground));
                        if (mask != expected) throw new InvalidDataException("Original movement mask differs from its official ground table.");
                        var serverGrounds = ((IEnumerable)walkable.Invoke(parser, new[] { NativeMap14SharedPrefabs.Get(info, "WalkableGroundTypeServerOverrideKey") }))
                            .Cast<object>().Select(Convert.ToInt32).ToArray();
                        var parsers = world.Assemblies.Shared.GetType("Parsers", true).GetField("Instance").GetValue(null);
                        foreach (var ground in Enum.GetValues(world.Assemblies.CommonType("GroundType")))
                            if ((bool)info.GetType().GetMethod("Walkable").Invoke(info, new[] { parsers, ground })
                                != (grounds.Contains(Convert.ToInt32(ground)) || serverGrounds.Contains(Convert.ToInt32(ground))))
                                throw new InvalidDataException("Original server override ground branch differs.");
                        movers++;
                    }
                    if (!configurationOnly) world.Start();
                    if (!configurationOnly && (!world.IsReady || originals.Any(entity =>
                        !(bool)NativeMap14SharedPrefabs.Get(entity, "Awaked")
                        || !(bool)NativeMap14SharedPrefabs.Get(entity, "Started"))))
                        throw new InvalidDataException("Original container did not start every prepared entity.");
                    if (errors != errorsBefore)
                        throw new InvalidDataException($"Original map {map}/{status} lifecycle logged {errors - errorsBefore} errors.");
                    total += originals.Length;
                    maps++;
                    Debug.Log($"MAINLINE_{(configurationOnly ? "CONFIGURATION" : "ENTITY_STARTUP")}_PASS: map={map}, status={status}, entities={originals.Length}");
                }
                yield return null;
            }
            foreach (int map in new[] { 10, 11, 12, 13, 14, 15, 16 })
            {
                using (var world = new MainlineSharedWorld(catalog, map, 5000 + map))
                {
                    var load = world.Load();
                    while (load.MoveNext()) yield return load.Current;
                    if (!world.IsReady || !(bool)NativeMap14SharedPrefabs.Get(world.Root.Container, "Started"))
                        throw new InvalidDataException("Original root-only automatic startup regressed: " + map);
                    Debug.Log("MAINLINE_ROOT_STARTUP_PASS: map=" + map);
                }
            }
            if (errors != 0) throw new InvalidDataException("Original world configuration logged errors: " + errors);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), configurationOnly ? "configuration.json" : "entity-startup.json"),
                $"{{\"map_states\":{maps},\"entities\":{total},\"movers\":{movers},\"configuration_only\":{configurationOnly.ToString().ToLowerInvariant()},\"play_mode\":{Application.isPlaying.ToString().ToLowerInvariant()}}}");
        }

        private static void VerifyTables(MainlineSharedWorld world)
        {
            var parser = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("MapLogicEntityTagInfoParser"));
            var rows = (IEnumerable)parser.GetType().GetMethod("GetAll").Invoke(parser, null);
            int count = 0;
            foreach (var row in rows)
            {
                var tag = parser.GetType().GetMethod("GetEntityTag").Invoke(parser, new[] {
                    NativeMap14SharedPrefabs.Get(row, "MapClass"), NativeMap14SharedPrefabs.Get(row, "LogicId") });
                if (!Equals(tag, NativeMap14SharedPrefabs.Get(row, "Tag")))
                    throw new InvalidDataException("Original logical entity tag PostParse index differs.");
                count++;
            }
            if (count == 0) throw new InvalidDataException("Original entity tag table is empty.");
            var conditions = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("MapEntityConditionShowInfoParser"));
            if ((int)conditions.GetType().GetMethod("GetCount").Invoke(conditions, null) == 0)
                throw new InvalidDataException("Original visibility conditions are absent.");
        }
    }
}
