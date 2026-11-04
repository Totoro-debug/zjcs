using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;
using SceneCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        private const string Key = "Zjcs.OriginalGamePlayerClip";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            SessionState.SetBool(Key + ".physics", false);
            SessionState.SetBool(Key + ".high-textures", false);
            SessionState.SetBool(Key + ".weapon-physics", false);
            SessionState.SetBool(Key + ".weapon-bones", false);
            SessionState.SetBool(Key + ".weapon-low", false);
            SessionState.SetBool(Key + ".weapon-prefabs", false);
            SessionState.SetBool(Key + ".weapon-host-motion", false);
            SessionState.SetBool(Key + ".player-live-physics", false);
            SessionState.SetBool(Key + ".player-appearance-bindings", false);
            SessionState.SetBool(Key + ".player-actor", false);
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")))
                throw new InvalidOperationException("Use a repository validation workspace.");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 480);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original Game prefab clip verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += (_, __, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            new GameObject("Original Game prefab clipping verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            try
            {
                var prefab = Resources.Load<GameObject>("Combat/SceneAppearance/PlayerModel_Game");
                Require(prefab && prefab.transform.childCount == 2 && prefab.GetComponent<Animator>().runtimeAnimatorController.name == "character_Female_1_Controller",
                    "original three nodes and exact empty base controller");
                Require(prefab.GetComponentsInChildren<Transform>(true).Count() == 3
                    && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c), "complete hierarchy with no missing scripts");
                var serializedClip = new SerializedObject(prefab.transform.Find("Dimianzhezhao").GetComponent<SkeletonRendererWeaponClipSet>());
                Require(serializedClip.FindProperty("mAppearanceRenderer").objectReferenceValue == prefab.GetComponent<ProfessionNativeSceneAppearance>(),
                    "original weapon clip appearance reference survives native script binding");
                var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
                var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
                var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                var resolver = new ProfessionAppearanceResolver(ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text));
                var weaponTextures = weapons.Textures;
                bool useHighPipeline = SessionState.GetBool(Key + ".high-textures", false)
                    || SessionState.GetBool(Key + ".weapon-bones", false) || SessionState.GetBool(Key + ".weapon-prefabs", false);
                // Every original Game body/weapon uses this pipeline, including
                // the ordinary clipping and live-physics probes in a fresh project.
                using var pipeline = Resources.Load<Zjcs.Presentation.Combat.OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets").Enter();
                {
                    // An empty scene has no camera and does not initialize its
                    // render pipeline. Render one actual camera frame first.
                    var warmup = new GameObject("Original High weapon pipeline warmup camera");
                    var warmupCamera = warmup.AddComponent<Camera>();
                    var warmupTarget = RenderTexture.GetTemporary(16, 16, 24);
                    warmupCamera.targetTexture = warmupTarget;
                    try
                    {
                        RenderPipeline.SubmitRenderRequest(warmupCamera, new RenderPipeline.StandardRequest { destination = warmupTarget });
                        yield return null;
                    }
                    finally
                    {
                        warmupCamera.enabled = false; warmupCamera.targetTexture = null;
                        Object.Destroy(warmup); RenderTexture.ReleaseTemporary(warmupTarget);
                    }
                }
                if (useHighPipeline)
                {
                    if (SessionState.GetBool(Key + ".high-textures", false)) CheckHighTextures();
                    if (SessionState.GetBool(Key + ".weapon-low", false)) CheckHighTextures(low: true);
                    if (SessionState.GetBool(Key + ".weapon-physics", false)) CheckWeaponPhysics();
                    if (SessionState.GetBool(Key + ".weapon-bones", false)) yield return CheckWeaponBoneOverrides();
                    weaponTextures = Resources.Load<ProfessionNativeWeaponTextureResources>("Combat/Weapons/texture-resources");
                }
                using var textures = new OfflineProfessionSceneTextureResources(catalog);
                using var manager = new AppearanceAssetManager(weaponTextures.ManagerConfig, _ => new Subscription());
                var materialBindings = new OfflineProfessionWeaponTextureResources(config, manager, weaponTextures);
                var previous = ProfessionAppearanceTextureBindings.Current;
                ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(catalog, textures, weaponTextures, materialBindings);
                try
                {
                    if (SessionState.GetBool(Key + ".player-actor", false))
                    {
                        var actorCheck = CheckPlayerActors(prefab, catalog, weapons, manager, materialBindings, config, resolver);
                        try { while (actorCheck.MoveNext()) yield return actorCheck.Current; }
                        finally { (actorCheck as IDisposable)?.Dispose(); }
                    }
                    if (SessionState.GetBool(Key + ".player-appearance-bindings", false))
                        yield return CheckPlayerAppearanceBindings(prefab, catalog, weapons, manager, materialBindings, config, resolver);
                    foreach (var model in catalog.Models)
                    foreach (int profession in new[] { 1, 10 })
                    {
                        string group = profession == 1 ? "100" : "200";
                        var staging = new GameObject("Inactive original prefab activation staging"); staging.SetActive(false);
                        var host = new GameObject("Original character sorting parent"); host.transform.SetParent(staging.transform, false);
                        host.AddComponent<SortingGroup>().sortingLayerID = 1182434123;
                        var root = Object.Instantiate(prefab, host.transform, false);
                        root.transform.localScale = Vector3.one * .3f;
                        root.transform.localPosition = Vector3.up * .37f;
                        var native = root.GetComponent<ProfessionNativeSceneRenderer>(); native.Configure(catalog);
                        var character = root.GetComponent<SceneCharacter>();
                        var animation = root.GetComponent<ProfessionNativeSceneAppearance>();
                        var handler = root.GetComponent<AppearanceRendererPartHandler>();
                        var bindings = new OfflineProfessionSceneAppearanceBindings(catalog, native, manager, config);
                        var parts = new PartBindings(config); var poses = new WeaponPoses();
                        var resourceBindings = new OfflineProfessionScenePartPoolBindings(weapons, materialBindings, parts, poses, () => false);
                        using var pool = new CharacterPartEntryProvider(resourceBindings);
                        handler.ConfigureParts(parts); handler.ConfigureRenderer(native, root.transform.Find("PartSlot"), pool, "Object", 0);
                        character.Configure(bindings, handler);
                        animation.Configure(bindings, handler, state =>
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(state,
                                new ProfessionNativeAnimationChannels(state.Source, group + "/idle_01", originalBezierSegments: true), .4f, false));
                        var sex = model.Sex == "male" ? Sex.Male : Sex.Female;
                        var data = resolver.BuildAppearanceRuntimeData(sex, (Profession)profession,
                            resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession), config.GetAppendSkins(AppendSkinType.Game));
                        data.SkinMap[SkinType.pWeapon] = "pWeapon_" + group + "_G_007";
                        try
                        {
                            character.SetRuntimeData(data);
                            var previousClip = ProfessionAppearanceSkinClipBindings.Current;
                            ProfessionAppearanceSkinClipBindings.Current = new OfflineProfessionSceneSkinClipBindings();
                            try { host.transform.SetParent(null, false); }
                            finally { ProfessionAppearanceSkinClipBindings.Current = previousClip; }
                            var ground = root.transform.Find("Dimianzhezhao");
                            var clip = ground.GetComponent<SkeletonClipAttachment>();
                            var setter = ground.GetComponent<SkeletonRendererWeaponClipSet>();
                            Require(clip.SkeletonGo == root && clip.SlotName == "ZZshadow/dimianzhezhao" && clip.AttachmentName == "dimianzhezhao",
                                "original ground clip references");
                            var part = handler.LoadedSkeletonDict[SkinType.pWeapon].Part;
                            var weapon = part.SkeletonTarget.GetComponent<ProfessionNativeSceneWeaponRenderer>();
                            poses.Apply(weapon.State); native.LateUpdateMesh(); weapon.UpdateAnimationAndMesh();
                            var slot = ((IProfessionFollowerSkeleton)native.State).FindSlot(clip.SlotName);
                            var attachment = ((IProfessionFollowerSkeleton)native.State).GetAttachment(slot.Index, clip.AttachmentName);
                            Require(attachment is ProfessionNativeClippingAttachment && slot.Bone.Active,
                                "real selected skeleton ground clipping attachment and active bone");
                            var material = part.SkeletonTarget.GetComponent<AppearanceRendererLite>().GetIndependentMaterials()[0];
                            var original = weapons.Find(data.SkinMap[SkinType.pWeapon]).OriginalMaterial;
                            float originalHeight = original.GetFloat(AppearanceMaterialUtils.sWorldClipHeight);
                            Require(material.HasProperty(AppearanceMaterialUtils.sWorldClipHeight), "actual original shader world clipping uniform");
                            var psGo = new GameObject("Clip particle property-block diagnostic"); psGo.transform.SetParent(part.GameObject.transform, false);
                            var ps = psGo.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            var particleMain = ps.main; particleMain.playOnAwake = false;
                            var particle = psGo.GetComponent<ParticleSystemRenderer>();
                            // Original disabled initialization has not cached a weapon material.
                            // First enable must discover both actual weapon and its active particles.
                            native.State.SetSlotAttachment(slot.Index, null); yield return null;
                            var bounds = weapon.GetComponent<MeshRenderer>().bounds;
                            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                            Capture(bounds, model.Sex + "-" + group + "-visibility-demand");
                            yield return null;
                            weapon.UpdateAnimationAndMesh();
                            Require(material != original && material.mainTexture && !material.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal),
                                "real independent High material after actual visibility demand: " + material.name + "/" + material.mainTexture?.name);
                            if (SessionState.GetBool(Key + ".physics", false)) CheckMixedPhysics(native, weapon, group);
                            if (SessionState.GetBool(Key + ".player-live-physics", false))
                                yield return CheckLivePlayerPhysics(native, weapon, group);
                            native.State.SetSlotAttachment(slot.Index, attachment); yield return null;
                            Require(Mathf.Abs(material.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) - ground.position.y) < .0001f,
                                "actual attachment identity enables world-height clipping");
                            var block = new MaterialPropertyBlock(); particle.GetPropertyBlock(block);
                            Require(Mathf.Abs(block.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) - ground.position.y) < .0001f,
                                "particle property block follows original weapon clip");
                            // Isolate shader pixels from the body clip polygon for this comparison.
                            native.State.SetSlotAttachment(slot.Index, null); yield return null;
                            float disabled = material.GetFloat(AppearanceMaterialUtils.sWorldClipHeight);
                            Require(disabled < -1e30f, "detaching ground clip removes weapon clip");
                            var before = Capture(bounds, model.Sex + "-" + group + "-unclipped");
                            float cut = weapon.GetComponent<MeshRenderer>().bounds.center.y;
                            ground.position = new Vector3(ground.position.x, cut, ground.position.z);
                            setter.SetClipHeight(0);
                            var after = Capture(bounds, model.Sex + "-" + group + "-clipped");
                            Require(!before.SequenceEqual(after), "real GPU pixels change when the weapon intersects the original clipping plane");
                            root.transform.position += Vector3.up * .75f; setter.SetClipHeight(0);
                            Require(Mathf.Abs(material.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) - ground.position.y) < .0001f,
                                "world plane uses current character transform");
                            particle.GetPropertyBlock(block);
                            Require(Mathf.Abs(block.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) - ground.position.y) < .0001f,
                                "particle plane also updates after nonzero world movement");
                            setter.SetClipHeight(float.MinValue);
                            Require(material.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) < -1e30f, "manual original disable sentinel");
                            Require(original.GetFloat(AppearanceMaterialUtils.sWorldClipHeight) == originalHeight,
                                "original shared material remains separate");
                            // No-weapon branch returns quietly; cached lite material is not destroyed by this component.
                            var noWeapon = Object.Instantiate(prefab, staging.transform, false);
                            noWeapon.transform.Find("Dimianzhezhao").GetComponent<SkeletonRendererWeaponClipSet>().SetClipHeight(0);
                            Object.Destroy(noWeapon);
                            if (SessionState.GetBool(Key + ".weapon-prefabs", false) && model.Sex == "female")
                                yield return CheckNativeWeaponPrefabs(native, handler, parts, group);
                            Object.Destroy(setter); yield return null;
                            // The complete-prefab fixture replaces and releases
                            // this weapon; the ordinary clipping probe retains it.
                            if (!SessionState.GetBool(Key + ".weapon-prefabs", false) || model.Sex != "female")
                                Require(material, "lite-owned material survives clipping-component destruction");
                        }
                        finally
                        {
                            host.SetActive(false); character.Release(true); pool.ClearPool(); native.Release(); Object.Destroy(host); Object.Destroy(staging);
                        }
                        yield return null;
                        Require(resourceBindings.LiveInstanceCount == 0, "actual part resources released");
                    }
                }
                finally { ProfessionAppearanceTextureBindings.Current = previous; }
                Require(errors == 0, "Unity reported runtime errors");
                Debug.Log("Original complete Game prefab and clipping passed: three source nodes, original application references/empty controller, both sexes and warrior/mage body with actual independent sword/staff, real Low/High textures, attachment enable/disable, current world transform, particle block, GPU clipping pixels, no-weapon and lite-material ownership. Explicit pre-physics idle diagnostic; production character factory, mixed physics motion and Windows Player remain separate.");
                SessionState.SetInt(Key + ".result", 0);
            }
            finally { EditorApplication.isPlaying = false; }
        }
        private static byte[] Capture(Bounds bounds, string name)
        {
            var go = new GameObject("Original prefab clipping camera"); var camera = go.AddComponent<Camera>();
            camera.aspect = .75f; camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .75f) * 1.4f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var rt = new RenderTexture(384, 512, 24); var texture = new Texture2D(384, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                if (GraphicsSettings.currentRenderPipeline)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                else { camera.targetTexture = rt; camera.Render(); }
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 384, 512), 0, 0); texture.Apply();
                Require(texture.GetPixels32().Count(p => p.r > 16 || p.g > 16 || p.b > 16) > 100, "visible actual GPU output");
                if (SessionState.GetBool(Key + ".player-live-physics", false))
                    Require(!texture.GetPixels32().Any(p => p.r > 240 && p.g < 20 && p.b > 240),
                        "live body/clipping probe has no error-shader diagnostic particles");
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), bytes);
                return bytes;
            }
            finally { RenderTexture.active = previous; Object.Destroy(go); Object.Destroy(rt); Object.Destroy(texture); }
        }
        private sealed class PartBindings : IProfessionAppearancePartBindings
        {
            public AppearanceConfig Config { get; }
            internal PartBindings(AppearanceConfig config) { Config = config; }
            public bool HasEffect(string name) => false;
            public IProfessionAppearancePartAsset LoadPrefab(string path) => throw new NotSupportedException(path);
            public GameObject Instantiate(GameObject prefab, Transform parent, bool world) => throw new NotSupportedException();
        }
        private sealed class WeaponPoses : IProfessionNativeSceneWeaponPoseProducer
        {
            private readonly Dictionary<ProfessionNativeAppearanceSkeleton, string> requests = new Dictionary<ProfessionNativeAppearanceSkeleton, string>();
            public void Play(ProfessionNativeAppearanceSkeleton state, string animation, bool loop) { requests[state] = animation; }
            public void Apply(ProfessionNativeAppearanceSkeleton state) => state.EvaluateUnmixedWeaponAnimation(requests[state], .4f);
        }
        private sealed class Subscription : IDisposable { public void Dispose() { } }
        private sealed class Runner : MonoBehaviour { }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original Game prefab clip: " + message); }
    }
}
