using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Local ownership of the original map container and its complete root.
    // World entity state and visibility are supplied by the map session later.
    public sealed class NativeMap14SharedRoot : IDisposable
    {
        [Serializable] private sealed class Binding
        {
            public string Resource;
            public string Sha256;
        }

        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedEntityUtilities utilities;
        private readonly MethodInfo instantiate;
        private bool disposed;

        public object Container { get; }
        public object Root { get; }
        public object Ground { get; }
        public object AreaAgent { get; }

        public NativeMap14SharedRoot(NativeMap14SharedAssemblies shared, NativeMap14SharedConfiguration configuration,
            NativeMap14SharedData data, long containerId)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (data == null || !data.Loaded) throw new ArgumentException("Load the original map data first.", nameof(data));
            if (containerId <= 0) throw new ArgumentOutOfRangeException(nameof(containerId));
            var catalog = Resources.Load<TextAsset>("Map14/SharedRoot/container-binding");
            if (!catalog) throw new InvalidOperationException("Original container table binding is absent.");
            var binding = JsonUtility.FromJson<Binding>(catalog.text);
            var asset = Resources.Load<TextAsset>(binding.Resource);
            if (!asset) throw new InvalidOperationException("Original container table is absent.");
            NativeMap14SharedAssemblies.Verify(asset.bytes, binding.Sha256);
            var tableType = shared.CommonType("TbContainerInfo");
            var constructor = tableType.GetConstructors()[0];
            object buffer = Activator.CreateInstance(constructor.GetParameters()[0].ParameterType, new object[] { asset.bytes });
            object table = constructor.Invoke(new[] { buffer });
            if ((int)buffer.GetType().GetProperty("Remaining").GetValue(buffer) != 0)
                throw new InvalidOperationException("Original container table was not fully consumed.");
            object info = ((IDictionary)tableType.GetProperty("DataMap").GetValue(table))[14];
            if (info == null) throw new InvalidOperationException("Original map container definition is absent.");

            utilities = new NativeMap14SharedEntityUtilities(shared);
            try
            {
                instantiate = shared.CommonType("EntityUtils").GetMethod("InstantiateEntity");
                Container = Activator.CreateInstance(shared.CommonType("EcContainer"));
                Set(Container, "Id", containerId);
                Set(Container, "ClassId", 14);
                Set(Container, "Info", info);
                int rootClassId = (int)info.GetType().GetField("RootEntityClassId").GetValue(info);
                Root = Instantiate(data.GetCommonPrefab(rootClassId));
                Set(Root, "Id", Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), containerId, configuration.RootEntityId));
                if (!(bool)Container.GetType().GetMethod("AddEntity").Invoke(Container, new[] { Root }))
                    throw new InvalidOperationException("Original root entity registration failed.");
                Ground = Component(Root, shared.CommonType("GridGroundComponent"));
                AreaAgent = Component(Root, shared.CommonType("AreaAgentComponent"));
                if (Ground == null || AreaAgent == null) throw new InvalidOperationException("Original map root grid is incomplete.");
            }
            catch { Dispose(); throw; }
        }

        public object Instantiate(object prefab)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedRoot));
            if (prefab == null || !shared.CommonType("EcEntity").IsInstanceOfType(prefab))
                throw new ArgumentException("An original shared entity prefab is required.", nameof(prefab));
            return instantiate.Invoke(null, new[] { prefab });
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14SharedRoot));
            Container.GetType().GetMethod("Start").Invoke(Container, null);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Container?.GetType().GetMethod("RemoveAllEntities").Invoke(Container, null); }
            finally { utilities?.Dispose(); }
        }

        private static object Component(object entity, Type type) =>
            entity.GetType().GetMethod("GetComponent", new[] { typeof(Type) }).Invoke(entity, new object[] { type });

        private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    }
}
