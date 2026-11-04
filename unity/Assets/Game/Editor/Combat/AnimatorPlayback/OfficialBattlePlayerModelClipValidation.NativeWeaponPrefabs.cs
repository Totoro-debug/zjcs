using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        [Serializable] private sealed class NativeWeaponNode
        {
            public string name; public int parent, layer; public bool active;
            public Vector3 position, scale; public Quaternion rotation;
        }
        [Serializable] private sealed class NativeWeaponCase
        {
            public string name, initial_skin, initial_animation; public NativeWeaponNode[] nodes;
            public float z_spacing;
            public int particles, particle_renderers, sprites, followers, rotations;
        }
        [Serializable] private sealed class NativeWeaponCases { public NativeWeaponCase[] cases; }

        public static void RunNativeWeaponPrefabs()
        {
            Run(); SessionState.SetBool(Key + ".weapon-prefabs", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 1800);
        }

        private static IEnumerator CheckNativeWeaponPrefabs(ProfessionNativeSceneRenderer body,
            AppearanceRendererPartHandler handler, PartBindings parts, string group)
        {
            var work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<NativeWeaponCases>(File.ReadAllText(Path.Combine(work, "weapon-native-prefab-cases.json")));
            var catalog = Resources.Load<ProfessionNativeSceneWeaponCatalog>("Combat/Weapons/scene-prefabs");
            Require(catalog && fixture.cases.Length == 390, "complete native weapon catalog and source inventory");
            handler.SetSkins(new Dictionary<SkinType, string>());
            using var manager = new AppearanceAssetManager(catalog.Textures.ManagerConfig, _ => new Subscription());
            var materials = new OfflineProfessionWeaponTextureResources(parts.Config, manager, catalog.Textures);
            var previous = ProfessionAppearanceTextureBindings.Current;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ProfessionAppearanceTextureBindings.Current = materials; ShaderUtil.allowAsyncCompilation = false;
            var cameraGo = new GameObject("Complete native weapon playback camera");
            var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = .75f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(384, 512, 24); target.Create(); camera.targetTexture = target;
            int count = 0, particles = 0, sprites = 0, visibleEffects = 0;
            try
            {
                foreach (var row in fixture.cases.Where(c => c.name.StartsWith("pWeapon_" + group + "_", StringComparison.Ordinal)))
                {
                    var entry = catalog.Find(row.name);
                    Require(entry.Prefab && entry.InitialSkin == row.initial_skin && entry.InitialAnimation == row.initial_animation,
                        "exact original native entry: " + row.name);
                    CheckNativeWeaponHierarchy(entry.Prefab, row);
                    var poolBindings = new OfflineProfessionScenePartPoolBindings(catalog, materials, parts, () => true);
                    using (var pool = new CharacterPartEntryProvider(poolBindings))
                    {
                        handler.ConfigureRenderer(body, body.transform.Find("PartSlot"), pool, "Object", 0);
                        try
                        {
                            handler.SetSkins(new Dictionary<SkinType, string> { [SkinType.pWeapon] = row.name });
                            var part = handler.LoadedSkeletonDict[SkinType.pWeapon].Part;
                            var root = part.GameObject;
                            var renderer = part.SkeletonTarget.GetComponent<ProfessionNativeSceneWeaponRenderer>();
                            Require(renderer && !string.IsNullOrEmpty(part.SkeletonFollower.SlotName), "native weapon part assembly: " + row.name);
                            Require(renderer.ZSpacing == row.z_spacing, "original per-slot weapon depth: " + row.name);
                            Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(c => c), "no missing native application scripts: " + row.name);
                            var originalFollowers = entry.Prefab.GetComponentsInChildren<SkeletonRendererFollower>(true);
                            var followers = root.GetComponentsInChildren<SkeletonRendererFollower>(true);
                            for (int i = 0; i < followers.Length; i++)
                                Require(followers[i] == part.SkeletonFollower
                                    ? followers[i].SkeletonRenderer == body
                                    : (bool)followers[i].SkeletonRenderer == (bool)originalFollowers[i].SkeletonRenderer,
                                    "preserved native follower/null target: " + row.name);
                            // Render actual frames so original visibility demand, MonoBehaviour
                            // updates, native spring clocks and particle simulation all run.
                            var bounds = NativeWeaponBounds(body.gameObject);
                            camera.transform.position = bounds.center + Vector3.back * 10;
                            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.4f;
                            for (int frame = 0; frame < 8; frame++) yield return null;
                            Require(NativeWeaponClock(renderer) > 0, "native weapon physical clock advances: " + row.name);
                            CheckNativeWeaponPose(renderer, row.name);
                            if (row.z_spacing != 0)
                                Require(renderer.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh)
                                    .SelectMany(f => f.sharedMesh.vertices).Any(v => Mathf.Abs(v.z) > .00001f),
                                    "original nonzero slot depth reaches actual native mesh: " + row.name);
                            bounds = NativeWeaponBounds(body.gameObject);
                            var visible = Capture(bounds, "native-weapon-" + row.name);
                            if (row.particles > 0 || row.sprites > 0)
                            {
                                var effects = root.GetComponentsInChildren<Renderer>(true)
                                    .Where(r => r is ParticleSystemRenderer || r is SpriteRenderer).ToArray();
                                var enabled = effects.Select(r => r.enabled).ToArray();
                                byte[] hidden;
                                try
                                {
                                    foreach (var effect in effects) effect.enabled = false;
                                    hidden = Capture(bounds, "native-effect-hidden-" + row.name);
                                }
                                finally { for (int i = 0; i < effects.Length; i++) effects[i].enabled = enabled[i]; }
                                if (!visible.SequenceEqual(hidden)) visibleEffects++;
                            }
                            handler.SetSkins(new Dictionary<SkinType, string>());
                            Require(!root.activeInHierarchy && poolBindings.LiveInstanceCount == 1, "native part returned to pool: " + row.name);
                            handler.SetSkins(new Dictionary<SkinType, string> { [SkinType.pWeapon] = row.name });
                            Require(handler.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == root, "native part reused from pool: " + row.name);
                            yield return null; yield return null;
                            CheckNativeWeaponPose(renderer, row.name);
                            count++; particles += row.particles; sprites += row.sprites;
                        }
                        finally { handler.Release(true); pool.ClearPool(); }
                    }
                    yield return null;
                    Require(poolBindings.LiveInstanceCount == 0, "native part pool disposed: " + row.name);
                    if (count % 25 == 0) Debug.Log("[Complete native weapons] " + group + " / " + count);
                }
                Require(count == (group == "100" ? 197 : 193) && visibleEffects > 0, "all original profession weapon prefabs and visible effect pixels tested");
                Debug.Log("[Complete native weapons] passed group=" + group + " prefabs=" + count + " particles=" + particles + " sprites=" + sprites + " visibleEffects=" + visibleEffects);
            }
            finally
            {
                // Original preview directly loads its shared Low references. Release
                // those fixture-owned requests only after all instances are destroyed.
                var low = (YooAssetTextureLoader)catalog.Textures.LoadPath("Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_Low.asset");
                foreach (var mapping in low.placeholderMap)
                {
                    mapping.targetTextureReference.AssetReference.Unload();
                    mapping.dyeTextureReference.AssetReference.Unload();
                }
                camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(cameraGo);
                ProfessionAppearanceTextureBindings.Current = previous; ShaderUtil.allowAsyncCompilation = previousAsync;
            }
            Require(materials.LoadedResourceTextureCount == 0, "complete native fixture released its texture requests");
        }

        private static void CheckNativeWeaponHierarchy(GameObject prefab, NativeWeaponCase row)
        {
            var nodes = prefab.GetComponentsInChildren<Transform>(true);
            Require(nodes.Length == row.nodes.Length, "complete original hierarchy size: " + row.name);
            for (int i = 0; i < nodes.Length; i++)
            {
                var actual = nodes[i]; var expected = row.nodes[i];
                Require(actual.name == expected.name && actual.gameObject.layer == expected.layer
                    && actual.gameObject.activeSelf == expected.active
                    && (i == 0 || actual.parent == nodes[expected.parent])
                    && Vector3.Distance(actual.localPosition, expected.position) < .00001f
                    && Quaternion.Angle(actual.localRotation, expected.rotation) < .01f
                    && Vector3.Distance(actual.localScale, expected.scale) < .00001f,
                    "original local transform/flags: " + row.name + "/" + i);
            }
            Require(prefab.GetComponentsInChildren<ParticleSystem>(true).Length == row.particles
                && prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Length == row.particle_renderers
                && prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == row.sprites
                && prefab.GetComponentsInChildren<SkeletonRendererFollower>(true).Length == row.followers
                && prefab.GetComponentsInChildren<SkeletonBoneRotOverride>(true).Length == row.rotations,
                "complete original component inventory: " + row.name);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials.Where(m => m))
                    Require(material.shader && material.shader.isSupported && material.passCount > 0,
                        "native original renderer shader: " + row.name + "/" + material.name);
        }

        private static Bounds NativeWeaponBounds(GameObject root)
        {
            // Native particle culling bounds can intentionally be enormous.
            // Keep a useful diagnostic view around actual character/weapon geometry.
            var renderers = root.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)
                && r.enabled && r.bounds.size.sqrMagnitude > 0).ToArray();
            Require(renderers.Length > 0, "visible native weapon/body renderers");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void CheckNativeWeaponPose(ProfessionNativeSceneWeaponRenderer renderer, string name)
        {
            var before = WeaponMatrices(renderer.State);
            float clock = NativeWeaponClock(renderer);
            renderer.UpdateAnimationAndMesh(); renderer.UpdateAnimationAndMesh();
            Require(NativeWeaponClock(renderer) == clock && WeaponMatrixDelta(before, WeaponMatrices(renderer.State)) < .0001f,
                "native redraw retains completed physical pose: " + name);
        }

        private static float NativeWeaponClock(ProfessionNativeSceneWeaponRenderer renderer)
            => ((ProfessionNativeSceneWeaponPlayback)typeof(ProfessionNativeSceneWeaponRenderer)
                .GetField("playback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer)).PhysicsTime;
    }
}
