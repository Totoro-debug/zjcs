using System;
using System.Collections;
using GConfig;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Owns one map's original parser scope and container. Entity restoration and
    // presentation borrow this scope rather than installing another map root.
    public sealed class MainlineSharedWorld : IDisposable
    {
        public NativeMap14SharedAssemblies Assemblies { get; private set; }
        public NativeMap14SharedConfiguration Configuration { get; private set; }
        public NativeMap14SharedData Data { get; private set; }
        public NativeMap14SharedRoot Root { get; private set; }
        public object LoEntityIds => entities?.LoEntityIds;
        public bool IsLoaded { get; private set; }
        public bool IsReady { get; private set; }

        private readonly long containerId;
        private MainlineSharedEntityCreation entities;
        private bool loading, disposed;

        public MainlineSharedWorld(NativeMap14SharedAssemblyCatalog catalog, int mapClassId, long containerId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (mapClassId <= 0) throw new ArgumentOutOfRangeException(nameof(mapClassId));
            if (containerId <= 0) throw new ArgumentOutOfRangeException(nameof(containerId));
            this.containerId = containerId;
            try
            {
                Assemblies = new NativeMap14SharedAssemblies(catalog);
                Configuration = new NativeMap14SharedConfiguration(Assemblies);
                Data = new NativeMap14SharedData(Assemblies, Configuration, mapClassId,
                    $"MainlineSharedWorld/{mapClassId}/catalog");
            }
            catch { Dispose(); throw; }
        }

        // Without a region provider this retains root-only automatic startup.
        // A fresh snapshot (0 unknown, 1 private, 2 public) finishes loaded but
        // unstarted. Bind the client's player/condition context, then call Start.
        public IEnumerator Load(Func<int, int> initialRegionStatus = null)
        {
            RequireActive();
            if (loading) throw new InvalidOperationException("This original map load has already started.");
            loading = true;
            return LoadCore(initialRegionStatus);
        }

        private IEnumerator LoadCore(Func<int, int> initialRegionStatus)
        {
            bool completed = false;
            try
            {
                RequireActive();
                var load = Data.LoadAll(false);
                while (true)
                {
                    RequireActive();
                    if (!load.MoveNext()) break;
                    // Finish the local parser job before a frame can interrupt
                    // loading and restore its global configuration dependencies.
                    if (load.Current is ConfigRequest request)
                        Configuration.Manager.Wait(new[] { request });
                    yield return load.Current;
                }
                Root = new NativeMap14SharedRoot(Assemblies, Configuration, Data, containerId,
                    "MainlineSharedWorld/container-binding");
                entities = new MainlineSharedEntityCreation(Assemblies, Data, Root);
                if (initialRegionStatus != null)
                {
                    entities.Populate(initialRegionStatus);
                    // Keep cancellation possible after the complete snapshot is
                    // registered, before handing ownership back to the host.
                    yield return null;
                    RequireActive();
                }
                IsLoaded = true;
                if (initialRegionStatus == null) Start();
                completed = true;
            }
            finally
            {
                if (!completed) Dispose();
            }
        }

        public void Start()
        {
            RequireActive();
            if (!IsLoaded) throw new InvalidOperationException("The original map snapshot has not finished loading.");
            if (IsReady) throw new InvalidOperationException("The original map container has already started.");
            try { Root.Start(); IsReady = true; }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsLoaded = false;
            IsReady = false;
            try { Root?.Dispose(); }
            finally
            {
                entities?.Clear();
                entities = null;
                Root = null;
                try { Data?.Dispose(); }
                finally
                {
                    Data = null;
                    try { Configuration?.Dispose(); }
                    finally
                    {
                        Configuration = null;
                        Assemblies?.Dispose();
                        Assemblies = null;
                    }
                }
            }
        }

        private void RequireActive()
        { if (disposed) throw new ObjectDisposedException(nameof(MainlineSharedWorld)); }
    }
}
