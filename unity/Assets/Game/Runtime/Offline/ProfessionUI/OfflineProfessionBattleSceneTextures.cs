using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // One original resource-routing endpoint for simultaneously visible body
    // Low and independent weapon High loaders. No global binding is switched
    // while another renderer is requesting textures.
    public sealed class OfflineProfessionBattleSceneTextures : IProfessionAppearanceTextureBindings
    {
        private readonly Dictionary<string, IProfessionAppearanceTextureBindings> byGuid = new Dictionary<string, IProfessionAppearanceTextureBindings>(StringComparer.Ordinal);
        private readonly Dictionary<ScriptableObject, IProfessionAppearanceTextureBindings> byAtlas = new Dictionary<ScriptableObject, IProfessionAppearanceTextureBindings>();
        public OfflineProfessionBattleSceneTextures(ProfessionNativeSceneAppearanceResources characters,
            IProfessionAppearanceTextureBindings body, ProfessionNativeWeaponTextureResources weapons,
            IProfessionAppearanceTextureBindings weapon)
        {
            if (!characters || !weapons || body == null || weapon == null) throw new ArgumentNullException();
            foreach (var entry in characters.TextureAddresses) byGuid.Add(entry.Guid, body);
            foreach (var guid in weapons.TextureGuids) byGuid.Add(guid, weapon);
            foreach (var model in characters.Models) byAtlas.Add(model.Atlas, body);
            byAtlas.Add(weapons.Atlas, weapon);
        }
        public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (!byGuid.TryGetValue(guid, out var binding)) throw new NotSupportedException("Original battle scene texture GUID is unbound: " + guid);
            return binding.LoadAssetSyncByGuid<T>(guid);
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            if (!atlas || !byAtlas.TryGetValue(atlas, out var binding)) throw new NotSupportedException("Original battle scene atlas identity is unbound.");
            return binding.GetAtlasMaterial(atlas, materialIndex);
        }
        public IDisposable Register() => ProfessionAppearanceTextureBindings.Register(this, byGuid.Keys, byAtlas.Keys);
    }
}
