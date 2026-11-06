using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GConfig;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // The client map's configuration closure. Global metadata queries keep the
    // original implementations; this scope owns only their local input bindings.
    internal sealed class MainlineSharedMapConfiguration : IDisposable
    {
        [Serializable] private sealed class Catalog
        {
            public string Manifest, AssemblyResource, AssemblySha256, EntityCatalog;
            public Entry[] Entries;
            public PlacementMap[] PlacementMaps;
        }
        [Serializable] private sealed class Entry
        { public string Config, Resource, OriginalPath, Sha256; }
        [Serializable] private sealed class PlacementMap
        { public int MapClassId; public string CatalogResource, CatalogSha256; }

        private readonly NativeMap14SharedAssemblies shared;
        private readonly ConfigManager manager;
        private readonly List<MetadataScope> metadata = new List<MetadataScope>();
        private readonly PlacementMap[] placementMaps;
        internal Assembly Readers { get; private set; }
        private bool completed, disposed;

        internal void Dispatch(string name, object value) => Readers.GetType("Zjcs.Mainline.SelectedConfiguration." + name, true)
            .GetMethod("OnMessage", BindingFlags.Public | BindingFlags.Static,
                null, new[] { value.GetType() }, null).Invoke(null, new[] { value });

        public MainlineSharedMapConfiguration(NativeMap14SharedAssemblies shared, ConfigManager manager,
            NativeMap14SharedData data)
        {
            this.shared = shared;
            this.manager = manager;
            var asset = Resources.Load<TextAsset>("MainlineSharedWorld/MapConfiguration/catalog");
            if (!asset) throw new InvalidOperationException("Original client map configuration is absent.");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog.Manifest != "884_156692" || catalog.Entries == null || catalog.Entries.Length != 7)
                throw new InvalidOperationException("Original client map configuration closure differs.");
            placementMaps = catalog.PlacementMaps;
            if (placementMaps == null || placementMaps.Length == 0
                || placementMaps.Select(map => map.MapClassId).Distinct().Count() != placementMaps.Length
                || !placementMaps.Any(map => map.MapClassId == data.MapClassId))
                throw new InvalidOperationException("Original client placement catalog list differs.");
            var assembly = Resources.Load<TextAsset>(catalog.AssemblyResource);
            if (!assembly) throw new InvalidOperationException("Selected original map readers are absent.");
            NativeMap14SharedAssemblies.Verify(assembly.bytes, catalog.AssemblySha256);
            var readers = Assembly.Load(assembly.bytes);
            Readers = readers;
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in catalog.Entries)
            {
                var payload = Resources.Load<TextAsset>(entry.Resource);
                if (!payload) throw new InvalidOperationException("Original client map table is absent: " + entry.Config);
                NativeMap14SharedAssemblies.Verify(payload.bytes, entry.Sha256);
                paths.Add(entry.OriginalPath, entry.Resource + ".bytes");
            }

            // Native map data has its own EC-only loader. Temporarily use the
            // original Resources loader for Luban, then restore that exact scope.
            var previousFactory = GConfig.FileLoader.sFileLoader;
            var previousLoader = manager.FileLoader;
            var previousTransform = manager.PathTransformFunc;
            try
            {
                GConfig.FileLoader.SetupFileLoader(new DefaultFileLoader());
                manager.Initialize("", false, 0);
                MainlineSharedClientConfiguration.Load(shared, manager);
                manager.PathTransformFunc = path => paths.TryGetValue(path, out var local) ? local
                    : throw new InvalidOperationException("Original client map path is unbound: " + path);
                var types = catalog.Entries.Select(entry => shared.CommonType(entry.Config + "Parser")).ToArray();
                if (types.Any(type => manager.GetParser(type) != null))
                    throw new InvalidOperationException("Original client map tables have already been registered.");
                var registry = (IDictionary)typeof(ConfigManager).GetField("parsers",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                foreach (var config in new[] { "MapWorldInfo", "EntityExtendParameterInfo" })
                {
                    var parser = (ParserBase)Activator.CreateInstance(readers.GetType(
                        "Zjcs.Mainline.SelectedConfiguration.Selected" + config + "Parser", true));
                    parser.Initialize(manager);
                    registry.Add(shared.CommonType(config + "Parser"), parser);
                }
                manager.RegisterTypes(types.Where(type => type != shared.CommonType("MapWorldInfoParser")
                    && type != shared.CommonType("EntityExtendParameterInfoParser")).ToArray());
                foreach (var entry in catalog.Entries)
                {
                    var parser = manager.GetParser(shared.CommonType(entry.Config + "Parser"));
                    var attribute = shared.CommonType(entry.Config).GetCustomAttribute<ConfigAttribute>();
                    var request = parser.Parse(attribute.LoadPhases, false);
                    if (request == null) throw new InvalidOperationException("Original map table phase was rejected.");
                    manager.Wait(new[] { request });
                    if (!request.Success) throw new InvalidOperationException("Original client map table failed: " + entry.Config);
                }
                foreach (var type in types)
                    if (type != shared.CommonType("EntityExtendParameterInfoParser")) manager.GetParser(type).PostParse();
            }
            finally
            {
                GConfig.FileLoader.SetupFileLoader(previousLoader);
                try { manager.Initialize("", false, 0); manager.PathTransformFunc = previousTransform; }
                finally { GConfig.FileLoader.SetupFileLoader(previousFactory); }
            }
            data.Include(catalog.EntityCatalog);
        }

        public void Complete(NativeMap14SharedData data)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineSharedMapConfiguration));
            if (completed) throw new InvalidOperationException("Original map metadata is already initialized.");
            if (!data.Loaded) throw new InvalidOperationException("Original map templates are not loaded.");
            manager.GetParser(shared.CommonType("EntityExtendParameterInfoParser")).PostParse();
            var type = shared.CommonType("MapInfoWrapParser");
            manager.RegisterTypes(new[] { type });
            var parser = manager.GetParser(type);
            // Use the already verified original deserialization, including its
            // initialized component info. No duplicate placement model is built.
            var placements = (IDictionary)type.GetField("mMapInfoWrapDic", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(parser);
            foreach (var map in placementMaps)
                placements.Add(map.MapClassId, data.ReadPlacement(map.MapClassId, map.CatalogResource, map.CatalogSha256));
            try
            {
                metadata.Add(new MetadataScope(shared.CommonType("MapInfoManager")));
                metadata.Add(new MetadataScope(shared.CommonType("MapRegionEntityInfoManager")));
                completed = true;
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int i = metadata.Count - 1; i >= 0; i--) metadata[i].Dispose();
            metadata.Clear();
        }

        private sealed class MetadataScope : IDisposable
        {
            private readonly object target;
            private readonly FieldInfo[] fields;
            private readonly object[] previous, owned;
            public MetadataScope(Type type)
            {
                target = type.GetProperty("Instance").GetValue(null);
                var instance = Activator.CreateInstance(type);
                type.GetMethod("Init").Invoke(instance, new object[] { null });
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                previous = fields.Select(field => field.GetValue(target)).ToArray();
                owned = fields.Select(field => field.GetValue(instance)).ToArray();
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(target, owned[i]);
            }
            public void Dispose()
            {
                for (int i = 0; i < fields.Length; i++)
                    if (ReferenceEquals(fields[i].GetValue(target), owned[i])) fields[i].SetValue(target, previous[i]);
            }
        }
    }
}
