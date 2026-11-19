using System;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimation;

namespace Zjcs.Editor.Combat
{
    // Creates the actual default player for the isolated skill-graph playback test.
    internal sealed class NativeJumpAppearanceFixture : IDisposable
    {
        private sealed class Subscription : IDisposable { public void Dispose() { } }
        private sealed class PartBindings : IProfessionAppearancePartBindings
        {
            public AppearanceConfig Config { get; }
            internal PartBindings(AppearanceConfig config) { Config = config; }
            public bool HasEffect(string name) => false;
            public IProfessionAppearancePartAsset LoadPrefab(string path) => throw new NotSupportedException(path);
            public GameObject Instantiate(GameObject prefab, Transform parent, bool world) => throw new NotSupportedException();
        }

        private IProfessionAppearanceTextureBindings previousTextures;
        private OfflineProfessionSceneTextureResources textures;
        private AppearanceAssetManager manager;
        private CharacterPartEntryProvider pool;
        private Zjcs.Official.CombatAppearance.AppearanceCharacter character;
        private PlayerAnimatorAppearance player;
        private ProfessionNativeSceneAppearanceResources catalog;
        internal ProfessionNativeSceneRenderer Native { get; private set; }
        internal Animator Animator { get; private set; }

        internal static NativeJumpAppearanceFixture Create(GameObject root, int profession = 1)
        {
            var fixture = new NativeJumpAppearanceFixture();
            fixture.catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
            var animators = Resources.Load<ProfessionNativeSceneAnimatorCatalog>("Combat/SceneAppearance/player-animators");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            var defaults = new ProfessionAppearanceSkinDefaults(tables, Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
                message => throw new InvalidOperationException(message));
            var data = resolver.BuildDefaultAppearanceData(config, Sex.Male, (Profession)profession);
            var profile = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
            profile.Wardrobe.Sex = (int)Sex.Male; profile.Wardrobe.Profession = profession;
            foreach (var pair in data.SkinMap)
                profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
            foreach (var pair in data.DyeMap)
                profile.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                    Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
            var graph = new OfflineProfessionAppearanceBindings(17, () => profile, _ => profile.InitialWeaponType,
                tables, config, rules, defaults);
            fixture.textures = new OfflineProfessionSceneTextureResources(fixture.catalog);
            fixture.manager = new AppearanceAssetManager(weapons.Textures.ManagerConfig, _ => new Subscription());
            var weaponMaterials = new OfflineProfessionWeaponTextureResources(config, fixture.manager, weapons.Textures);
            fixture.previousTextures = ProfessionAppearanceTextureBindings.Current;
            ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(
                fixture.catalog, fixture.textures, weapons.Textures, weaponMaterials);

            root.AddComponent<SortingGroup>().sortingOrder = 2000;
            root.transform.localScale = new Vector3(-.3f, .3f, 1);
            fixture.Native = root.AddComponent<ProfessionNativeSceneRenderer>();
            fixture.Native.Configure(fixture.catalog);
            var bindings = new OfflineProfessionSceneAppearanceBindings(fixture.catalog, fixture.Native, fixture.manager, config);
            var partBindings = new PartBindings(config);
            var parts = root.AddComponent<AppearanceMecanimPartHandler>(); parts.ConfigureParts(partBindings);
            var slot = new GameObject("PartSlot").transform; slot.SetParent(root.transform, false);
            fixture.pool = new CharacterPartEntryProvider(new OfflineProfessionScenePartPoolBindings(
                weapons, weaponMaterials, partBindings, () => true));
            parts.ConfigureRenderer(fixture.Native, slot, fixture.pool, SortingLayer.IDToName(1182434123), 0);
            fixture.Animator = root.AddComponent<Animator>(); fixture.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            fixture.Native.BindAnimatorPose(new ProfessionNativeAnimatorPose(fixture.Animator,
                (state, channels, time, loop) => ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, channels, time, loop)));
            var appearance = root.AddComponent<ProfessionNativeSceneAppearance>();
            appearance.Configure(bindings, parts, state => fixture.Native.ApplyAnimatorPose());
            fixture.character = root.AddComponent<Zjcs.Official.CombatAppearance.AppearanceCharacter>();
            fixture.character.Configure(bindings, parts);
            fixture.player = root.AddComponent<PlayerAnimatorAppearance>(); fixture.player.ConfigurePlayerAppearance(graph);
            fixture.player.Animator = fixture.Animator; fixture.player.PlayerPhase = PlayerPhase.Game;
            fixture.player.ConfigureAnimatorResources(animators.Contains, animators.Load);
            fixture.player.AppendSkinType = AppendSkinType.Game; fixture.player.ConfigureAppearance(fixture.character);
            fixture.player.Init(17);
            fixture.Native.BindPlayerAnimator(fixture.Animator);
            var controller = root.AddComponent<AnimationController>();
            controller.NotCreateGraph = true; controller.myAnimator = fixture.Animator; controller.CreateOverrideController = false;
            controller.SetRuntimeAnimationController(new AnimatorOverrideController(fixture.Animator.runtimeAnimatorController));
            return fixture;
        }

        public void Dispose()
        {
            player.Clear(); character.Release(true); pool.ClearPool();
            catalog.Loader.TryUnloadUnusedTextures(true);
            ProfessionAppearanceTextureBindings.Current = previousTextures;
            textures.Dispose(); manager.Dispose();
        }
    }
}
