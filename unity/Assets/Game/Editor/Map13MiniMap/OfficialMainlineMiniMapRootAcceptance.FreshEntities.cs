using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        [Serializable] private sealed class FreshGrid { public FreshPosition Pos; public int Dir; public bool FlipX; }
        [Serializable] private sealed class FreshPosition { public int X, Y; }
        [Serializable] private sealed class FreshStage { public int State; }
        [Serializable] private sealed class FreshActive { public bool Active; }
        [Serializable] private sealed class FreshSource
        {
            public FreshGrid LoGridTransformComponent;
            public FreshStage LoWorldStageComponent;
            public FreshActive LoWorldActiveableEntityComponent;
        }

        private static IEnumerator VerifyFreshEntities()
        {
            object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            int tested = 0, spawned = 0, blocked = 0;
            VerifySharedWorldCancellation(catalog);
            var failures = VerifyFreshEntityCancellation(catalog);
            while (failures.MoveNext()) yield return failures.Current;
            foreach (int map in new[] { 10, 11, 12, 13, 14, 15, 16 })
            foreach (int status in new[] { 0, 1, 2 })
            {
                var definition = JsonUtility.FromJson<OfficialMapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"Assets/Official/World/Map/{map}/map{map}.json").text);
                var sources = (definition.EntityComponents ?? Array.Empty<OfficialMapEntityComponents>()).ToDictionary(row => row.EntityId,
                    row => JsonUtility.FromJson<FreshSource>(row.Json));
                using (var world = new MainlineSharedWorld(catalog, map, 2000L + map * 10 + status))
                {
                    var shared = world.Assemblies;
                    object Component(object entity, string name) => entity.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                        .Invoke(entity, new object[] { shared.CommonType(name) });
                    object container = null, originalIds = null;
                    var created = new List<object>();
                    void InitialState(object entity, OfficialMapEntityPlacement current)
                    {
                        if (!sources.TryGetValue(current.Id, out var raw))
                        {
                            Require(map == 10, "Only the original Map10 export lacks component JSON");
                            var placement = world.Data.GetContainerPrefab(current.Id);
                            var stageInfo = Component(placement, "LoWorldStageComponent");
                            var activeInfo = Component(placement, "LoWorldActiveableEntityComponent");
                            var gridInfo = Component(placement, "LoGridTransformComponent");
                            raw = new FreshSource
                            {
                                LoGridTransformComponent = gridInfo == null ? null : new FreshGrid
                                {
                                    Pos = new FreshPosition { X = current.Grid[0], Y = current.Grid[1] }, Dir = current.Direction,
                                    FlipX = (bool)Get(Get(gridInfo, "Info"), "FlipX")
                                },
                                LoWorldStageComponent = stageInfo == null ? null : new FreshStage { State = (int)Get(Get(stageInfo, "Info"), "State") },
                                LoWorldActiveableEntityComponent = activeInfo == null ? null : new FreshActive { Active = (bool)Get(Get(activeInfo, "Info"), "Active") }
                            };
                        }
                        var grid = Component(entity, "GridTransformComponent");
                        var stage = Component(entity, "WorldStageComponent");
                        var active = Component(entity, "WorldActiveableEntityComponent");
                        if (grid != null && raw.LoGridTransformComponent != null)
                        {
                            var expected = raw.LoGridTransformComponent;
                            var pos = Get(grid, "MyPos");
                            Require((int)Get(pos, "X") == expected.Pos.X && (int)Get(pos, "Y") == expected.Pos.Y
                                && Convert.ToInt32(Get(grid, "Direction")) == expected.Dir && (bool)Get(grid, "FlipX") == expected.FlipX,
                                "Fresh runtime grid matches independently exported placement coordinates");
                        }
                        if (stage != null && raw.LoWorldStageComponent != null)
                            Require((int)Get(stage, "State") == raw.LoWorldStageComponent.State, "Fresh original stage configuration");
                        if (active != null && raw.LoWorldActiveableEntityComponent != null)
                            Require((bool)Get(active, "Active") == raw.LoWorldActiveableEntityComponent.Active, "Fresh original activation configuration");
                    }
                    var load = world.Load(_ => status);
                    bool prepared = false;
                    while (load.MoveNext())
                    {
                        if (world.Root == null || prepared) { yield return load.Current; continue; }
                        prepared = true;
                        container = world.Root.Container; originalIds = world.LoEntityIds;
                        Require(!world.IsReady && !(bool)Get(container, "Started"), "Complete initial snapshot precedes container startup");
                        foreach (var placement in definition.Entities)
                        {
                            var support = status == 1 ? placement.Private : placement.Public;
                            bool expected = status != 0 && support[0] && !support[1];
                            var lookup = new object[] { (long)Get(container, "Id"), placement.Id, null };
                            bool found = (bool)originalIds.GetType().GetMethod("TryGetEntIdByLoEntId").Invoke(originalIds, lookup);
                            Require(found == expected, "Original unknown/private/public spawn gates: " + map + "/" + placement.Id);
                            tested++;
                            if (!found) blocked++;
                            else
                            {
                                var entity = container.GetType().GetMethod("GetEntity").Invoke(container, new[] { lookup[2] });
                                Require(entity != null && (int)Get(entity, "ClassId") == placement.EntityId
                                    && !(bool)Get(entity, "Awaked") && !(bool)Get(entity, "Started"), "Configured snapshot has no early entity lifecycle");
                                InitialState(entity, placement); created.Add(entity); spawned++;
                            }
                        }
                        Require(((IDictionary)Get(container, "EntityDict")).Count == created.Count + 1, "One root and one identity per supported placement");
                        yield return load.Current;
                    }
                    Require(prepared && world.IsLoaded && !world.IsReady && world.Root != null
                        && !(bool)Get(container, "Started"), "Loading a fresh snapshot leaves client startup to its bound host");
                    world.Dispose();
                    Require(world.LoEntityIds == null && ((IDictionary)Get(container, "EntityDict")).Count == 0,
                        "Scope disposal clears original container and mapping: map=" + map + ",status=" + status);
                    Require(created.All(entity => (bool)Get(entity, "Removing") && !(bool)Get(entity, "Ended")
                        && !(bool)Get(entity, "Awaked") && !(bool)Get(entity, "Started")),
                        "Original removal of an unstarted snapshot does not run entity End/Destroy");
                    foreach (var entity in created)
                        Require((int)originalIds.GetType().GetMethod("GetLoEntIdByEntId").Invoke(originalIds, new[] { Get(entity, "Id") }) == -1,
                            "Disposed map releases its original placement mapping");
                }
                Debug.Log($"MAINLINE_FRESH_MAP_PASS: map={map}, status={status}");
                yield return null;
            }
            Require(tested == 33522 && blocked > 0 && spawned > 0, "All seven maps and all original region states");
            File.WriteAllText(Path.Combine(Workspace, "fresh-map-entities.json"),
                $"{{\"placements_tested\":{tested},\"spawned\":{spawned},\"blocked\":{blocked},\"startup_verified\":false}}");
            Debug.Log($"MAINLINE_FRESH_SNAPSHOT_PASS: {tested} placements, {spawned} prepared original entities, {blocked} original spawn gates. Original client startup is pending.");
        }

        private static IEnumerator VerifyFreshEntityCancellation(NativeMap14SharedAssemblyCatalog catalog)
        {
            foreach (bool invalidStatus in new[] { false, true })
            using (var world = new MainlineSharedWorld(catalog, 10, invalidStatus ? 3001 : 3000))
            {
                bool earlyStartRejected = false;
                try { world.Start(); } catch (InvalidOperationException) { earlyStartRejected = true; }
                Require(earlyStartRejected, "Starting before preparation is rejected");
                var load = world.Load(_ => invalidStatus ? 3 : 1);
                object container = null;
                bool rejected = false;
                while (true)
                {
                    bool more;
                    try { more = load.MoveNext(); }
                    catch (ArgumentOutOfRangeException) { rejected = true; break; }
                    if (!more) break;
                    if (world.Root == null) { yield return load.Current; continue; }
                    container = world.Root.Container;
                    ((IDisposable)load).Dispose();
                    break;
                }
                Require(rejected == invalidStatus && world.Root == null && world.Data == null
                    && world.LoEntityIds == null && !world.IsLoaded && !world.IsReady,
                    "Invalid region status and abandoned prepared snapshot release the complete scope");
                if (container != null)
                    Require(((IDictionary)NativeMap14SharedPrefabs.Get(container, "EntityDict")).Count == 0,
                        "Abandoned prepared snapshot releases original entities");
            }
            using (var world = new MainlineSharedWorld(catalog, 10, 3002))
            {
                // Unknown regions create no placed entities. Exercise the
                // explicit start transition with the original root alone.
                var load = world.Load(_ => 0);
                while (load.MoveNext()) yield return load.Current;
                Require(world.IsLoaded && !world.IsReady, "Prepared root awaits explicit startup");
                world.Start();
                Require(world.IsReady, "Explicit original root startup completes");
                bool rejected = false;
                try { world.Start(); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Repeated original container startup is rejected");
            }
        }
    }
}
