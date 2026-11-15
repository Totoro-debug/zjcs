using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeAvatarUI
{
    // The home HUD detaches its original portrait widget before releasing this
    // owner. Body, independent weapons and effect parts use the existing native
    // Windows resource routes; no temporary replacement of another page's route.
    public sealed class OfflineHomeAvatarPortraits : IDisposable
    {
        private readonly GAction lowMemory = new GAction();
        private readonly OfflineProfessionSceneTextureResources bodies;
        private readonly AppearanceAssetManager materials;
        private readonly CharacterPartEntryProvider parts;
        private readonly IDisposable routes;
        private readonly OfflineProfessionPortraitSession portraits;
        private bool disposed;
        private int updatedFrame = -1;
        public OfflineProfessionAppearanceBindings Appearance { get; }
        public CharacterPortraitManager Manager => portraits.Manager;

        public OfflineHomeAvatarPortraits(string cacheRoot, long playerId,
            Func<OfflinePlayerProgress> readPlayer, Func<int,int?> itemType)
        {
            try
            {
                var resources = ProfessionNativePortraitStudioResources.LoadDefault();
                var weapons = Required<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
                var tables = ProfessionAppearanceTables.Load(Required<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                var defaults = new ProfessionAppearanceSkinDefaults(tables,
                    Required<TextAsset>("Combat/SceneAppearance/skin-defaults").text, Debug.LogError);
                Appearance = new OfflineProfessionAppearanceBindings(playerId, readPlayer, itemType, tables,
                    resources.AppearanceConfig, Required<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule"), defaults);
                bodies = new OfflineProfessionSceneTextureResources(resources.Characters);
                materials = new AppearanceAssetManager(weapons.Textures.ManagerConfig, lowMemory.Subscribe);
                var weaponMaterials = new OfflineProfessionWeaponTextureResources(resources.AppearanceConfig, materials, weapons.Textures);
                routes = new OfflineProfessionBattleSceneTextures(resources.Characters, bodies, weapons.Textures, weaponMaterials).Register();
                var effects = new OfflineProfessionAppearancePartBindings(resources.AppearanceConfig, tables.SpineSkinInfoParser,
                    Required<OfflineProfessionImageCatalog>("Combat/PlayerEffects/catalog"));
                parts = new CharacterPartEntryProvider(new OfflineProfessionScenePartPoolBindings(weapons, weaponMaterials, effects, () => true));
                portraits = new OfflineProfessionPortraitSession(cacheRoot, resources, Appearance, tables, materials, parts, effects, lowMemory);
                Application.lowMemory += OnLowMemory;
            }
            catch { Dispose(); throw; }
        }

        private void OnLowMemory() => lowMemory.Invoke();
        public void RefreshAppearance()
        {
            Check(); Appearance.Refresh(AppearanceOperateSource.Normal);
        }
        public void Update()
        {
            Check();
            if (updatedFrame == Time.frameCount) return;
            updatedFrame = Time.frameCount;
            materials.Update(); parts.Update(); portraits.Update();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Application.lowMemory -= OnLowMemory;
            // Original studio/portrait handles release before their part and
            // material caches; registered routes stay valid throughout teardown.
            try { portraits?.Dispose(); }
            finally
            {
                try { parts?.Dispose(); }
                finally
                {
                    try { materials?.Dispose(); }
                    finally { routes?.Dispose(); bodies?.Dispose(); }
                }
            }
        }
        private void Check()
        { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeAvatarPortraits)); }
        private static T Required<T>(string address) where T : UnityEngine.Object
            => Resources.Load<T>(address) ?? throw new InvalidOperationException("Original home portrait resource is unbound: " + address);
    }
}
