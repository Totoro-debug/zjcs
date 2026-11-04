using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using MessagePack;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        [Serializable] private sealed class SharedMapCatalog { public SharedMapPayload[] Entries; }
        [Serializable] private sealed class SharedMapPayload { public string OriginalPath, Resource; }

        private static byte[] OriginalStageInput(byte[] bytes)
        {
            int[] Fields(int offset)
            {
                if (MessagePackBinary.IsNil(bytes, offset)) return Array.Empty<int>();
                int count = MessagePackBinary.ReadArrayHeader(bytes, offset, out int header);
                var fields = new int[count]; offset += header;
                for (int i = 0; i < count; i++) { fields[i] = offset; offset += MessagePackBinary.ReadNextBlock(bytes, offset); }
                return fields;
            }
            // The selected original SubStageStateInfoFormatter.Deserialize reads
            // field 3 (Select) and deliberately does not assign it to the result.
            // Preserve that original consumption instead of inventing a value.
            var discarded = new System.Collections.Generic.List<int>();
            foreach (int state in Fields(Fields(0)[3]))
                foreach (int sub in Fields(Fields(state)[1]))
                { var fields = Fields(sub); if (fields.Length > 3) discarded.Add(fields[3]); }
            using (var output = new MemoryStream())
            {
                int start = 0;
                foreach (int offset in discarded)
                { output.Write(bytes, start, offset - start); output.WriteByte(0xc0); start = offset + MessagePackBinary.ReadNextBlock(bytes, offset); }
                output.Write(bytes, start, bytes.Length - start);
                return output.ToArray();
            }
        }

        private static void VerifySharedMapComponentBytes(NativeMap14SharedAssemblies shared, NativeMap14SharedData data)
        {
            object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
            var catalog = JsonUtility.FromJson<SharedMapCatalog>(Resources.Load<TextAsset>($"MainlineSharedWorld/{data.MapClassId}/catalog").text);
            var deserialize = typeof(NativeMap14SharedData).GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Instance);
            var resolver = typeof(NativeMap14SharedData).GetField("resolver", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(data);
            foreach (var entry in catalog.Entries.Where(row => row.OriginalPath.StartsWith("Config/EC/", StringComparison.Ordinal)
                && !row.OriginalPath.StartsWith("Config/EC/MapRootEntityLite/", StringComparison.Ordinal)))
            {
                var persistent = deserialize.Invoke(data, new object[] { shared.CommonType("EcPersistentConfig"), Resources.Load<TextAsset>(entry.Resource).bytes });
                foreach (var raw in (IEnumerable)Get(persistent, "EcRawEntities"))
                {
                    int container = (int)Get(raw, "ContainerId"), entity = (int)Get(raw, "ClassId");
                    var prefab = container == 0 ? data.GetCommonPrefab(entity) : data.GetContainerPrefab(entity);
                    foreach (var component in (IEnumerable)Get(raw, "EcRawComponents"))
                    {
                        var bytes = (byte[])Get(component, "ComponentInfoBytes");
                        if (bytes == null || bytes.Length == 0) continue;
                        var sourceBytes = bytes;
                        var type = shared.Shared.GetType((string)Get(component, "ComponentType"), true);
                        if (type.Name == "WorldStageComponent") bytes = OriginalStageInput(bytes);
                        var original = prefab.GetType().GetMethod("GetComponent", new[] { typeof(Type) }).Invoke(prefab, new object[] { type });
                        // Info.Init intentionally builds serializable caches such
                        // as AreaInfo.PosSet. Check decoding before that original
                        // initialization, while startup below checks its result.
                        var info = deserialize.Invoke(data, new object[] { Get(original, "_Info").GetType(), sourceBytes });
                        var formatter = resolver.GetType().GetMethod("GetFormatter").MakeGenericMethod(info.GetType()).Invoke(resolver, null);
                        var arguments = new object[] { null, 0, info, resolver };
                        int written = (int)formatter.GetType().GetMethod("Serialize").Invoke(formatter, arguments);
                        if (written != bytes.Length || !bytes.SequenceEqual(((byte[])arguments[0]).Take(written)))
                        {
                            File.WriteAllBytes(Path.Combine(Workspace, "component-source.bytes"), bytes);
                            File.WriteAllBytes(Path.Combine(Workspace, "component-roundtrip.bytes"), ((byte[])arguments[0]).Take(written).ToArray());
                        }
                        Require(written == bytes.Length && bytes.SequenceEqual(((byte[])arguments[0]).Take(written)),
                            "Original component formatter must retain every selected input byte: " + entry.OriginalPath + "/" + type.Name);
                    }
                }
            }
        }

        private static void VerifySharedWorldCancellation(NativeMap14SharedAssemblyCatalog catalog)
        {
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            {
                var parsers = shared.Shared.GetType("Parsers", true).GetField("Instance");
                var previousParsers = parsers.GetValue(null);
                var previousLoader = GConfig.FileLoader.sFileLoader;
                var previousResolver = MessagePackSerializer.DefaultResolver;
                void Restored() => Require(ReferenceEquals(parsers.GetValue(null), previousParsers)
                    && ReferenceEquals(GConfig.FileLoader.sFileLoader, previousLoader)
                    && ReferenceEquals(MessagePackSerializer.DefaultResolver, previousResolver),
                    "Cancelled or failed world restores the original parser, loader and formatter scopes");
                bool rejected = false;
                try { using (var missing = new MainlineSharedWorld(catalog, int.MaxValue, 1)) { } }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Missing map configuration fails construction");
                Restored();
                using (var interrupted = new MainlineSharedWorld(catalog, 10, 10))
                {
                    var load = interrupted.Load();
                    Require(load.MoveNext() && load.Current is GConfig.ConfigRequest request && request.Success
                        && interrupted.Root == null, "A yielded parser request is complete before cancellation");
                    rejected = false;
                    try { interrupted.Load(); } catch (InvalidOperationException) { rejected = true; }
                    Require(rejected, "A world cannot install a second load into its own scope");
                    ((IDisposable)load).Dispose();
                    Require(!interrupted.IsReady && interrupted.Data == null && interrupted.Configuration == null,
                        "Abandoning the load enumerator releases its unfinished world");
                    Restored();
                }
                using (var closed = new MainlineSharedWorld(catalog, 11, 11))
                {
                    var load = closed.Load();
                    Require(load.MoveNext(), "Map load yields before its native root starts");
                    closed.Dispose();
                    rejected = false;
                    try { load.MoveNext(); } catch (ObjectDisposedException) { rejected = true; }
                    Require(rejected, "A closed map load cannot resume using restored global dependencies");
                    Restored();
                }
            }
            Debug.Log("MAINLINE_SHARED_WORLD_LIFETIME_PASS: failed construction, cancelled parser load, duplicate load and closed-map resume preserve borrowed global scopes.");
        }

        private static IEnumerator VerifySharedMaps()
        {
            object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            int cells = 0, placements = 0;
            VerifySharedWorldCancellation(catalog);
            foreach (int map in new[] { 10, 11, 12, 13, 14, 15, 16 })
            {
                var definition = JsonUtility.FromJson<OfficialMapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"Assets/Official/World/Map/{map}/map{map}.json").text);
                using (var world = new MainlineSharedWorld(catalog, map, 1000L + map))
                {
                    var shared = world.Assemblies;
                    var configuration = world.Configuration;
                    bool rejected = false;
                    try { using (var wrong = new NativeMap14SharedData(shared, configuration, map == 10 ? 11 : 10,
                        $"MainlineSharedWorld/{map}/catalog")) { } }
                    catch (ArgumentException) { rejected = true; }
                    Require(rejected, "Original shared catalog rejects a different map identity");
                    var data = world.Data;
                    {
                        Require(!world.IsReady && world.Root == null, "Map root starts only after all original data is loaded");
                        var load = world.Load();
                        while (load.MoveNext()) yield return load.Current;
                        Require(data.Loaded && data.MapClassId == map, "Configured original shared map load");
                        VerifySharedMapComponentBytes(shared, data);
                        Require(world.IsReady, "Common world module completes original startup");
                        var root = world.Root;
                        {
                            Require((int)Get(root.Container, "ClassId") == map && (long)Get(root.Container, "Id") == 1000L + map
                                && (int)Get(root.Root, "ClassId") == map, "Original container and root use the requested map");
                            var nav = definition.Navigation;
                            var originalCells = (Array)Get(data.Navigation, "PosDataArray");
                            var gridMask = root.Ground.GetType().GetMethod("GetGridMask");
                            var gridArea = root.AreaAgent.GetType().GetMethod("GridToArea");
                            for (int y = 0; y < nav.Height; y++)
                            {
                                var row = (Array)originalCells.GetValue(y);
                                for (int x = 0; x < nav.Width; x++)
                                {
                                    var pos = Activator.CreateInstance(shared.CommonType("Pos2d"), x, y);
                                    var expected = nav.Rows[y].Cells[x].RawValues;
                                    Require((int)Get(row.GetValue(x), "Area") == expected[0]
                                        && Convert.ToInt32(Get(row.GetValue(x), "GroundType")) == expected[1]
                                        && (int)gridArea.Invoke(root.AreaAgent, new[] { pos }) == expected[0]
                                        && (int)gridMask.Invoke(root.Ground, new[] { pos }) == 1 << expected[1],
                                        "Original configured navigation and initialized native ground: " + map);
                                    cells++;
                                }
                                if ((y & 15) == 0) yield return null;
                            }
                            foreach (var placement in definition.Entities)
                            {
                                var prefab = data.GetContainerPrefab(placement.Id);
                                Require(prefab != null, "Original configured container prefab exists: " + map + "/" + placement.Id);
                                var fresh = root.Instantiate(prefab);
                                var original = ((IEnumerable)Get(prefab, "ComponentList")).Cast<object>().ToArray();
                                var actual = ((IEnumerable)Get(fresh, "ComponentList")).Cast<object>().ToArray();
                                Require(!ReferenceEquals(prefab, fresh) && original.Length == actual.Length
                                    && original.Zip(actual, (a, b) => a.GetType() == b.GetType()
                                        && !ReferenceEquals(a, b) && ReferenceEquals(Get(a, "_Info"), Get(b, "_Info"))).All(value => value),
                                    "Original full placement component order, fresh state and shared configuration");
                                var common = data.GetCommonPrefab(placement.EntityId);
                                Require(common != null && (int)Get(common, "ClassId") == placement.EntityId,
                                    "Original default spawned entity template is available");
                                placements++;
                            }
                            world.Dispose(); world.Dispose();
                            Require(!world.IsReady && world.Root == null && world.Data == null
                                && world.Configuration == null && world.Assemblies == null
                                && (bool)Get(root.Root, "Ended"), "Common module ends the original root and releases the scope");
                        }
                    }
                }
            }
            Require(placements == 11174, "All seven original maps' placement templates");
            Debug.Log($"MAINLINE_SHARED_MAPS_PASS: seven common-module original roots, {cells} initialized cells, {placements} fresh full placement templates and default common prefab lookups. No server spawning/state or formal host proof.");
        }
    }
}
