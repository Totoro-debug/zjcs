using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using SceneCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;
using ScenePlayer = Zjcs.Official.CombatAppearance.PlayerAppearance;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunScenePlayerAppearance()
        {
            Run(); SessionState.SetBool(Key + ".scenePlayerAppearance", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator ScenePlayerAppearanceSteps(
            Func<GameObject, OfflineProfessionAppearanceBindings, ScenePlayer> createPlayer = null,
            Action<ScenePlayer> afterInit = null, string labelSuffix = "")
        {
            var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            var defaults = new ProfessionAppearanceSkinDefaults(tables, Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text,
                message => { throw new InvalidOperationException(message); });
            Require(catalog && weapons && config && rules, "selected current player configuration resources");
            foreach (int weaponType in new[] { 200, 201 })
            {
                var initialProfile = ScenePlayerProfile(resolver, config, Sex.Male, 0);
                initialProfile.InitialWeaponType = weaponType;
                string initialSave = JsonUtility.ToJson(initialProfile);
                var initial = new OfflineProfessionAppearanceBindings(17, () => initialProfile, _ => weaponType, tables, config, rules, defaults);
                var initialData = (AppearanceData)initial.AppearanceData;
                Require(initialData.SkinMap[SkinType.pWeapon].SkinId == (weaponType == 200 ? 35000 : 36000)
                    && JsonUtility.ToJson(initialProfile) == initialSave, "current initial weapon defaults restore a private snapshot without modifying the unpromoted save");
            }
            var previous = ProfessionAppearanceTextureBindings.Current;
            using var textures = new OfflineProfessionSceneTextureResources(catalog);
            using var manager = new AppearanceAssetManager(weapons.Textures.ManagerConfig, _ => new SceneSubscription());
            var weaponMaterials = new OfflineProfessionWeaponTextureResources(config, manager, weapons.Textures);
            ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(catalog, textures, weapons.Textures, weaponMaterials);
            try
            {
                foreach (Sex sex in new[] { Sex.Male, Sex.Female })
                foreach (int profession in new[] { 1, 10 })
                {
                    OfflinePlayerProgress profile = ScenePlayerProfile(resolver, config, sex, profession);
                    string saved = JsonUtility.ToJson(profile);
                    var graph = new OfflineProfessionAppearanceBindings(17, () => profile, _ => profile.InitialWeaponType, tables, config, rules, defaults);
                    var host = new GameObject("Current player profile appearance lifecycle");
                    host.AddComponent<SortingGroup>().sortingOrder = 2000;
                    var root = new GameObject("PlayerModel_Game"); root.transform.SetParent(host.transform, false);
                    root.transform.localScale = new Vector3(-.3f, .3f, 1);
                    var native = root.AddComponent<ProfessionNativeSceneRenderer>(); native.Configure(catalog);
                    var bindings = new OfflineProfessionSceneAppearanceBindings(catalog, native, manager, config);
                    var partBindings = new ScenePartBindings(config);
                    AppearanceRendererPartHandler parts = SessionState.GetBool(Key + ".sceneMecanimParts", false)
                        ? root.AddComponent<AppearanceMecanimPartHandler>() : root.AddComponent<AppearanceRendererPartHandler>();
                    parts.ConfigureParts(partBindings);
                    var slot = new GameObject("PartSlot").transform; slot.SetParent(root.transform, false);
                    var poolResources = new OfflineProfessionScenePartPoolBindings(weapons, weaponMaterials, partBindings,
                        new SceneDiagnosticWeaponPoses(), () => true);
                    using var pool = new CharacterPartEntryProvider(poolResources);
                    parts.ConfigureRenderer(native, slot, pool, SortingLayer.IDToName(1182434123), 0);
                    var appearance = root.AddComponent<ProfessionNativeSceneAppearance>();
                    appearance.Configure(bindings, parts, state =>
                    {
                        if (SessionState.GetBool(Key + ".sceneMecanimParts", false)) native.ApplyAnimatorPose();
                        else ProfessionNativeConstraintPose.EvaluateBeforePhysics(state,
                            new ProfessionNativeAnimationChannels(state.Source, graph.GetProfessionIdStr(graph.Profession) + "/idle_01"), .4f, false);
                    });
                    var character = root.AddComponent<SceneCharacter>(); character.Configure(bindings, parts);
                    var observer = root.AddComponent<ScenePlayerAppearanceObserver>(); observer.Character = character;
                    var player = createPlayer == null ? root.AddComponent<ScenePlayer>() : createPlayer(root, graph);
                    player.ConfigurePlayerAppearance(graph);
                    player.AppendSkinType = AppendSkinType.Game; player.ConfigureAppearance(character, observer, null);
                    int events = 0, completions = 0, independentRefreshes = 0;
                    player.OnAppearanceRefreshed += () =>
                    {
                        Require(observer.Calls == events + independentRefreshes + 1, "dependent appearance info refresh precedes original completion event");
                        events++;
                    };
                    try
                    {
                        var transform = root.transform.localToWorldMatrix;
                        player.Init(17, onComplete: () => completions++);
                        Require(player.Inited && player.IsSelf && events == 1 && completions == 1,
                            "original self registration, refresh, event and initialization callback");
                        if (SessionState.GetBool(Key + ".sceneMecanimParts", false))
                        {
                            int automatic = 0;
                            var before = Enumerable.Range(0, native.State.Pose.Count).Select(native.State.Pose.World).ToArray();
                            ProfessionPartClipApplied applied = (animation, name, layer, weight, time, lastTime, backward) =>
                            { if (time > 0) automatic++; };
                            native.OnClipApplied += applied;
                            try
                            {
                                for (int frame = 0; automatic < 2 && frame < 60; frame++) yield return null;
                                Require(automatic >= 2 && before.Where((matrix, i) => matrix != native.State.Pose.World(i)).Any(),
                                    "actual Play Mode LateUpdate consumes the live Animator and changes native bones without a manual sampling call");
                            }
                            finally { native.OnClipApplied -= applied; }
                        }
                        if (SessionState.GetBool(Key + ".sceneBoundMixNext", false))
                        {
                            var mixing = ObserveSceneBoundMixNext(native, root.GetComponent<Animator>(), labelSuffix);
                            while (mixing.MoveNext()) yield return mixing.Current;
                        }
                        afterInit?.Invoke(player);
                        Require(saved == JsonUtility.ToJson(profile), "default restoration and player refresh preserve persisted local wardrobe bytes");
                        Require(appearance.Skins.Contains("performFace") && !appearance.Skins.Contains("map_01")
                            && player.AppearanceRuntimeData.SkinMap[SkinType.pWeapon] == (profession == 1 ? "pWeapon_100_G_007" : "pWeapon_200_G_007"),
                            "current default profession weapon and official Game append selection");
                        var weaponGo = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
                        var weapon = weaponGo.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                        Action draw = () =>
                        {
                            native.LateUpdateMesh(); weaponGo.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh();
                        };
                        draw();
                        string label = "player-" + sex + "-" + profession + labelSuffix;
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-visibility"); draw();
                        var bounds = SceneCharacterBounds(native, weapon);
                        var baseline = CaptureSceneRendererCamera(bounds, label + "-default");
                        graph.Refresh(AppearanceOperateSource.Map); graph.Refresh(AppearanceOperateSource.ProfessionPromote);
                        Require(events == 1, "original map and profession-promotion notifications skip refresh");
                        graph.Refresh(AppearanceOperateSource.Normal); draw();
                        Require(events == 2 && baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-refreshed")),
                            "normal local snapshot refresh retains exact current default GPU appearance");
                        string hair = player.AppearanceRuntimeData.SkinMap[SkinType.hair];
                        string otherHair = native.State.Source.SkinNames.First(n => n.StartsWith("hair_", StringComparison.Ordinal) && n != hair);
                        var hairOverride = new ScenePlayerHairOverride(otherHair);
                        player.SetRuntimeDataOverride(hairOverride); independentRefreshes++; draw();
                        Require(player.AppearanceRuntimeData.SkinMap[SkinType.hair] == otherHair && observer.Calls == 3 && events == 2,
                            "original temporary override updates dependents without publishing a profile refresh event");
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-override");
                        player.SetRuntimeDataOverride(hairOverride);
                        Require(observer.Calls == 3, "same override identity is ignored");
                        player.SetRuntimeDataOverride(null); independentRefreshes++; draw();
                        Require(player.AppearanceRuntimeData.SkinMap[SkinType.hair] == hair
                            && baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-override-restored")),
                            "clearing override restores recorded original skin and exact portrait pixels");
                        root.SetActive(false);
                        graph.Refresh(AppearanceOperateSource.Normal);
                        Require(events == 2, "disabled player unsubscribes from local appearance notifications");
                        root.SetActive(true); draw();
                        Require(events == 3, "enabled initialized player re-registers and immediately refreshes");
                        Sex opposite = sex == Sex.Male ? Sex.Female : Sex.Male;
                        profile = ScenePlayerProfile(resolver, config, opposite, profession);
                        string swappedSave = JsonUtility.ToJson(profile);
                        graph.Refresh(AppearanceOperateSource.Normal); draw();
                        Require(events == 4 && native.State.Source.Sex == (opposite == Sex.Male ? "male" : "female")
                            && parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == weaponGo,
                            "real local profile sex change rebuilds body and retains equipped weapon through original player refresh");
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-opposite-visibility"); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-opposite");
                        Require(swappedSave == JsonUtility.ToJson(profile), "cross-sex projection does not rewrite the selected save");
                        profile = JsonUtility.FromJson<OfflinePlayerProgress>(saved);
                        graph.Refresh(AppearanceOperateSource.Normal); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-restored-visibility"); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-profile-restored"))
                            && root.transform.localToWorldMatrix == transform, "saved wardrobe restoration preserves exact portrait pixels, source scale and anchor");
                        var hairDye = profile.Wardrobe.Dyes.Single(dye => dye.Type == (int)DyeType.Hair);
                        hairDye.Hue = (hairDye.Hue + 120) % 360; hairDye.Saturation = 70; hairDye.Value = 60;
                        string dyedSave = JsonUtility.ToJson(profile);
                        graph.Refresh(AppearanceOperateSource.Normal); draw();
                        Require(!baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-profile-dyed"))
                            && dyedSave == JsonUtility.ToJson(profile), "saved hair HSV reaches original resolver and actual native materials without rewriting profile data");
                        profile = JsonUtility.FromJson<OfflinePlayerProgress>(saved);
                        graph.Refresh(AppearanceOperateSource.Normal); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-dye-restored")),
                            "restoring the saved HSV restores exact default portrait pixels");
                        profile = ScenePlayerProfile(resolver, config, sex, profession == 1 ? 10 : 1);
                        graph.Refresh(AppearanceOperateSource.Normal);
                        weaponGo = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
                        weapon = weaponGo.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-profession-visibility"); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-profession-changed");
                        Require(player.AppearanceRuntimeData.SkinMap[SkinType.pWeapon] == (profession == 1 ? "pWeapon_200_G_007" : "pWeapon_100_G_007"),
                            "local profession change selects the other original default weapon");
                        profile = JsonUtility.FromJson<OfflinePlayerProgress>(saved);
                        graph.Refresh(AppearanceOperateSource.Normal);
                        weaponGo = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
                        weapon = weaponGo.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true); draw();
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-profession-restored-visibility"); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-profession-restored")),
                            "restoring profession restores the original weapon and exact portrait pixels");
                        int beforeClear = events;
                        player.Clear();
                        Require(!player.Inited && !player.IsSelf && player.AppearanceRuntimeData == null && !appearance.Skins.Any()
                            && appearance.IndependentMaterialDict.Count == 0 && !character.TryGetSkinName(SkinType.hair, out _)
                            && parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == weaponGo,
                            "original ordinary player clear unsubscribes, clears body/material state and retains independent parts for reinitialization");
                        graph.Refresh(AppearanceOperateSource.Normal);
                        Require(events == beforeClear, "cleared player receives no local events");
                        player.Init(17); draw();
                        weaponGo = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
                        weapon = weaponGo.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
                        CaptureSceneRendererCamera(SceneCharacterBounds(native, weapon), label + "-reinit-visibility"); draw();
                        Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-reinitialized")),
                            "original player clear/reinit restores exact current persisted appearance");
                        Debug.Log("[Current native player appearance] " + sex + "/" + profession + ": "
                            + string.Join(",", appearance.Skins) + "; default/profile/override/event/sex-change/clear/reinit camera lifecycle passed.");
                    }
                    finally { player.Clear(); character.Release(true); pool.ClearPool(); Object.DestroyImmediate(host); }
                    catalog.Loader.TryUnloadUnusedTextures(true);
                    yield return null;
                    Require(textures.LoadedTextureCount == 0, "player teardown releases actual body Low/dye requests");
                }
            }
            finally { ProfessionAppearanceTextureBindings.Current = previous; }
        }

        private static OfflinePlayerProgress ScenePlayerProfile(ProfessionAppearanceResolver resolver, AppearanceConfig config, Sex sex, int profession)
        {
            var data = resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession);
            var profile = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
            profile.Wardrobe.Sex = (int)sex; profile.Wardrobe.Profession = profession;
            foreach (var pair in data.SkinMap)
                profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
            foreach (var pair in data.DyeMap)
                profile.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                    Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
            return profile;
        }

        public sealed class ScenePlayerAppearanceObserver : APlayerAppearanceInfo
        {
            internal SceneCharacter Character;
            internal int Calls;
            public override void RefreshAppearance(Sex sex, int profession, AppearanceRuntimeData data)
            {
                Require(Character.TryGetSkinName(SkinType.hair, out string hair) && hair == data.SkinMap[SkinType.hair],
                    "original dependent info observes the already applied character appearance");
                Calls++;
            }
        }

        private sealed class ScenePlayerHairOverride : IAppearanceRuntimeDataOverride
        {
            private readonly string hair;
            internal ScenePlayerHairOverride(string hair) { this.hair = hair; }
            public void ApplyOverride(AppearanceRuntimeData data, object source, Dictionary<SkinType, string> modifiedSkinDict = null)
            {
                modifiedSkinDict[SkinType.hair] = data.SkinMap[SkinType.hair];
                data.SkinMap[SkinType.hair] = hair;
            }
        }
    }
}
