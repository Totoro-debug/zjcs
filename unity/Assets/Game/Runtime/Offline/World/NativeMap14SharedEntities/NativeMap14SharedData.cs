using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GConfig;
using MessagePack;
using MessagePack.Formatters;
using UnityEngine;
using Zjcs.Official.SharedSerialization;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Binds original map records and parser calls. It supplies no server state.
    public sealed class NativeMap14SharedData : IDisposable
    {
        [Serializable] private sealed class Catalog
        {
            public Entry[] Entries;
            public string LookupResource;
            public string LookupSha256;
        }
        [Serializable] private sealed class Entry
        {
            public string OriginalPath;
            public string Resource;
            public string Sha256;
        }

        private sealed class LocalLoader : IFileLoader
        {
            private readonly Dictionary<string, Entry> entries;
            private readonly DefaultFileLoader original = new DefaultFileLoader();
            public LocalLoader(Dictionary<string, Entry> entries) { this.entries = entries; }
            public void Initialize(ConfigManager manager) { original.Initialize(manager); }
            public bool Exists(string file) => entries.ContainsKey(file);
            public void LoadFile(string file, bool isBinary, bool loadAsync, ConfigRequestBase request)
            {
                if (!entries.TryGetValue(file, out var entry))
                    throw new InvalidOperationException("Original shared map path is not bound: " + file);
                LoadAsset(entry);
                original.LoadFile(entry.Resource + ".bytes", isBinary, loadAsync, request);
            }
        }

        private readonly NativeMap14SharedAssemblies shared;
        private readonly ConfigManager manager;
        private readonly Catalog catalog;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly NativeSharedFormatterResolver resolver;
        private readonly IFormatterResolver previousResolver;
        private readonly IFileLoader previousFactory;
        private readonly IFileLoader previousManagerLoader;
        private readonly Func<string, string> previousTransform;
        private readonly object reflectionManager;
        private readonly object previousReflector;
        private readonly object reflector;
        private readonly Dictionary<string, Type> reflectedTypes;
        private readonly LocalLoader loader;
        private bool disposed;

        public object DataCenter { get; private set; }
        public object Navigation { get; private set; }
        public object Placement { get; private set; }
        public bool Loaded { get; private set; }

        public NativeMap14SharedData(NativeMap14SharedAssemblies shared, NativeMap14SharedConfiguration configuration)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            manager = configuration?.Manager ?? throw new ArgumentNullException(nameof(configuration));
            var asset = Resources.Load<TextAsset>("Map14/SharedSerialization/catalog");
            if (!asset) throw new InvalidOperationException("Original shared map catalog is absent.");
            catalog = JsonUtility.FromJson<Catalog>(asset.text);
            foreach (var entry in catalog.Entries) entries.Add(entry.OriginalPath, entry);
            var lookup = Resources.Load<TextAsset>(catalog.LookupResource);
            if (!lookup) throw new InvalidOperationException("Original generated formatter lookup is absent.");
            NativeMap14SharedAssemblies.Verify(lookup.bytes, catalog.LookupSha256);
            resolver = new NativeSharedFormatterResolver(shared.Shared, lookup.text);
            previousResolver = MessagePackSerializer.DefaultResolver;
            previousFactory = GConfig.FileLoader.sFileLoader;
            previousManagerLoader = manager.FileLoader;
            previousTransform = manager.PathTransformFunc;
            var reflectionType = shared.CommonType("ReflectionClassManager");
            reflectionManager = reflectionType.GetProperty("Instance").GetValue(null);
            previousReflector = NewReflector();
            // Capture registry contents for scope restoration through original Init.
            CopyRegistry(reflectionType, "ReflectionClassDict", "AllClasses", previousReflector);
            CopyRegistry(reflectionType, "SameSubclass", "SameSubclasses", previousReflector);
            reflector = NewReflector();
            reflectedTypes = (Dictionary<string, Type>)reflector.GetType().GetProperty("AllClasses").GetValue(reflector);
            loader = new LocalLoader(entries);
            try
            {
                reflectionType.GetMethod("Init").Invoke(reflectionManager, new[] { reflector });
                MessagePackSerializer.SetDefaultResolver(resolver);
                GConfig.FileLoader.SetupFileLoader(loader);
                manager.Initialize("", false, 0);
                manager.PathTransformFunc = null;
                var parsers = new[] { "EcDataCenterParser", "EcMessagePackParser", "MapRootEntityLiteInfoParser", "MapInfoWrapMessagePackParser" };
                foreach (var name in parsers)
                {
                    Type type = shared.CommonType(name);
                    manager.RegisterTypes(new[] { type });
                    if (manager.GetParser(type) == null) throw new InvalidOperationException("Original map parser registration failed: " + name);
                }
                DataCenter = manager.GetParser(shared.CommonType("EcDataCenterParser"));
            }
            catch { Dispose(); throw; }
        }

        public IEnumerator LoadAll(bool loadAsync = true)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedData));
            if (Loaded) yield break;
            foreach (var entry in catalog.Entries)
            {
                string parserName;
                string file = entry.OriginalPath.Substring(entry.OriginalPath.LastIndexOf('/') + 1);
                if (entry.OriginalPath.StartsWith("Config/EC/MapRootEntityLite/", StringComparison.Ordinal))
                    parserName = "MapRootEntityLiteInfoParser";
                else if (entry.OriginalPath.StartsWith("Config/MapInfoWrap/", StringComparison.Ordinal))
                    parserName = "MapInfoWrapMessagePackParser";
                else
                {
                    parserName = "EcMessagePackParser";
                    RegisterSourceTypes(Deserialize(shared.CommonType("EcPersistentConfig"), LoadAsset(entry).bytes));
                }
                var parser = (ISupportParseSingle)manager.GetParser(shared.CommonType(parserName));
                ConfigRequest request = parser.ParseSingle(file, loadAsync);
                yield return request;
                if (!request.Success) throw new InvalidOperationException("Original shared map parser failed: " + entry.OriginalPath);
                if (parserName == "MapRootEntityLiteInfoParser") Navigation = request.Result;
                if (parserName == "MapInfoWrapMessagePackParser") Placement = request.Result;
            }
            Loaded = true;
        }

        public object GetCommonPrefab(int classId) => DataCenter.GetType().GetMethod("GetCommonPrefab").Invoke(DataCenter, new object[] { classId });
        public object GetContainerPrefab(int entityId) => DataCenter.GetType().GetMethod("GetContainerPrefab").Invoke(DataCenter, new object[] { 14, entityId });

        private object Deserialize(Type type, byte[] bytes)
        {
            var formatter = typeof(NativeSharedFormatterResolver).GetMethod("GetFormatter").MakeGenericMethod(type).Invoke(resolver, null);
            var arguments = new object[] { bytes, 0, resolver, 0 };
            var result = typeof(IMessagePackFormatter<>).MakeGenericType(type).GetMethod("Deserialize").Invoke(formatter, arguments);
            if ((int)arguments[3] != bytes.Length) throw new InvalidOperationException("Original shared record was not fully consumed: " + type.FullName);
            return result;
        }

        private void RegisterSourceTypes(object persistent)
        {
            foreach (var entity in (IEnumerable)persistent.GetType().GetField("EcRawEntities").GetValue(persistent))
                foreach (var component in (IEnumerable)entity.GetType().GetField("EcRawComponents").GetValue(entity))
                {
                    string name = (string)component.GetType().GetField("ComponentType").GetValue(component);
                    reflectedTypes[name] = shared.Shared.GetType(name, true);
                    Type info = shared.Shared.GetType(name + "Info", false);
                    if (info != null) reflectedTypes[info.FullName] = info;
                }
        }

        private object NewReflector() => Activator.CreateInstance(shared.CommonType("DefaultClassReflector"), new object[] { new HashSet<string>(), new Type[0][] });

        private void CopyRegistry(Type type, string field, string property, object target)
        {
            var source = (IDictionary)type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reflectionManager);
            var destination = (IDictionary)target.GetType().GetProperty(property).GetValue(target);
            foreach (DictionaryEntry entry in source) destination.Add(entry.Key, entry.Value);
        }

        private static TextAsset LoadAsset(Entry entry)
        {
            var asset = Resources.Load<TextAsset>(entry.Resource);
            if (!asset) throw new InvalidOperationException("Original shared map payload is absent: " + entry.OriginalPath);
            NativeMap14SharedAssemblies.Verify(asset.bytes, entry.Sha256);
            return asset;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(MessagePackSerializer.DefaultResolver, resolver)) MessagePackSerializer.SetDefaultResolver(previousResolver);
            var reflectionType = shared.CommonType("ReflectionClassManager");
            if (ReferenceEquals(reflectionType.GetField("ReflectionClassDict", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reflectionManager), reflectedTypes))
                reflectionType.GetMethod("Init").Invoke(reflectionManager, new[] { previousReflector });
            if (manager)
            {
                var currentFactory = GConfig.FileLoader.sFileLoader;
                try
                {
                    GConfig.FileLoader.SetupFileLoader(previousManagerLoader);
                    manager.Initialize("", false, 0);
                    manager.PathTransformFunc = previousTransform;
                }
                finally { GConfig.FileLoader.SetupFileLoader(ReferenceEquals(currentFactory, loader) ? previousFactory : currentFactory); }
            }
            DataCenter = null;
            Navigation = null;
            Placement = null;
        }
    }
}
