using System;
using System.Collections.Generic;
using System.Reflection;
using GConfig;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Populate the original Parsers scope from unchanged local Luban payloads.
    // In particular, movement masks and entity tags still use original parsers.
    internal static class MainlineSharedWorldConfiguration
    {
        [Serializable] private sealed class Catalog { public string Manifest; public Entry[] Entries; }
        [Serializable] private sealed class Entry
        { public string OriginalPath, Resource, Sha256, Parser, Config; }

        internal static void Load(NativeMap14SharedAssemblies shared, ConfigManager manager)
        {
            var asset = Resources.Load<TextAsset>("MainlineSharedWorld/Configuration/catalog");
            if (!asset) throw new InvalidOperationException("Original mainline world tables are not bound.");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog.Manifest != "884_156692" || catalog.Entries == null || catalog.Entries.Length != 3)
                throw new InvalidOperationException("Original mainline world table closure differs.");
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in catalog.Entries)
            {
                var bytes = Resources.Load<TextAsset>(entry.Resource);
                if (!bytes) throw new InvalidOperationException("Original world table is absent: " + entry.OriginalPath);
                NativeMap14SharedAssemblies.Verify(bytes.bytes, entry.Sha256);
                paths.Add(entry.OriginalPath, entry.Resource + ".bytes");
            }
            var previous = manager.PathTransformFunc;
            try
            {
                manager.PathTransformFunc = path => paths.TryGetValue(path, out var local) ? local
                    : throw new InvalidOperationException("Original world table path is unbound: " + path);
                foreach (var entry in catalog.Entries)
                {
                    var type = shared.CommonType(entry.Parser);
                    manager.RegisterTypes(new[] { type });
                    var parser = manager.GetParser(type)
                        ?? throw new InvalidOperationException("Original world parser is not registered: " + entry.Parser);
                    var attribute = shared.CommonType(entry.Config).GetCustomAttribute<ConfigAttribute>()
                        ?? throw new InvalidOperationException("Original world config attribute is absent: " + entry.Config);
                    var request = parser.Parse(attribute.LoadPhases, false)
                        ?? throw new InvalidOperationException("Original world parse phase was rejected: " + entry.Config);
                    manager.Wait(new[] { request });
                    if (!request.Success) throw new InvalidOperationException("Original world table parsing failed: " + entry.Config);
                    parser.PostParse();
                }
            }
            finally { manager.PathTransformFunc = previous; }
        }
    }
}
