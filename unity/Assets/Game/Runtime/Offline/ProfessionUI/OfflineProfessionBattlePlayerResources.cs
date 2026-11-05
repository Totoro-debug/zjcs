using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // One battle owns these services; Unity owns actor animation and LateUpdate.
    // Original application managers still own material, texture and part lifetimes.
    public sealed class OfflineProfessionBattlePlayerResources : IDisposable
    {
        private readonly HashSet<Player> players = new HashSet<Player>();
        private readonly GameObject prefab;
        private readonly ProfessionNativeSceneAppearanceResources characters;
        private readonly ProfessionAppearanceTables tables;
        private readonly AppearanceConfig config;
        private readonly AppearanceAnimNameCombineRule rules;
        private readonly ProfessionAppearanceSkinDefaults defaults;
        private readonly OfflineProfessionSceneTextureResources bodyTextures;
        private readonly AppearanceAssetManager manager;
        private readonly OfflineProfessionAppearancePartBindings parts;
        private readonly CharacterPartEntryProvider pool;
        private readonly IDisposable textureRoutes;
        private int updatedFrame = -1;
        private bool disposed;

        public OfflineProfessionBattlePlayerResources()
        {
            try
            {
                prefab = Required<GameObject>("Combat/SceneAppearance/PlayerModel_Game");
                characters = Required<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
                var weapons = Required<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
                var weaponTextures = Required<ProfessionNativeWeaponTextureResources>("Combat/Weapons/texture-resources");
                config = Required<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                rules = Required<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
                tables = ProfessionAppearanceTables.Load(Required<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                defaults = new ProfessionAppearanceSkinDefaults(tables,
                    Required<TextAsset>("Combat/SceneAppearance/skin-defaults").text, Debug.LogError);
                bodyTextures = new OfflineProfessionSceneTextureResources(characters);
                manager = new AppearanceAssetManager(weaponTextures.ManagerConfig, callback => new LowMemorySubscription(callback));
                var materials = new OfflineProfessionWeaponTextureResources(config, manager, weaponTextures);
                textureRoutes = new OfflineProfessionBattleSceneTextures(characters, bodyTextures, weaponTextures, materials).Register();
                parts = new OfflineProfessionAppearancePartBindings(config, tables.SpineSkinInfoParser,
                    Required<OfflineProfessionImageCatalog>("Combat/PlayerEffects/catalog"));
                pool = new CharacterPartEntryProvider(new OfflineProfessionScenePartPoolBindings(weapons, materials, parts, () => true));
            }
            catch { Dispose(); throw; }
        }

        public Player Create(Transform parent, long playerId, Func<OfflinePlayerProgress> readPlayer, Func<int, int?> itemType)
        {
            ThrowIfDisposed();
            var appearance = new OfflineProfessionAppearanceBindings(playerId, readPlayer, itemType, tables, config, rules, defaults);
            var bindings = new OfflineProfessionScenePlayerBindings(characters, manager, parts, pool, appearance);
            var actor = new ProfessionNativeBattlePlayerActor(prefab, parent, bindings, playerId);
            var player = new Player(this, actor, appearance);
            players.Add(player);
            return player;
        }

        // Called by the battle host. Multiple role views cannot advance shared
        // cache timers twice within one Unity frame; this never ticks an actor.
        public void Update()
        {
            ThrowIfDisposed();
            if (updatedFrame == Time.frameCount) return;
            updatedFrame = Time.frameCount;
            manager.Update(); pool.Update();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var player in new List<Player>(players)) player.Dispose();
            pool?.Dispose();
            manager?.Dispose();
            textureRoutes?.Dispose();
            bodyTextures?.Dispose();
        }

        private void ThrowIfDisposed()
        { if (disposed) throw new ObjectDisposedException(nameof(OfflineProfessionBattlePlayerResources)); }
        private static T Required<T>(string address) where T : UnityEngine.Object
            => Resources.Load<T>(address) ?? throw new InvalidOperationException("Packaged original battle player resource is unavailable: " + address);

        public sealed class Player : IDisposable
        {
            private OfflineProfessionBattlePlayerResources owner;
            public ProfessionNativeBattlePlayerActor Actor { get; }
            public OfflineProfessionAppearanceBindings Appearance { get; }
            internal Player(OfflineProfessionBattlePlayerResources owner, ProfessionNativeBattlePlayerActor actor,
                OfflineProfessionAppearanceBindings appearance)
            { this.owner = owner; Actor = actor; Appearance = appearance; }
            public void RefreshAppearance()
            {
                if (owner == null) throw new ObjectDisposedException(nameof(Player));
                Appearance.Refresh(AppearanceOperateSource.Normal);
            }
            public void Dispose()
            {
                if (owner == null) return;
                var previous = owner; owner = null;
                previous.players.Remove(this);
                Actor.Dispose();
            }
        }

        private sealed class LowMemorySubscription : IDisposable
        {
            private Application.LowMemoryCallback callback;
            internal LowMemorySubscription(Action callback)
            { this.callback = callback.Invoke; Application.lowMemory += this.callback; }
            public void Dispose()
            { if (callback == null) return; Application.lowMemory -= callback; callback = null; }
        }
    }
}
