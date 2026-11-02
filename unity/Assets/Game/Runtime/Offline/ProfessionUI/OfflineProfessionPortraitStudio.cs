using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using SceneCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;
using ScenePlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    // Production photographer bindings, with the original studio prefab and
    // real saved-player/material/part services supplied by the owning feature.
    public sealed class OfflineProfessionPortraitStudio : IProfessionPortraitPhotographerBindings, IDisposable
    {
        private readonly ProfessionNativePortraitStudioResources resources;
        private readonly IProfessionPlayerAppearanceBindings player;
        private readonly IAppearanceAssetManager materials;
        private readonly ICharacterPartEntryProvider partProvider;
        private readonly IProfessionAppearancePartBindings parts;
        private GameObject instance;
        private ProfessionNativePortraitAppearance animation;
        private SceneCharacter character;
        private bool disposed;
        public PlayerPortraitSettings Settings => resources.Settings;

        public OfflineProfessionPortraitStudio(ProfessionNativePortraitStudioResources resources,
            IProfessionPlayerAppearanceBindings player, IAppearanceAssetManager materials,
            ICharacterPartEntryProvider partProvider, IProfessionAppearancePartBindings parts)
        {
            this.resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
            if (!resources.StudioPrefab || !resources.Hierarchy || !resources.Settings
                || !resources.AppearanceConfig || !resources.Characters)
                throw new ArgumentException("Original portrait resource closure is incomplete.");
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
            this.partProvider = partProvider ?? throw new ArgumentNullException(nameof(partProvider));
            this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        }

        public CharacterPortraitPhotographer InstantiateStudio()
        {
            if (disposed || instance) throw new InvalidOperationException("This original portrait studio already has an owner.");
            var staging = new GameObject("Original portrait binding scope");
            staging.SetActive(false);
            var previousClip = ProfessionAppearanceSkinClipBindings.Current;
            try
            {
                instance = UnityEngine.Object.Instantiate(resources.StudioPrefab, staging.transform, false);
                animation = instance.GetComponentInChildren<ProfessionNativePortraitAppearance>(true);
                character = instance.GetComponentInChildren<SceneCharacter>(true);
                var renderer = animation.GetComponent<ProfessionNativeSceneRenderer>();
                var handler = animation.GetComponent<AppearanceRendererPartHandler>();
                var playerAppearance = animation.GetComponent<ScenePlayerAppearance>();
                renderer.ConfigurePortrait(resources.Characters, resources.Hierarchy);
                var bindings = new OfflineProfessionSceneAppearanceBindings(resources.Characters, renderer, materials, resources.AppearanceConfig);
                character.Configure(bindings, handler);
                animation.Configure(bindings, handler);
                handler.ConfigureParts(parts);
                handler.ConfigureRenderer(renderer, animation.transform.Find(resources.PartSlot), partProvider,
                    resources.SortingLayer, resources.SortingOrder);
                playerAppearance.ConfigurePlayerAppearance(player);
                var photographer = instance.GetComponent<CharacterPortraitPhotographer>();
                photographer.Configure(this);
                ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionPortraitSkinClipBindings();
                instance.transform.SetParent(null, false);
                return photographer;
            }
            catch
            {
                if (instance) UnityEngine.Object.Destroy(instance);
                instance = null;
                throw;
            }
            finally
            {
                ProfessionAppearanceSkinClipBindings.Current = previousClip;
                UnityEngine.Object.Destroy(staging);
            }
        }

        public ICharacterPortraitBuilder CreateBuilder(ProfessionPortraitKind type)
        {
            if (type != ProfessionPortraitKind.Head && type != ProfessionPortraitKind.BlindBox)
                throw new NotSupportedException("This original partner avatar entry uses Head/BlindBox; bind the profession builder for another entry.");
            return new HeadCharacterPortraitBuilder(Settings, OfflineProfessionAppearanceBindings.Clone);
        }
        public IProfessionPortraitRendererGraph GetRendererGraph(Component value) => value == animation
            ? animation : throw new ArgumentException("Portrait graph belongs to another actor.");
        public void ReleaseAppearance(Component value, bool tryUnloadAllAssets)
        {
            if (value != character) throw new ArgumentException("Portrait appearance belongs to another studio.");
            character.Release(tryUnloadAllAssets);
        }
        public string DescribeAppearance(object value) => value is AppearanceData data
            ? "AppearanceData skins=" + data.SkinMap.Count + " append=" + data.AppendAppearanceDict.Count
            : value?.GetType().FullName ?? "null";
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (character) character.Release(true);
            if (instance) UnityEngine.Object.Destroy(instance);
            instance = null;
        }
    }
}
