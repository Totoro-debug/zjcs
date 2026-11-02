using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerPlayerPortraitValidation
{
    const string Key = "PartnerPlayerPortraitValidation.Active";
    static PartnerPlayerPortraitValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original partner player portrait validation").AddComponent<PartnerPlayerPortraitRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Actual portrait capture did not finish"));
        };
    }
    public static void Run()
    {
        var work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 600);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "portrait-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerPlayerPortraitRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        var steps = Check();
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { PartnerPlayerPortraitValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerPlayerPortraitValidation.Finish(null);
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    IEnumerator Check()
    {
        var resources = ProfessionNativePortraitStudioResources.LoadDefault();
        foreach (var value in new[] { new[] { 0, 100, 120 }, new[] { 1, 260, 310 }, new[] { 2, 520, 620 }, new[] { 3, 312, 372 } })
        {
            var parameter = resources.Settings.GetParameters((ProfessionPortraitKind)value[0]);
            Require(parameter != null && parameter.TextureWidth == value[1] && parameter.TextureHeight == value[2],
                "All four original portrait kinds retain their enum arrays and dimensions");
        }
        var headParameters = resources.Settings.GetParameters(ProfessionPortraitKind.Head);
        Require(headParameters.ExcludeSkinTypes.Select(t => (int)t).SequenceEqual(new[] { 7, 8, 12 })
            && headParameters.Size == 1 && headParameters.DepthBuffer == 0 && !headParameters.CaptureParticle
            && headParameters.GetOffset() == new Vector3(-.07f, 2.7f, -5), "Original Head crop/exclusion/depth/particle parameters");
        Require(headParameters.GetHashWeightsBySkinType(SkinType.hair) == 1
            && headParameters.GetHashWeightsBySkinType(SkinType.body) == 0, "Original Head appearance hash weights");
        var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
        var rules = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
        var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        var defaults = new ProfessionAppearanceSkinDefaults(tables,
            Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, message => { throw new Exception(message); });
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        using var textures = new OfflineProfessionSceneTextureResources(resources.Characters);
        var lowMemory = new GAction();
        using var manager = new AppearanceAssetManager(weapons.Textures.ManagerConfig, lowMemory.Subscribe);
        var weaponMaterials = new OfflineProfessionWeaponTextureResources(resources.AppearanceConfig, manager, weapons.Textures);
        var old = ProfessionAppearanceTextureBindings.Current;
        ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(resources.Characters, textures, weapons.Textures, weaponMaterials);
        var parts = new Parts(resources.AppearanceConfig, tables.SpineSkinInfoParser);
        using var pool = new CharacterPartEntryProvider(new OfflineProfessionScenePartPoolBindings(weapons, weaponMaterials, parts, () => true));
        using var texturePool = new CharacterPortraitTexturePool(lowMemory);
        try
        {
            int captures = 0;
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                var data = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(resources.AppearanceConfig, sex, (Profession)profession);
                var player = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
                foreach (var pair in data.SkinMap)
                    player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                foreach (var pair in data.DyeMap)
                    player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                        Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                var appearance = new OfflineProfessionAppearanceBindings(17, () => player, _ => player.InitialWeaponType,
                    tables, resources.AppearanceConfig, rules, defaults);
                using var studio = new OfflineProfessionPortraitStudio(resources, appearance, manager, pool, parts);
                var photographer = studio.InstantiateStudio(); photographer.Init(texturePool);
                var request = new CharacterPortraitRequestor();
                // This is the actual serialized animation on the original
                // NpcPanel and dialogue-message TeamHeadCharacterPortrait.
                request.SetAnimationName("home/idle_01");
                request.SetRequestInfo(17, ProfessionPortraitKind.Head, appearance.AppearanceData, (int)sex, profession, false);
                var actor = photographer.GetComponentInChildren<Zjcs.Official.CombatAppearance.AppearanceCharacter>(true);
                studio.CreateBuilder(ProfessionPortraitKind.Head).HandleAppearanceCharacter(actor, request);
                CheckSlotDepth(actor.GetComponent<ProfessionNativeSceneRenderer>());
                var before = OfflineProfessionAppearanceBindings.Clone((AppearanceData)appearance.AppearanceData);
                var portrait = photographer.CapturePortraitTexture(request, out var pixels);
                Require(portrait != null && portrait.Width == 100 && portrait.Height == 120 && pixels?.Length == 48000,
                    "Original Head 100x120 frame captured through the real photographer");
                var color = portrait.Tex.GetPixels32();
                Require(color.Count(c => c.a > 0) > 500 && color.Any(c => c.a == 0), "Actual character and transparent portrait margins");
                Require(!color.Any(c => c.r > 245 && c.g < 10 && c.b > 245 && c.a > 240), "No shader error pixels");
                var source = (AppearanceData)appearance.AppearanceData;
                Require(source.SkinMap.Keys.SequenceEqual(before.SkinMap.Keys)
                    && source.SkinMap.Values.Select(v => v.SkinId).SequenceEqual(before.SkinMap.Values.Select(v => v.SkinId)),
                    "Original exclusion cloning preserves the caller appearance");
                string label = sex + "-" + profession;
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "portrait-" + label + ".png"), portrait.Tex.EncodeToPNG());
                var again = photographer.CapturePortraitTexture(request, out var secondPixels);
                Require(again != null && pixels.SequenceEqual(secondPixels), "Full appearance release and recapture restores exact GPU pixels");
                portrait.DestroyTexture(); again.DestroyTexture();
                captures += 2;
                Require(photographer.GetComponentInChildren<AppearanceRendererPartHandler>(true).LoadedSkeletonDict.Count == 0,
                    "Head excludes the original weapon/back parts");
                Debug.Log("[Original partner player portrait] " + label + ": 100x120, transparent margins, source immutable, exact release/recapture pixels");
                yield return null;
                resources.Characters.Loader.TryUnloadUnusedTextures(true);
                Require(textures.LoadedTextureCount == 0, "Original portrait release unloads every requested body/dye texture");
                var cacheSteps = CheckManagerCache(resources, appearance, player, tables, rules, defaults,
                    manager, pool, parts, lowMemory, pixels);
                while (cacheSteps.MoveNext()) yield return cacheSteps.Current;
            }
            Require(captures == 8, "All four actual sex/profession portraits and recaptures");
        }
        finally { ProfessionAppearanceTextureBindings.Current = old; }
    }

    static IEnumerator CheckManagerCache(ProfessionNativePortraitStudioResources resources,
        OfflineProfessionAppearanceBindings appearance, OfflinePlayerProgress saved,
        ProfessionAppearanceTables tables, AppearanceAnimNameCombineRule rules, ProfessionAppearanceSkinDefaults defaults,
        AppearanceAssetManager materials, CharacterPartEntryProvider partsPool, Parts parts, GAction lowMemory, byte[] expected)
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "portrait-cache");
        var request = new CharacterPortraitRequestor();
        request.SetAnimationName("home/idle_01");
        request.SetRequestInfo(17, ProfessionPortraitKind.Head, appearance.AppearanceData, (int)appearance.Sex, appearance.Profession, true);
        Texture2D ownedTexture;
        long version, hash;
        string file;
        using (var session = new OfflineProfessionPortraitSession(root, resources, appearance, tables, materials, partsPool, parts, lowMemory))
        {
            request.GetDetailRequestData(session.Settings, session.Manager, session.AppearanceReader, out var details);
            version = details.VersionId; hash = details.AppearanceHashCode;
            file = Path.Combine(root, "17", "CharacterPortrait", "17_" + hash);
            Require(!File.Exists(file), "A new appearance has no pre-existing cache file");
            var cache = ((ICharacterPortraitManager)session.Manager).GetCharacterPortrait(request);
            Require(cache.SubcribeTexture(request) == resources.Settings.LoadingTexture.Tex, "Original loading image before the queue runs");
            for (int frame = 0; frame < 30 && !cache.NeedRecycle; frame++) { session.Update(); yield return null; }
            Require(cache.NeedRecycle && cache.AppearanceHashCode == hash, "Original queue captures with the actual studio");
            ownedTexture = cache.ForceGetTexture().Tex;
            Require(ownedTexture.GetRawTextureData<byte>().ToArray().SequenceEqual(expected)
                && File.ReadAllBytes(file).SequenceEqual(expected), "Queued GPU pixels and original persistent bytes match direct original capture");
            Require(ReferenceEquals(cache, ((ICharacterPortraitManager)session.Manager).GetCharacterPortrait(request)), "Original in-memory cache reused");
            cache.Unsubscribe(request); request.StopRequest();
        }
        yield return null;
        Require(!ownedTexture, "Offline session shutdown destroys its active portrait texture");
        var restored = new OfflineProfessionAppearanceBindings(17, () => saved, _ => saved.InitialWeaponType,
            tables, resources.AppearanceConfig, rules, defaults);
        Require(((AppearanceData)restored.AppearanceData).ModifyTimeStamp != ((AppearanceData)appearance.AppearanceData).ModifyTimeStamp,
            "Reopened local appearance has a fresh transient timestamp");
        request.SetRequestInfo(17, ProfessionPortraitKind.Head, restored.AppearanceData, (int)restored.Sex, restored.Profession, true);
        using (var session = new OfflineProfessionPortraitSession(root, resources, restored, tables, materials, partsPool, parts, lowMemory))
        {
            request.GetDetailRequestData(session.Settings, session.Manager, session.AppearanceReader, out var details);
            Require(details.VersionId == version && details.AppearanceHashCode == hash, "Reopened local snapshot has a stable original cache key");
            var cache = ((ICharacterPortraitManager)session.Manager).GetCharacterPortrait(request);
            Require(cache.NeedRecycle && cache.AppearanceHashCode == hash
                && cache.ForceGetTexture().Tex.GetRawTextureData<byte>().ToArray().SequenceEqual(expected),
                "Original disk cache restores exact pixels before any manager Update");
            ownedTexture = cache.ForceGetTexture().Tex;
            cache.SubcribeTexture(request); cache.Unsubscribe(request); request.StopRequest();
            var data = OfflineProfessionAppearanceBindings.Clone((AppearanceData)restored.AppearanceData);
            data.DyeMap.First().Value.Hsv = new Hsv(12, 34, 56);
            Require(session.AppearanceReader.GetModifyTimeStamp(data) != version, "Local cache version includes dye channels");
            data = OfflineProfessionAppearanceBindings.Clone((AppearanceData)restored.AppearanceData);
            data.EquipSkinDict[EquipPos.Weapon] = 123;
            Require(session.AppearanceReader.GetModifyTimeStamp(data) != version, "Local cache version includes equipment appearances");
            var pending = new CharacterPortraitRequestor(); pending.SetAnimationName("home/idle_01");
            pending.SetRequestInfo(99, ProfessionPortraitKind.Head, restored.AppearanceData, (int)restored.Sex, restored.Profession, true);
            var canceled = ((ICharacterPortraitManager)session.Manager).GetCharacterPortrait(pending);
            pending.StopRequest();
            session.Update(); yield return null;
            Require(!canceled.NeedRecycle && !File.Exists(Path.Combine(root, "17", "CharacterPortrait", "99_" + hash)),
                "Canceled queue request does not capture or write cache");
        }
        yield return null;
        Require(!ownedTexture, "Restored cache texture released with the offline session");
        Debug.Log("[Original partner portrait cache] " + appearance.Sex + "-" + appearance.Profession
            + ": real queue capture, in-memory reuse, disk reopen, stable appearance key, cancellation and texture cleanup");
    }
    static void CheckSlotDepth(ProfessionNativeSceneRenderer renderer)
    {
        Require(renderer.ZSpacing == -.0002f, "The actual original studio slot spacing is bound");
        var state = renderer.State;
        var slots = state.CreateDrawSlots(Enumerable.Range(0, state.SlotCount).ToArray());
        using var zero = new ProfessionNativeMeshBatches(state.SlotCount);
        using var depth = new ProfessionNativeMeshBatches(state.SlotCount);
        zero.Build(state.Pose, slots, true, true);
        depth.Build(state.Pose, slots, true, true, renderer.ZSpacing);
        Require(zero.Batches.Count == depth.Batches.Count && depth.Batches.Count > 0, "Original drawable source-slot batch identities");
        for (int i = 0; i < depth.Batches.Count; i++)
        {
            var a = zero.Batches[i]; var b = depth.Batches[i];
            Require(a.FirstDrawIndex == a.LastDrawIndex && a.FirstDrawIndex == b.FirstDrawIndex
                && a.Page == b.Page && a.Mesh.triangles.SequenceEqual(b.Mesh.triangles)
                && a.Mesh.uv.SequenceEqual(b.Mesh.uv) && a.Mesh.colors32.SequenceEqual(b.Mesh.colors32),
                "Nonzero studio spacing preserves original clipped triangles, UV, PMA colors and atlas pages");
            var before = a.Mesh.vertices; var after = b.Mesh.vertices;
            float expectedDepth = b.FirstDrawIndex * renderer.ZSpacing;
            if (!(before.Length == after.Length && before.Select(p => new Vector2(p.x, p.y)).SequenceEqual(after.Select(p => new Vector2(p.x, p.y)))
                && before.All(p => p.z == 0) && after.All(p => p.z == expectedDepth)))
                Debug.Log("[Original slot depth diagnostic] draw=" + b.FirstDrawIndex + " vertices=" + before.Length + "/" + after.Length
                    + " expected=" + expectedDepth.ToString("R") + " actual=" + string.Join(",", after.Select(p => p.z.ToString("R")).Distinct())
                    + " zero=" + string.Join(",", before.Select(p => p.z.ToString("R")).Distinct()));
            Require(before.Length == after.Length && before.Select(p => new Vector2(p.x, p.y)).SequenceEqual(after.Select(p => new Vector2(p.x, p.y)))
                && before.All(p => p.z == 0) && after.All(p => p.z == expectedDepth),
                "Each real source slot retains XY geometry with its original draw-order depth");
        }
    }
    sealed class Parts : IProfessionAppearancePartBindings
    {
        public AppearanceConfig Config { get; }
        readonly SpineSkinInfoParser parser;
        public Parts(AppearanceConfig config, SpineSkinInfoParser parser) { Config = config; this.parser = parser; }
        public bool HasEffect(string skin) => parser.HasEffect(skin);
        public IProfessionAppearancePartAsset LoadPrefab(string path)
            => throw new NotSupportedException("This default-head fixture requires no effect prefab; actual request: " + path);
        public GameObject Instantiate(GameObject prefab, Transform parent, bool world)
            => Object.Instantiate(prefab, parent, world);
    }
}
