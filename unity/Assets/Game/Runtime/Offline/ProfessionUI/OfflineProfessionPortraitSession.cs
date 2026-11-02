using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // One selected local actor owns the original background manager and studio.
    // The owning host ticks Update and detaches its UI before disposing this scope.
    public sealed class OfflineProfessionPortraitSession : IProfessionPortraitManagerBindings, IDisposable
    {
        private readonly OfflineProfessionPortraitStudio studio;
        private bool disposed;
        public CharacterPortraitManager Manager { get; }
        public PlayerPortraitSettings Settings { get; }
        public ICharacterPortraitPersister Persister { get; }
        public GAction LowMemory { get; }
        public IReadOnlyList<ProfessionPortraitSkinHash> AppearanceHashes { get; }
        public IReadOnlyList<ProfessionPortraitSkinName> SkinNames { get; }
        public IProfessionPortraitAppearanceReader AppearanceReader { get; }

        public OfflineProfessionPortraitSession(string profileCacheRoot,
            ProfessionNativePortraitStudioResources resources, OfflineProfessionAppearanceBindings player,
            ProfessionAppearanceTables tables, IAppearanceAssetManager materials,
            ICharacterPartEntryProvider partProvider, IProfessionAppearancePartBindings parts, GAction lowMemory)
        {
            if (string.IsNullOrWhiteSpace(profileCacheRoot)) throw new ArgumentException(nameof(profileCacheRoot));
            if (!resources) throw new ArgumentNullException(nameof(resources));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            LowMemory = lowMemory ?? throw new ArgumentNullException(nameof(lowMemory));
            Settings = resources.Settings;
            AppearanceReader = new OfflineProfessionPortraitAppearanceReader(player);
            AppearanceHashes = resources.AppearanceConfig.AppearanceToHash
                .Select(value => new ProfessionPortraitSkinHash(value.SkinName, value.HashValue)).ToArray();
            SkinNames = tables.SpineSkinInfoParser.GetAll()
                .Select(value => new ProfessionPortraitSkinName(value.Id, value.SkinName)).ToArray();
            string cacheRoot = Path.Combine(Path.GetFullPath(profileCacheRoot), player.Id.ToString(CultureInfo.InvariantCulture));
            Persister = new CharacterPortraitPersister(new OfflineProfessionPortraitState(cacheRoot), cacheRoot);
            studio = new OfflineProfessionPortraitStudio(resources, player, materials, partProvider, parts);
            Manager = new CharacterPortraitManager(this);
            try { Manager.Initialize(); }
            catch
            {
                try { Manager.DisposeOfflineSession(); }
                finally { studio.Dispose(); }
                throw;
            }
        }

        IProfessionPortraitStudio IProfessionPortraitManagerBindings.InstantiateStudio() => studio.InstantiateStudio();
        public void Update()
        {
            if (!disposed) Manager.Update();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Manager.DisposeOfflineSession(); }
            finally { studio.Dispose(); }
        }
    }
}
