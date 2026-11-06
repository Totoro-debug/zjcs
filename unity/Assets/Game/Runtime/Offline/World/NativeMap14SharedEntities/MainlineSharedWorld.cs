using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GConfig;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // State already resolved by the owning local map, keyed by original placement.
    public readonly struct MainlineSharedEntityState
    {
        public readonly int EntityClassId, State, X, Y;
        public MainlineSharedEntityState(int entityClassId, int state, int x, int y)
        { EntityClassId = entityClassId; State = state; X = x; Y = y; }
    }

    // Owns one map's original parser scope and container. Entity restoration and
    // presentation borrow this scope rather than installing another map root.
    public sealed partial class MainlineSharedWorld : IDisposable
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
        private MainlineSharedMapConfiguration clientConfiguration;
        private MainlineSharedClientContext client;
        private MainlineSharedClientServices clientServices;
        private Dictionary<int, int> clientRegions;
        private long publicContainerId;
        private bool clientSupportsPrivate;
        private NativeMap14SharedLogging logging;
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
                logging = new NativeMap14SharedLogging(Assemblies);
                Configuration = new NativeMap14SharedConfiguration(Assemblies);
                MainlineSharedWorldConfiguration.Load(Assemblies, Configuration.Manager);
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

        // Prepare the full original client configuration and its metadata
        // dependencies. The host binds its original player/map before Start.
        public IEnumerator LoadClient(Func<int, int> initialRegionStatus)
        {
            RequireActive();
            if (initialRegionStatus == null) throw new ArgumentNullException(nameof(initialRegionStatus));
            if (loading) throw new InvalidOperationException("This original map load has already started.");
            try
            {
                clientConfiguration = new MainlineSharedMapConfiguration(Assemblies, Configuration.Manager, Data);
                return Load(initialRegionStatus);
            }
            catch { Dispose(); throw; }
        }

        // A mixed map keeps distinct private/public entity identifiers inside
        // the original merged display container, as consumed by MsgMapStart.
        public IEnumerator LoadClient(Func<int, int> initialRegionStatus, long publicContainerId)
        {
            RequireActive();
            if (publicContainerId <= 0 || publicContainerId == containerId)
                throw new ArgumentOutOfRangeException(nameof(publicContainerId));
            if (initialRegionStatus == null) throw new ArgumentNullException(nameof(initialRegionStatus));
            if (loading) throw new InvalidOperationException("This original map load has already started.");
            try
            {
                clientServices = new MainlineSharedClientServices(Assemblies);
                var load = LoadClient(initialRegionStatus);
                this.publicContainerId = publicContainerId;
                return load;
            }
            catch { Dispose(); throw; }
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
                clientConfiguration?.Complete(Data);
                if (publicContainerId != 0)
                {
                    var metadataType = Assemblies.CommonType("MapInfoManager");
                    var info = metadataType.GetMethod("GetMapPartInfo").Invoke(
                        metadataType.GetProperty("Instance").GetValue(null), new object[] { Data.MapClassId });
                    clientSupportsPrivate = (bool)NativeMap14SharedPrefabs.Get(info, "SupportPrivateRegion");
                    clientRegions = new Dictionary<int, int>();
                    foreach (var region in ((IDictionary)NativeMap14SharedPrefabs.Get(Data.Placement, "MaxRegionProgressDict")).Keys)
                    {
                        int id = (int)region, status = initialRegionStatus(id);
                        if (status < 0 || status > 2) throw new ArgumentOutOfRangeException(nameof(initialRegionStatus));
                        clientRegions.Add(id, status);
                    }
                    initialRegionStatus = region => clientRegions[region];
                }
                Root = new NativeMap14SharedRoot(Assemblies, Configuration, Data, containerId,
                    "MainlineSharedWorld/container-binding");
                entities = new MainlineSharedEntityCreation(Assemblies, Data, Root, clientConfiguration);
                if (initialRegionStatus != null)
                {
                    entities.Populate(initialRegionStatus, publicContainerId == 0 ? null : (Func<int, long>)ClientContainerId);
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

        public void SynchronizeEntityStates(IReadOnlyDictionary<int, MainlineSharedEntityState> states)
        {
            RequireActive();
            if (!IsLoaded) throw new InvalidOperationException("Load the original entities before restoring their local state.");
            entities.Synchronize(states, IsReady);
            if (client != null) entities.SynchronizeActivations(client.Player);
        }

        // One committed local snapshot drives stage/grid changes and the original
        // region remove/add/status sequence. Raw progress must already be published.
        public void SynchronizeClientState(IReadOnlyDictionary<int, MainlineSharedEntityState> states,
            Func<int, int> regionStatus)
        {
            RequireActive();
            if (!IsReady || client == null || clientRegions == null)
                throw new InvalidOperationException("Start the original client before synchronizing its live regions.");
            if (states == null || regionStatus == null) throw new ArgumentNullException(nameof(states));
            var next = clientRegions.Keys.ToDictionary(region => region, regionStatus);
            if (next.Any(pair => pair.Value < clientRegions[pair.Key] || pair.Value > 2))
                throw new InvalidOperationException("A live original region snapshot cannot revert its saved status.");
            SynchronizeEntityStates(states);
            foreach (var pair in next.OrderBy(pair => pair.Key))
            {
                if (pair.Value == clientRegions[pair.Key]) continue;
                entities.TransitionRegion(pair.Key, pair.Value, ClientContainerId(pair.Value), states, client.Player);
                clientRegions[pair.Key] = pair.Value;
            }
            if (ClientCharacter != null)
            {
                var position = NativeMap14SharedPrefabs.Get(CharacterComponent(ClientCharacter, "GridTransformComponent"), "Pos");
                SynchronizeClientCharacter((int)NativeMap14SharedPrefabs.Get(position, "X"), (int)NativeMap14SharedPrefabs.Get(position, "Y"));
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

        public void StartClient(object player)
            => StartClientCore(player, null);

        public void StartClient(object player, int x, int y)
            => StartClientCore(player, new[] { x, y });

        private void StartClientCore(object player, int[] position)
        {
            RequireActive();
            if (!IsLoaded || IsReady || clientRegions == null)
                throw new InvalidOperationException("Load the client map with distinct private/public identifiers before client startup.");
            if (player == null || !Assemblies.CommonType("Player").IsInstanceOfType(player))
                throw new ArgumentException("An original local player is required.", nameof(player));
            try
            {
                entities.SynchronizeActivations(player);
                client = new MainlineSharedClientContext(this, clientServices, player, clientRegions, ClientContainerId);
                if (position != null) CreateClientCharacter(position[0], position[1]);
                Start();
            }
            catch { Dispose(); throw; }
        }

        private long ClientContainerId(int status) =>
            status == 2 && clientSupportsPrivate ? publicContainerId : containerId;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsLoaded = false;
            IsReady = false;
            try { Root?.Dispose(); }
            finally
            {
                ClientCharacter = null;
                try
                {
                    try { client?.Dispose(); }
                    finally
                    {
                        client = null;
                        clientRegions = null;
                        try { entities?.Clear(); }
                        finally
                        {
                            entities = null;
                            Root = null;
                            try { clientServices?.Dispose(); }
                            finally
                            {
                                clientServices = null;
                                try { clientConfiguration?.Dispose(); }
                                finally { clientConfiguration = null; Data?.Dispose(); }
                            }
                        }
                    }
                }
                finally
                {
                    Data = null;
                    try { Configuration?.Dispose(); }
                    finally
                    {
                        Configuration = null;
                        try { logging?.Dispose(); }
                        finally
                        {
                            logging = null;
                            Assemblies?.Dispose();
                            Assemblies = null;
                        }
                    }
                }
            }
        }

        private void RequireActive()
        { if (disposed) throw new ObjectDisposedException(nameof(MainlineSharedWorld)); }
    }
}
