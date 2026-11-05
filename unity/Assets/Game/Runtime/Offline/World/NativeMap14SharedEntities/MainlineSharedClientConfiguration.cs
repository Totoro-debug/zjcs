using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GConfig;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Bind original player parsers in the same configuration scope as the map.
    // The only binary adaptation replaces YooAsset path discovery with a pinned
    // local file; the original reader, cache, defaults and formulas are retained.
    public static class MainlineSharedClientConfiguration
    {
        [Serializable] private sealed class Catalog
        { public string Manifest, BinaryFile, BinarySha256; public Entry[] Entries; }
        [Serializable] private sealed class Entry
        { public string Config, Resource, OriginalPath, Sha256; }

        public static void Load(NativeMap14SharedAssemblies shared, ConfigManager manager)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            if (!manager) throw new ArgumentNullException(nameof(manager));
            var asset = Resources.Load<TextAsset>("MainlineSharedWorld/ClientConfiguration/catalog");
            if (!asset) throw new InvalidOperationException("Original player configuration is absent.");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog.Manifest != "884_156692" || catalog.Entries == null || catalog.Entries.Length != 13)
                throw new InvalidOperationException("Original player configuration closure differs.");
            var types = catalog.Entries.Select(entry => shared.CommonType(entry.Config + "Parser")).ToArray();
            var binaryType = shared.CommonType("LevelPropParser");
            if (types.Any(type => manager.GetParser(type) != null) || manager.GetParser(binaryType) != null)
                throw new InvalidOperationException("Original player configuration has already been registered.");
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in catalog.Entries)
            {
                var payload = Resources.Load<TextAsset>(entry.Resource);
                if (!payload) throw new InvalidOperationException("Original player table is absent: " + entry.Config);
                NativeMap14SharedAssemblies.Verify(payload.bytes, entry.Sha256);
                paths.Add(entry.OriginalPath, entry.Resource + ".bytes");
            }
            var binaryPath = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, catalog.BinaryFile));
            var localRoot = Path.GetFullPath(Application.streamingAssetsPath).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!binaryPath.StartsWith(localRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Original property file is outside StreamingAssets.");
            NativeMap14SharedAssemblies.Verify(File.ReadAllBytes(binaryPath), catalog.BinarySha256);
            var previous = manager.PathTransformFunc;
            try
            {
                manager.PathTransformFunc = path => paths.TryGetValue(path, out var local) ? local : previous(path);
                manager.RegisterTypes(types);
                for (int i = 0; i < types.Length; i++)
                {
                    var parser = manager.GetParser(types[i])
                        ?? throw new InvalidOperationException("Original player parser registration failed: " + types[i]);
                    var attribute = shared.CommonType(catalog.Entries[i].Config).GetCustomAttribute<ConfigAttribute>()
                        ?? throw new InvalidOperationException("Original player table phases are absent.");
                    var request = parser.Parse(attribute.LoadPhases, false)
                        ?? throw new InvalidOperationException("Original player table phase was rejected.");
                    manager.Wait(new[] { request });
                    if (!request.Success) throw new InvalidOperationException("Original player table failed: " + types[i]);
                }
                // Profession PostParse resolves skills, so all original tables
                // must finish parsing before any PostParse hook is invoked.
                foreach (var type in types) manager.GetParser(type).PostParse();
                RegisterProperties(shared, manager, binaryType, binaryPath);
            }
            finally { manager.PathTransformFunc = previous; }
        }

        private static void RegisterProperties(NativeMap14SharedAssemblies shared, ConfigManager manager,
            Type type, string path)
        {
            var parser = (IParser)Activator.CreateInstance(type);
            try
            {
                // Invoke the unchanged ParserBase.Initialize nonvirtually, then
                // retain LevelPropParser.Initialize's reader/cache/register order.
                var initialize = new DynamicMethod("OriginalLevelPropBaseInitialize", typeof(void),
                    new[] { typeof(ParserBase), typeof(ConfigManager) }, typeof(ParserBase), true);
                var il = initialize.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, typeof(ParserBase).GetMethod("Initialize"));
                il.Emit(OpCodes.Ret);
                var bind = (Action<ParserBase, ConfigManager>)initialize.CreateDelegate(typeof(Action<ParserBase, ConfigManager>));
                bind((ParserBase)parser, manager);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("mBinaryReader", fields).SetValue(parser,
                    Activator.CreateInstance(shared.CommonType("LevelPropBinaryReader"), new object[] { path }));
                var cache = type.GetField("mCache", fields);
                cache.SetValue(parser, Activator.CreateInstance(cache.FieldType, new object[] { 1000 }));
                // The recovered registry has no instance-registration overload.
                var registry = (IDictionary)typeof(ConfigManager).GetField("parsers", fields).GetValue(manager);
                registry.Add(type, parser);
            }
            catch { ((IDisposable)parser).Dispose(); throw; }
        }
    }
}
