using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class SceneWeaponProof { public SceneShader[] current_shader_conversions; }
        [Serializable] private sealed class SceneShader { public SceneProgram[] programs; }
        [Serializable] private sealed class SceneProgram { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class SceneHierarchy { public SceneNode[] nodes; }
        [Serializable] private sealed class SceneNode { public string parent, name; public SceneGo game_object; public SceneComponent[] components; }
        [Serializable] private sealed class SceneGo { public SceneId source; public SceneGoFields fields; }
        [Serializable] private sealed class SceneId { public string path_id; }
        [Serializable] private sealed class SceneGoFields { public int m_Layer; public bool m_IsActive; }
        [Serializable] private sealed class SceneComponent { public string type; public SceneFields runtime_fields; }
        [Serializable] private sealed class ScenePointer { public long m_PathID; }
        [Serializable] private sealed class SceneFields
        {
            public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation;
            public string BoneName, SlotName, AttachmentName, initialSkinName, _animationName;
            public bool FollowZSpacing, ZFront, FollowLocalScale, FollowBoneWorldFlip, FollowRotation,
                FollowSkeletonTransformFlipX, InitializeOnAwake, loop, useClipping, pmaVertexColors;
            public float zSpacing, timeScale; public int updateWhenInvisible, m_SortingLayerID, m_SortingOrder, LevelOfResolution;
            public ScenePointer[] Gos, RenderExistingMeshes;
        }
        private sealed class SceneSubscription : IDisposable { public void Dispose() { } }

        public static void RunSceneWeapons()
        {
            Run(); SessionState.SetBool(Key + ".sceneWeapons", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneWeaponSteps()
        {
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var scene = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var characters = Resources.Load<ProfessionNativeCharacterCatalog>("ProfessionUI/character-resources");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            Require(scene && config && rules && characters && textures, "original scene weapon dependencies");
            var proof = JsonUtility.FromJson<SceneWeaponProof>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/battle-animation/scene-weapon-source-manifest.json")));
            var shader = scene.Find("pWeapon_100_G_007").OriginalMaterial.shader;
            int stages = 0; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var program in proof.current_shader_conversions.Single().programs)
                {
                    foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        var compiled = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(program.pass_index)
                            .CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                        Require(compiled.Success, "current original scene weapon D3D stage " + stages); stages++;
                    }
                    if (stages % 128 == 0) Debug.Log("[Original scene weapon shader] compiled D3D stages=" + stages + "/1088");
                    if (stages % 64 == 0) yield return null;
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = oldAsync; }
            Require(stages == 1088 && !ShaderUtil.ShaderHasError(shader), "all 544 original scene weapon programs");
            int frames = 0, visible = 0, images = 0;
            var oldTextureBindings = ProfessionAppearanceTextureBindings.Current;
            using (var manager = new AppearanceAssetManager(scene.Textures.ManagerConfig, _ => new SceneSubscription()))
            {
                var bindings = new OfflineProfessionWeaponTextureResources(config, manager, scene.Textures);
                ProfessionAppearanceTextureBindings.Current = bindings;
                try
                {
                    foreach (Sex sex in new[] { Sex.Male, Sex.Female })
                    foreach (int profession in new[] { 1, 10 })
                    {
                        var root = new GameObject("Original complete core plus scene weapon diagnostic");
                        try
                        {
                            var resources = new OfflineProfessionCharacterResources(config, characters, (_, __) => { }, (_, __) => { });
                            var core = root.AddComponent<CorePoseEndpoint>(); core.Textures = textures;
                            core.Configure(new CorePoseBindings(core), resources);
                            var character = root.AddComponent<AppearanceCharacter>(); character.Configure(resources);
                            var defaults = new ProfessionAppearanceResolver(tables).BuildDefaultAppearanceData(config, sex, (Profession)profession);
                            var player = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                            player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
                            foreach (var pair in defaults.SkinMap) player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                            foreach (var pair in defaults.DyeMap) player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                                Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                            var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => player.InitialWeaponType, tables, config, rules);
                            ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, sex, profession, graph.AppearanceData, AppendSkinType.Game);
                            string prefix = graph.GetProfessionIdStr(profession);
                            var coreRenderer = root.AddComponent<SceneCoreFollower>(); coreRenderer.State = core.State;
                            var entry = scene.Find("pWeapon_" + prefix + "_G_007");
                            var weapon = CreateSceneWeapon(entry, scene, bindings, root.transform, out var part, out var follower);
                            part.ConfigurePart(new ScenePartBindings(config)); part.Init(root);
                            var page = scene.Weapon.FindPage("pWeapon_" + prefix + "_007");
                            Require(weapon.CustomMaterialOverride[entry.OriginalMaterial].mainTexture == page.Main
                                && entry.OriginalMaterial.mainTexture == page.Placeholder, "original scene shared material loads high texture without changing source");
                            var slot = ((IProfessionFollowerSkeleton)core.State).FindSlot(follower.SlotName);
                            Require(slot != null && slot.Index == core.State.Source.FindSlot("W_jian_D/W_jian_D"), "original weapon target slot identity");
                            var actions = core.State.Source.AnimationNames.Where(name => name.StartsWith(prefix + "/", StringComparison.Ordinal)).ToArray();
                            int bodyImages = 0;
                            using (var coreBatches = new ProfessionNativeMeshBatches(core.State.SlotCount))
                            using (var weaponBatches = new ProfessionNativeMeshBatches(weapon.State.SlotCount))
                            {
                                foreach (string name in actions)
                                {
                                    var channels = new ProfessionNativeAnimationChannels(core.State.Source, name);
                                    foreach (float time in new[] { 0f, .4f, 1.1f })
                                    {
                                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(core.State, channels, time, false);
                                        part.TryPlayAnim(name); weapon.Sample(time); follower.ForceUpdate();
                                        foreach (var effectFollower in weapon.GetComponentsInChildren<BaseSkeletonFollower>()) effectFollower.ForceUpdate();
                                        bool expected = slot.Attachment != null && ReferenceEquals(slot.Attachment,
                                            ((IProfessionFollowerSkeleton)core.State).GetAttachment(slot.Index, follower.AttachmentName));
                                        Require(follower.Gos.Single().activeSelf == expected, "original attachment identity gates weapon visibility");
                                        if (expected && slot.Bone.Active)
                                        {
                                            Vector3 anchor = follower.transform.position;
                                            Require(Vector2.Distance(anchor, root.transform.TransformPoint(new Vector3(slot.Bone.WorldX, slot.Bone.WorldY, 0))) < .0001f,
                                                "original follower bone position in native source scale"); visible++;
                                        }
                                        coreBatches.Build(core.State.Pose, core.State.CreateDrawSlots(new[] { slot.Index }));
                                        weaponBatches.Build(weapon.State.Pose, weapon.State.CreateDrawSlots(), weapon.UseClipping, weapon.PmaVertexColors);
                                        foreach (var batch in weaponBatches.Batches)
                                            foreach (Vector3 vertex in batch.Mesh.vertices) Require(IsFinite(vertex.x) && IsFinite(vertex.y), "finite scene weapon vertices");
                                        frames++;
                                        if (expected && time == .4f && bodyImages < 2)
                                        { CaptureSceneWeapon(folder, sex + "-" + name.Replace('/', '-'), core, coreBatches, weapon, weaponBatches, slot.Index); images++; bodyImages++; }
                                    }
                                    if (frames % 90 == 0) yield return null;
                                }
                            }
                            // Original shared -> independent branch, demand loading, then real destruction.
                            var lite = weapon.GetComponent<AppearanceRendererLite>();
                            var independent = lite.GetIndependentMaterials();
                            weapon.Demand();
                            Require(independent.Length == 1 && independent[0] != entry.OriginalMaterial
                                && independent[0].mainTexture == page.Main, "original independent scene material and demand texture load");
                            Require(ReferenceEquals(independent, lite.GetIndependentMaterials()), "original independent initialization is idempotent");
                            lite.MarkUnloadTextureImmediately();
                            character.Release();
                            Debug.Log("[Original scene weapon] " + sex + "/" + prefix + ": " + actions.Length + " original actions; native four-node source hierarchy, high texture, original attachment/rotation/scale/visibility, pre-physics composition.");
                        }
                        finally { Object.DestroyImmediate(root); }
                        yield return null;
                    }
                    Require(frames == 1305 && visible > 0 && images == 8, "complete original core/scene weapon diagnostic coverage");
                    var data = (AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.MaterialOnDemandData[])
                        typeof(AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>).GetField("loadedDataAtMaterial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scene.Textures.Loader);
                    Require(data.All(row => row.referenceCounter == 0 && !row.textureRequest.WasRequested), "all original scene texture requests released");
                }
                finally { ProfessionAppearanceTextureBindings.Current = oldTextureBindings; }
            }
            Debug.Log("[Original scene weapons] " + stages + " D3D stages; " + frames + " pre-physics frames (" + visible + " visible); " + images + " GPU compositions; shared/independent demand lifecycle released.");
        }

        private static SceneWeaponEndpoint CreateSceneWeapon(ProfessionNativeSceneWeaponCatalog.Entry entry, ProfessionNativeSceneWeaponCatalog catalog,
            IProfessionAppearanceUILiteBindings bindings, Transform parent, out AppearanceSkeletonPart part, out SkeletonRendererFollower follower)
        {
            var source = JsonUtility.FromJson<SceneHierarchy>(entry.Hierarchy.text);
            Require(source.nodes.Length == 4, "complete original integrated scene weapon hierarchy");
            var nodes = new Dictionary<string, GameObject>();
            foreach (var node in source.nodes)
            {
                var go = new GameObject(node.name); go.SetActive(false); go.layer = node.game_object.fields.m_Layer;
                go.transform.SetParent(string.IsNullOrEmpty(node.parent) ? parent : nodes[node.parent].transform, false);
                var fields = node.components.Single(row => row.type == "Transform").runtime_fields;
                go.transform.localPosition = fields.m_LocalPosition; go.transform.localRotation = fields.m_LocalRotation; go.transform.localScale = fields.m_LocalScale;
                nodes.Add(node.game_object.source.path_id, go);
            }
            var spineNode = source.nodes.Single(node => node.name == "Spine");
            var spine = nodes[spineNode.game_object.source.path_id];
            spine.AddComponent<MeshFilter>(); var renderer = spine.AddComponent<MeshRenderer>(); renderer.sharedMaterial = entry.OriginalMaterial;
            var rendererFields = spineNode.components.Single(row => row.type == "MeshRenderer").runtime_fields;
            renderer.sortingLayerID = rendererFields.m_SortingLayerID; renderer.sortingOrder = rendererFields.m_SortingOrder;
            var animation = spineNode.components.Single(row => row.type == "Spine.Unity.SkeletonAnimation").runtime_fields;
            Require(animation.timeScale == 1 && animation.loop, "original default scene weapon clock");
            var endpoint = spine.AddComponent<SceneWeaponEndpoint>(); endpoint.State = new ProfessionNativeAppearanceSkeleton(catalog.Weapon.Model.Data);
            endpoint.State.SetSkin(endpoint.State.Data.FindSkin(animation.initialSkinName)); endpoint.State.SetToSetupPose();
            endpoint.SourceMaterial = entry.OriginalMaterial; endpoint.PlayAnimation(animation._animationName, animation.loop);
            endpoint.UseClipping = animation.useClipping; endpoint.PmaVertexColors = animation.pmaVertexColors;
            endpoint.SourceZSpacing = animation.zSpacing; endpoint.InvisibleMode = animation.updateWhenInvisible; endpoint.Sample(0);
            var lite = spine.AddComponent<AppearanceRendererLite>(); lite.SkeletonRenderer = endpoint; lite.Configure(bindings);
            lite.LevelOfResolution = (LevelOfResolution)spineNode.components.Single(row => row.type == "AppearanceSystem.AppearanceRendererLite").runtime_fields.LevelOfResolution;
            lite.RenderExistingMeshes = new List<Component>();
            Require(spineNode.components.Single(row => row.type == "AppearanceSystem.AppearanceRendererLite").runtime_fields.RenderExistingMeshes.Length == 0,
                "original scene weapon has no existing-mesh replacements");
            foreach (var node in source.nodes)
                foreach (var component in node.components.Where(row => row.type == "SkeletonRendererFollower"))
                {
                    var go = nodes[node.game_object.source.path_id]; var follow = go.AddComponent<SkeletonRendererFollower>(); var f = component.runtime_fields;
                    follow.BoneName = f.BoneName; follow.SlotName = f.SlotName; follow.AttachmentName = f.AttachmentName;
                    follow.FollowZSpacing = f.FollowZSpacing; follow.ZFront = f.ZFront; follow.FollowLocalScale = f.FollowLocalScale;
                    follow.FollowBoneWorldFlip = f.FollowBoneWorldFlip; follow.FollowRotation = f.FollowRotation;
                    follow.FollowSkeletonTransformFlipX = f.FollowSkeletonTransformFlipX; follow.InitializeOnAwake = f.InitializeOnAwake;
                    follow.Gos = f.Gos.Select(pointer => nodes[pointer.m_PathID.ToString()]).ToArray();
                    if (node.name == "Effect") follow.SkeletonRenderer = endpoint;
                }
            var root = nodes[source.nodes[0].game_object.source.path_id];
            part = root.AddComponent<AppearanceSkeletonPart>(); part.Skeleton = spine; part.SkinType = SkinType.pWeapon;
            follower = root.GetComponent<SkeletonRendererFollower>(); part.mSkeletonFollower = follower;
            foreach (var node in source.nodes.Reverse()) nodes[node.game_object.source.path_id].SetActive(node.game_object.fields.m_IsActive);
            return endpoint;
        }

        private sealed class ScenePartBindings : IProfessionAppearancePartBindings
        {
            public AppearanceConfig Config { get; }
            internal ScenePartBindings(AppearanceConfig config) { Config = config; }
            public bool HasEffect(string name) => false;
            public IProfessionAppearancePartAsset LoadPrefab(string path) => throw new NotSupportedException(path);
            public GameObject Instantiate(GameObject prefab, Transform parent, bool world) => throw new NotSupportedException();
        }
        public sealed class SceneCoreFollower : MonoBehaviour, IProfessionFollowerRenderer
        {
            internal ProfessionNativeAppearanceSkeleton State;
            public IProfessionFollowerSkeleton Skeleton => State;
            public bool UseCustomZSpacing => false;
            public int UpdateWhenInvisible => 0;
            public int UpdateMode => 3;
            public float ZSpacing => 0; // Explicit diagnostic parent depth; production source renderer remains unbound.
            public event Action<IProfessionFollowerAnimation> OnAnimationRebuild { add { } remove { } }
        }
        public sealed class SceneWeaponEndpoint : MonoBehaviour, IProfessionAppearanceRendererLite, IProfessionFollowerRenderer, IProfessionPartAnimatedSkeleton
        {
            internal ProfessionNativeAppearanceSkeleton State; internal Material SourceMaterial;
            internal bool UseClipping, PmaVertexColors; internal float SourceZSpacing; internal int InvisibleMode;
            private string animation; private bool loop;
            public Component Component => this;
            public string SkeletonDataName => "primaryWeapon";
            public IProfessionAppearanceSkeleton Skeleton => State;
            IProfessionFollowerSkeleton IProfessionFollowerAnimation.Skeleton => State;
            public bool UseCustomZSpacing => false;
            public int UpdateWhenInvisible => InvisibleMode;
            public int UpdateMode => 3;
            public float ZSpacing => SourceZSpacing;
            public int SeparatorSlotCount => 0;
            public string[] AnimationNames => State.Source.AnimationNames.ToArray();
            public Dictionary<Material, Material> CustomMaterialOverride { get; } = new Dictionary<Material, Material>();
            public event Action<IProfessionAppearanceRendererLite> OnCustomDemandLoading;
            public event Action<IProfessionFollowerAnimation> OnAnimationRebuild;
            public IEnumerable<Material> SkinMaterials(IProfessionAppearanceSkin skin)
            {
                foreach (var attachment in ((ProfessionNativeSkin)skin).Entries.Select(row => row.Attachment).OfType<ProfessionNativeRenderAttachment>())
                { Require(attachment.Page + "_placeholder" == SourceMaterial.mainTexture.name, "original scene skin atlas-page material"); yield return SourceMaterial; }
            }
            public void Initialize(bool overwrite, bool quiet) { if (overwrite) OnAnimationRebuild?.Invoke(this); }
            public void UpdateAnimationAndMesh() { Sample(0); Demand(); }
            internal void Demand() => OnCustomDemandLoading?.Invoke(this);
            public void PlayAnimation(string name, bool value)
            {
                if (!AnimationNames.Contains(name)) { Debug.LogWarning("Original scene weapon has no animation: " + name); return; }
                animation = name; loop = value;
            }
            internal void Sample(float time)
            {
                float duration = MaxTime(State.Source.GetAnimation(animation));
                State.EvaluateUnmixedWeaponAnimation(animation, loop && duration > 0 ? time % duration : time);
            }
            private static float MaxTime(object value)
            {
                if (value is IReadOnlyDictionary<string, object> map)
                    return map.Select(pair => pair.Key == "time" ? Convert.ToSingle(pair.Value) : MaxTime(pair.Value)).DefaultIfEmpty(0).Max();
                if (value is IEnumerable<object> array) return array.Select(MaxTime).DefaultIfEmpty(0).Max();
                return 0;
            }
        }

        private static void CaptureSceneWeapon(string folder, string name, CorePoseEndpoint core, ProfessionNativeMeshBatches coreBatches,
            SceneWeaponEndpoint weapon, ProfessionNativeMeshBatches weaponBatches, int separator)
        {
            var world = weapon.transform.localToWorldMatrix;
            var bounds = coreBatches.Batches[0].Mesh.bounds;
            foreach (var batch in coreBatches.Batches) bounds.Encapsulate(batch.Mesh.bounds);
            foreach (var batch in weaponBatches.Batches) foreach (Vector3 vertex in batch.Mesh.vertices) bounds.Encapsulate(world.MultiplyPoint3x4(vertex));
            float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            int split = Enumerable.Range(0, core.State.SlotCount).Single(i => core.State.SlotPose.DrawSlot(i) == separator);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var old = RenderTexture.active; var capture = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    foreach (var batch in weaponBatches.Batches) commands.DrawMesh(batch.Mesh, fit * world, weapon.CustomMaterialOverride[weapon.SourceMaterial], 0, 0);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(pixel => pixel.a > 8) > 10, "original world shader renders weapon pixels independently");
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    // Diagnostic split composition; production world depth/order is verified separately.
                    foreach (var batch in coreBatches.Batches.Where(batch => batch.LastDrawIndex < split)) commands.DrawMesh(batch.Mesh, fit, core.PageMaterial(batch.Page));
                    foreach (var batch in weaponBatches.Batches) commands.DrawMesh(batch.Mesh, fit * world, weapon.CustomMaterialOverride[weapon.SourceMaterial], 0, 0);
                    foreach (var batch in coreBatches.Batches.Where(batch => batch.LastDrawIndex >= split)) commands.DrawMesh(batch.Mesh, fit, core.PageMaterial(batch.Page));
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(pixel => pixel.a > 8) > 500, "scene weapon composition raster");
                File.WriteAllBytes(Path.Combine(folder, "scene-weapon-" + name + ".png"), capture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(capture); }
        }
    }
}
