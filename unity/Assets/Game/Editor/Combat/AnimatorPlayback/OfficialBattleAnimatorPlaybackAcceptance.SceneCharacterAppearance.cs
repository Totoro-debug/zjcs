using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using SceneCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneCharacterAppearance()
        {
            Run(); SessionState.SetBool(Key + ".sceneCharacterAppearance", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneCharacterAppearanceSteps()
        {
            var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            Require(config && config != AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset"),
                "selected current configuration has its own actual resource identity");
            ValidateSceneCharacterConfig(config);
            var previous = ProfessionAppearanceTextureBindings.Current;
            using var textures = new OfflineProfessionSceneTextureResources(catalog);
            using var manager = new AppearanceAssetManager(weapons.Textures.ManagerConfig, _ => new SceneSubscription());
            var weaponMaterials = new OfflineProfessionWeaponTextureResources(config, manager, weapons.Textures);
            ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(catalog, textures, weapons.Textures, weaponMaterials);
            try
            {
                foreach (var model in catalog.Models)
                foreach (string group in new[] { "100", "200" })
                {
                    var host = new GameObject("Current original character appearance native camera verification");
                    host.AddComponent<SortingGroup>().sortingOrder = 2000;
                    var root = new GameObject("PlayerModel_Game"); root.transform.SetParent(host.transform, false);
                    root.transform.localScale = new Vector3(-.3f, .3f, 1);
                    var native = root.AddComponent<ProfessionNativeSceneRenderer>(); native.Configure(catalog);
                    var bindings = new OfflineProfessionSceneAppearanceBindings(catalog, native, manager, config);
                    Require(!bindings.TryGetPlayerSkeletonAssets(Sex.None, out var invalid, out var invalidSplit) && !invalid && !invalidSplit,
                        "original invalid-sex asset lookup rejects without substituting female");
                    var parts = root.AddComponent<AppearanceRendererPartHandler>();
                    var partBindings = new ScenePartBindings(config); parts.ConfigureParts(partBindings);
                    var slot = new GameObject("PartSlot").transform; slot.SetParent(root.transform, false);
                    var poolResources = new OfflineProfessionScenePartPoolBindings(weapons, weaponMaterials, partBindings,
                        new SceneDiagnosticWeaponPoses(), () => true);
                    using var pool = new CharacterPartEntryProvider(poolResources);
                    parts.ConfigureRenderer(native, slot, pool, SortingLayer.IDToName(1182434123), 0);
                    var appearance = root.AddComponent<ProfessionNativeSceneAppearance>();
                    string action = group + "/idle_01";
                    appearance.Configure(bindings, parts, state => ProfessionNativeConstraintPose.EvaluateBeforePhysics(
                        state, new ProfessionNativeAnimationChannels(state.Source, action), .4f, false));
                    var character = root.AddComponent<SceneCharacter>(); character.Configure(bindings, parts);
                    var map = SceneCharacterSkins(model, group);
                    var data = new AppearanceRuntimeData(model.Sex == "male" ? Sex.Male : Sex.Female, map,
                        appendSkins: new List<string> { "default" });
                    try
                    {
                        var callerTransform = root.transform.localToWorldMatrix;
                        character.SetRuntimeData(data);
                        var initialState = native.State;
                        Require(appearance.Valid && !appearance.UseSplitAnimation && appearance.SkeletonDataAsset == model.Asset,
                            "current original character consumes actual fully exported native asset");
                        Require(!character.TryGetSkinName(SkinType.pWeapon, out _) && !character.TryGetSkinName(SkinType.faceDecoration, out _)
                            && appearance.Skins.SequenceEqual(map.Where(p => p.Value != "emptySkin" && config.GetPartConfig(p.Key) == null)
                                .Select(p => p.Value).Concat(data.AppendSkins)), "original part/empty filtering, source skin order and append order");
                        var go = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
                        var weapon = go.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                        Action draw = () =>
                        {
                            native.LateUpdateMesh();
                            go.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                        };
                        draw();
                        var bounds = SceneCharacterBounds(native, weapon);
                        string label = "appearance-" + model.Sex + "-" + group;
                        // The original follower deliberately skips invisible
                        // targets (updateWhenInvisible=0). Let the real camera
                        // establish visibility before comparing stable frames.
                        CaptureSceneRendererCamera(bounds, label + "-visibility"); draw();
                        Require(native.UpdateMode == 3, "actual camera activates original visible-target follower branch");
                        bounds = SceneCharacterBounds(native, weapon);
                        var baseline = CaptureSceneRendererCamera(bounds, label + "-original");
                        weapon.State.SetToSetupPose(); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-weapon-pose-reapplied")),
                            "native animation-and-mesh request reapplies completed weapon pose after a setup reset");
                        var originals = appearance.IndependentMaterialDict.Keys.ToArray();
                        var sourceDyes = originals.ToDictionary(m => m, m => m.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)));
                        var sourceTextures = originals.ToDictionary(m => m, m => m.mainTexture);
                        var body = map[SkinType.body];
                        Require(appearance.TryGetMaterial(body, out var bodyMaterial) && bodyMaterial.GetTexture("_DyeTex")
                            && bodyMaterial.mainTexture && !bodyMaterial.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal),
                            "actual original appearance loads body Low and dye textures");
                        var dye = new HsvChange { OffsetH = .4f, OffsetScaleS = .7f, OffsetScaleV = .5f };
                        data.DyeMap = new Dictionary<SkinType, Dictionary<int, HsvChange>>
                        { { SkinType.body, new Dictionary<int, HsvChange> { { 1, dye }, { 2, dye }, { 3, dye } } } };
                        character.SetRuntimeData(data); draw();
                        Require(ReferenceEquals(initialState, native.State) && parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == go,
                            "same character runtime data retains actual skeleton and equipped weapon");
                        Require(appearance.TryGetMaterial(body, out bodyMaterial)
                            && bodyMaterial.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)) == new Vector4(.4f, .7f, .5f, 0),
                            "current original character copies and applies requested dye channels");
                        data.DyeMap[SkinType.body][1] = new HsvChange();
                        Require(bodyMaterial.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)).x == .4f, "caller dye dictionary does not alias applied material data");
                        Require(!baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-dyed")), "actual portrait GPU pixels change after source dye application");
                        Require(originals.All(m => m.GetVector(AppearanceMaterialUtils.GetDyePropertyId(1)) == sourceDyes[m]
                            && m.mainTexture == sourceTextures[m]),
                            "original atlas materials remain isolated from instance dye");
                        data.DyeMap = null;
                        character.SetRuntimeData(data); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-reset")),
                            "actual material pool resets dye and restores exact original portrait pixels");
                        action = group + "/skill_41";
                        character.SetSkins(map, appendSkins: data.AppendSkins); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-skill");
                        action = group + "/idle_01";
                        var withoutWeapon = new Dictionary<SkinType, string>(map); withoutWeapon.Remove(SkinType.pWeapon);
                        character.SetSkins(withoutWeapon, appendSkins: data.AppendSkins);
                        Require(!go.activeInHierarchy && poolResources.LiveInstanceCount == 1, "source character removal returns actual independent weapon to pool");
                        character.SetRuntimeData(data); draw();
                        Require(parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == go
                            && baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-reequipped")),
                            "source character re-equips original pooled weapon with exact camera composition");
                        character.Release(true); pool.ClearPool();
                        Require(!appearance.Skins.Any() && appearance.IndependentMaterialDict.Count == 0
                            && parts.LoadedSkeletonDict.Count == 0 && poolResources.LiveInstanceCount == 0,
                            "source full character release clears skins, independent materials and actual parts");
                        character.SetRuntimeData(data);
                        weapon = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                        go = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject; draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-reload-visibility"); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-released-reloaded"))
                            && root.transform.localToWorldMatrix == callerTransform, "full release/reload restores original pixels and preserves caller scale/anchor");
                        var combined = native.State.Skin;
                        var originalSeparator = native.Separator;
                        var opposite = catalog.Models.Single(candidate => candidate.Sex != model.Sex);
                        bool rejected = false;
                        try { bindings.GetSkeletonData(opposite.Asset, true); }
                        catch (InvalidOperationException) { rejected = true; }
                        Require(rejected && ReferenceEquals(combined, native.State.Skin), "a catalog query cannot rebind the active skin to an uninitialized foreign skeleton");
                        rejected = false;
                        try { combined.AddSkin(opposite.Asset.Data.SkinCatalog.FindSkin("default")); }
                        catch (ArgumentException) { rejected = true; }
                        Require(rejected, "populated cross-layout skin combinations still fail explicitly");
                        var oppositeData = new AppearanceRuntimeData(opposite.Sex == "male" ? Sex.Male : Sex.Female,
                            SceneCharacterSkins(opposite, group), appendSkins: data.AppendSkins);
                        character.SetRuntimeData(oppositeData); draw();
                        Require(appearance.SkeletonDataAsset == opposite.Asset && native.State.Source == opposite.Asset.Data
                            && ReferenceEquals(combined, native.State.Skin) && ReferenceEquals(originalSeparator, native.Separator),
                            "same original character retains its combined skin and separator across different native skeleton layouts");
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-opposite-visibility"); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-opposite");
                        Require(parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == go,
                            "cross-layout character switch retains and rebinds its actual equipped weapon");
                        character.SetRuntimeData(data); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-restored-visibility"); draw();
                        Require(ReferenceEquals(combined, native.State.Skin)
                            && baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-layout-restored"))
                            && root.transform.localToWorldMatrix == callerTransform,
                            "switching back restores exact original camera pixels, combined-skin identity and caller transform");
                        Debug.Log("[Current native character appearance] " + model.Sex + "/" + group + ": "
                            + string.Join(",", appearance.Skins) + "; actual dye/reset/part-return/reload and cross-layout round-trip camera comparisons passed.");
                    }
                    finally
                    {
                        character.Release(true); pool.ClearPool(); Object.DestroyImmediate(host);
                    }
                    catalog.Loader.TryUnloadUnusedTextures(true);
                    yield return null;
                    Require(textures.LoadedTextureCount == 0, "character teardown releases every actual Low/dye request");
                }
            }
            finally { ProfessionAppearanceTextureBindings.Current = previous; }
            Debug.Log("Current original character runtime data drives native appearance, actual texture/material pooling and independent weapons. Pose inputs remain explicit unmixed pre-physics diagnostics; no UI input or production battle is claimed.");
        }

        private static Dictionary<SkinType, string> SceneCharacterSkins(ProfessionNativeSceneAppearanceResources.Model model, string group)
        {
            string sex = model.Sex == "male" ? "M" : "F";
            var result = new Dictionary<SkinType, string>
            {
                { SkinType.body, "body_" + group + "_" + sex + "_01" },
                { SkinType.faceShape, "faceShape_" + sex + "_01" },
                { SkinType.hair, model.Sex == "male" ? "hair_G_01" : "hair_F_01" },
                { SkinType.pWeapon, "pWeapon_" + group + "_G_007" }, { SkinType.faceDecoration, "emptySkin" }
            };
            // Explicit appearance parameters chosen from the actual current data;
            // this fixture does not assert a player creation/default selection rule.
            foreach (var type in new[] { SkinType.eye, SkinType.brow, SkinType.mouth, SkinType.ethnicity })
            {
                string prefix = type + "_";
                var names = model.Asset.Data.SkinNames.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                string selected = names.FirstOrDefault(n => n.Contains("_" + sex + "_") && n.EndsWith("_01", StringComparison.Ordinal))
                    ?? names.FirstOrDefault(n => n.Contains("_G_") && n.EndsWith("_01", StringComparison.Ordinal));
                Require(selected != null, "actual current explicit face skin: " + type);
                result.Add(type, selected);
            }
            return result;
        }

        private static Bounds SceneCharacterBounds(ProfessionNativeSceneRenderer native, ProfessionNativeSceneWeaponRenderer weapon)
        {
            var parts = ((DynamicSkeletonRenderSeparator)native.Separator).PartsRenderers.Cast<ProfessionNativeSceneMeshPart>().ToArray();
            var bounds = parts[0].MeshRenderer.bounds;
            foreach (var part in parts) bounds.Encapsulate(part.MeshRenderer.bounds);
            bounds.Encapsulate(weapon.GetComponent<MeshRenderer>().bounds);
            return bounds;
        }

        private sealed class SceneDiagnosticWeaponPoses : IProfessionNativeSceneWeaponPoseProducer
        {
            private readonly Dictionary<ProfessionNativeAppearanceSkeleton, string> requests = new Dictionary<ProfessionNativeAppearanceSkeleton, string>();
            public void Play(ProfessionNativeAppearanceSkeleton state, string animation, bool loop)
            {
                Require(loop, "current source weapon animation requests loop");
                requests[state] = animation;
            }
            public void Apply(ProfessionNativeAppearanceSkeleton state)
                => state.EvaluateUnmixedWeaponAnimation(requests[state], .4f);
        }

        [Serializable] private sealed class SceneCharacterConfigFixture { public SceneCharacterConfigFields original_fields; }
        [Serializable] private sealed class SceneCharacterConfigFields
        {
            public SceneCharacterSkinFlags[] mPlayerSkinTypes;
            public SceneCharacterAppend[] mAppendSkinConfigs;
            public AppearancePartConfig[] mPartConfigs;
            public AppearanceOverrideConfig[] mAppearanceOverrideConfigs;
            public AppearanceHashValue[] mAppearanceHashConfigs;
            public AppearanceSkeletonPath[] mAppearanceSkeletonPaths;
            public Color MimicsMonsterColor;
        }
        [Serializable] private sealed class SceneCharacterSkinFlags { public int mSkinType, mBaseSkin, mCanNormalSet; }
        [Serializable] private sealed class SceneCharacterAppend { public int mAppendSkinType; public string[] mAppendSkins; }

        private static void ValidateSceneCharacterConfig(AppearanceConfig config)
        {
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var expected = JsonUtility.FromJson<SceneCharacterConfigFixture>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/battle-animation/scene-character-appearance-source-manifest.json"))).original_fields;
            Require(expected.mPlayerSkinTypes.Length == 13 && expected.mAppearanceHashConfigs.Length == 1750
                && expected.mAppearanceSkeletonPaths.Length == 466 && config.MimicsMonsterColor == expected.MimicsMonsterColor,
                "actual current original configuration scope and color");
            Require(config.PlayerSkinTypes.Count == expected.mPlayerSkinTypes.Length, "all current player skin flags imported");
            for (int i = 0; i < expected.mPlayerSkinTypes.Length; i++)
            {
                var actual = config.PlayerSkinTypes[i]; var flags = expected.mPlayerSkinTypes[i];
                Require((int)actual.SkinType == flags.mSkinType && actual.BaseSkin == (flags.mBaseSkin != 0)
                    && actual.CanNormalSet == (flags.mCanNormalSet != 0), "current skin enum/flags and order");
            }
            var appends = SceneCharacterConfigField<AppendSkinConfig>(config, "mAppendSkinConfigs");
            Require(appends.Count == expected.mAppendSkinConfigs.Length, "all current append groups imported");
            for (int i = 0; i < appends.Count; i++)
                Require((int)appends[i].AppendSkinType == expected.mAppendSkinConfigs[i].mAppendSkinType
                    && appends[i].AppendSkins.SequenceEqual(expected.mAppendSkinConfigs[i].mAppendSkins), "current append group names and order");
            Require(SceneCharacterConfigField<AppearancePartConfig>(config, "mPartConfigs").Select(value => JsonUtility.ToJson(value))
                    .SequenceEqual(expected.mPartConfigs.Select(value => JsonUtility.ToJson(value)))
                && SceneCharacterConfigField<AppearanceOverrideConfig>(config, "mAppearanceOverrideConfigs").Select(value => JsonUtility.ToJson(value))
                    .SequenceEqual(expected.mAppearanceOverrideConfigs.Select(value => JsonUtility.ToJson(value))), "every current part alias and sex-specific override");
            Require(config.AppearanceToHash.Select(value => JsonUtility.ToJson(value)).SequenceEqual(expected.mAppearanceHashConfigs.Select(value => JsonUtility.ToJson(value))),
                "all 1750 ordered current skin names and signed 64-bit hashes");
            Require(SceneCharacterConfigField<AppearanceSkeletonPath>(config, "mAppearanceSkeletonPaths").Select(value => JsonUtility.ToJson(value))
                .SequenceEqual(expected.mAppearanceSkeletonPaths.Select(value => JsonUtility.ToJson(value))), "all 466 current skeleton names and resource paths");
            Debug.Log("[Current native character configuration] Every original field, 13 skin types, four append groups, part aliases, sex overrides, 1750 signed hashes and 466 skeleton paths imported exactly.");
        }

        private static List<T> SceneCharacterConfigField<T>(AppearanceConfig config, string name)
            => (List<T>)typeof(AppearanceConfig).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(config);
    }
}
