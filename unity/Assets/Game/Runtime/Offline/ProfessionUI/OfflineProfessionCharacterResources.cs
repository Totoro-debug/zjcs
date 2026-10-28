using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class OfflineProfessionCharacterResources : IProfessionAppearanceCharacterBindings
    {
        private sealed class Handle : IProfessionAppearanceCharacterAsset
        {
            private ScriptableObject asset;
            internal Handle(ScriptableObject asset) { this.asset = asset; }
            public ScriptableObject Result => asset ? asset : throw new ObjectDisposedException(nameof(Handle));
            // The explicit catalog owns the packaged asset. Releasing a request
            // drops its reference without unloading another appearance's asset.
            public void Dispose() { asset = null; }
        }
        private readonly ProfessionNativeCharacterCatalog catalog;
        private readonly Action<Component, Dictionary<SkinType, string>> setParts;
        private readonly Action<Component, bool> releaseParts;
        public AppearanceConfig Config { get; }
        public OfflineProfessionCharacterResources(AppearanceConfig config, ProfessionNativeCharacterCatalog catalog,
            Action<Component, Dictionary<SkinType, string>> setParts, Action<Component, bool> releaseParts)
        {
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            this.catalog = catalog ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.setParts = setParts ?? throw new ArgumentNullException(nameof(setParts));
            this.releaseParts = releaseParts ?? throw new ArgumentNullException(nameof(releaseParts));
        }
        public IProfessionAppearanceCharacterAsset LoadAsset(string path) => new Handle(catalog.LoadPath(path));
        public void SetPartSkins(Component handler, Dictionary<SkinType, string> skins) => setParts(handler, skins);
        public void ReleaseParts(Component handler, bool all) => releaseParts(handler, all);
    }
}
