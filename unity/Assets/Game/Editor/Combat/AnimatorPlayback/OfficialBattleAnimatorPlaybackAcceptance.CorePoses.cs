using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
        [Serializable] private sealed class CorePoseCases { public CorePoseBody[] bodies; }
        [Serializable] private sealed class CorePoseBody { public string sex, prefix; public int profession; public CorePoseCase[] cases; public string[] capture; }
        [Serializable] private sealed class CorePoseCase { public string animation; public float duration; public float[] times; }

        public static void RunCorePoses()
        {
            Run();
            SessionState.SetBool(Key + ".corePoses", true);
        }

        private static IEnumerator CorePoseSteps()
        {
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<CorePoseCases>(File.ReadAllText(Path.Combine(folder, "core-pose-cases.json")));
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var catalog = Resources.Load<ProfessionNativeCharacterCatalog>("ProfessionUI/character-resources");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            var failures = new List<string>();
            int total = 0, images = 0;
            Require(fixture != null && fixture.bodies.Length == 4 && config && rules && catalog && textures, "original full core pose inputs");
            var resources = new OfflineProfessionCharacterResources(config, catalog,
                (handler, skins) => { throw new InvalidOperationException("Separate parts are outside this core pose check."); },
                (handler, all) => { throw new InvalidOperationException("Separate part release is outside this core pose check."); });
            foreach (var body in fixture.bodies)
            {
                var go = new GameObject("Original complete core skill pose " + body.sex + "/" + body.prefix);
                try
                {
                    var endpoint = go.AddComponent<CorePoseEndpoint>(); endpoint.Textures = textures;
                    endpoint.Configure(new CorePoseBindings(endpoint), resources);
                    var character = go.AddComponent<AppearanceCharacter>(); character.Configure(resources);
                    Sex sex = body.sex == "male" ? Sex.Male : Sex.Female;
                    var defaults = resolver.BuildDefaultAppearanceData(config, sex, (Profession)body.profession);
                    var player = new OfflinePlayerProgress { InitialWeaponType = body.profession == 1 ? 200 : 201 };
                    player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = body.profession;
                    foreach (var pair in defaults.SkinMap) player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                    foreach (var pair in defaults.DyeMap) player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId,
                        Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                    var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => body.profession == 1 ? 200 : 201, tables, config, rules);
                    ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, sex, body.profession, graph.AppearanceData, AppendSkinType.Game);
                    var state = endpoint.State; var data = state.Source;
                    Require(endpoint.Skins.Contains("performFace") && !endpoint.Skins.Contains("map_01"), "original Game append skins instead of UI/map append skins");
                    Require(graph.GetProfessionIdStr(body.profession) == body.prefix, "original profession animation prefix");
                    var expectedNames = data.AnimationNames.Where(name => name.StartsWith(body.prefix + "/", StringComparison.Ordinal)).OrderBy(name => name).ToArray();
                    Require(expectedNames.SequenceEqual(body.cases.Select(row => row.animation).OrderBy(name => name)), "all original class combat actions");
                    Require(character.TryGetSkinName(SkinType.body, out var clothing) && endpoint.Skins.Contains(clothing), "original class body skin");
                    var channels = body.cases.ToDictionary(row => row.animation, row => new ProfessionNativeAnimationChannels(data, row.animation));
                    int checkedFrames = 0;
                    using (var batches = new ProfessionNativeMeshBatches(data.SlotCount))
                    {
                        foreach (var row in body.cases)
                        {
                            Require(channels[row.animation].Duration == row.duration, "same original full-channel duration");
                            foreach (float time in row.times)
                            {
                                try
                                {
                                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, channels[row.animation], time, false);
                                    batches.Build(state.Pose, state.CreateDrawSlots());
                                    foreach (var batch in batches.Batches)
                                    {
                                        Require(batch.Blend == 0, "restore additional core material blend");
                                        endpoint.PageMaterial(batch.Page);
                                        foreach (var vertex in batch.Mesh.vertices) Require(IsFinite(vertex.x) && IsFinite(vertex.y), "finite complete core geometry");
                                    }
                                    checkedFrames++;
                                }
                                catch (Exception error) { failures.Add(body.sex + "/" + row.animation + " at " + time + ": " + error.Message); break; }
                            }
                        }
                        foreach (string name in body.capture)
                        {
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, channels[name], .4f, false);
                            batches.Build(state.Pose, state.CreateDrawSlots());
                            CaptureCorePose(folder, body.sex + "-" + name.Replace('/', '-'), endpoint, batches); images++;
                        }
                    }
                    total += checkedFrames;
                    Debug.Log("[Original core skill poses] " + body.sex + "/" + body.prefix + ": " + channels.Count + " actions, " + checkedFrames
                        + " pre-physics frames; original Game append, resource/skin/dye graph and full core geometry; " + string.Join(",", endpoint.Skins));
                    character.Release();
                }
                finally { Object.DestroyImmediate(go); }
                yield return null;
            }
            File.WriteAllLines(Path.Combine(folder, "core-pose-failures.txt"), failures);
            Require(failures.Count == 0, "complete core pose failures: " + string.Join("; ", failures.Take(8)));
            Require(images == 8 && total > 2000, "complete original combat core pose coverage");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void CaptureCorePose(string folder, string name, CorePoseEndpoint endpoint, ProfessionNativeMeshBatches batches)
        {
            Require(batches.Batches.Count > 0, "original full core diagnostic has geometry");
            var bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches) bounds.Encapsulate(batch.Mesh.bounds);
            float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
            Require(extent > 0 && IsFinite(extent), "original full core diagnostic bounds");
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var capture = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    foreach (var batch in batches.Batches) commands.DrawMesh(batch.Mesh, fit, endpoint.PageMaterial(batch.Page));
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(pixel => pixel.a > 8) > 500, "original full core diagnostic has rendered source pixels");
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), capture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(capture); }
        }

        private sealed class CorePoseBindings : IProfessionAppearanceSpineBindings
        {
            private readonly CorePoseEndpoint endpoint;
            internal CorePoseBindings(CorePoseEndpoint endpoint) { this.endpoint = endpoint; }
            public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner) => endpoint;
            public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet) => ((ProfessionNativeSkeletonAsset)asset).Data.SkinCatalog;
            public IProfessionAppearanceSkin CreateSkin(string name) => endpoint.State.CreateSkin(name);
        }

        public sealed class CorePoseEndpoint : AppearanceSpine, IProfessionAppearanceSkeletonComponent
        {
            internal ProfessionNativeTextureCatalog Textures;
            internal ProfessionNativeAppearanceSkeleton State;
            private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            public IProfessionAppearanceSkeleton Skeleton => State;
            public override YooAssetTextureLoader TextureLoader => null;
            protected override void InitSkeleton(ScriptableObject asset) => State = new ProfessionNativeAppearanceSkeleton(((ProfessionNativeSkeletonAsset)asset).Data);
            internal Material PageMaterial(string page)
            {
                if (materials.TryGetValue(page, out var material)) return material;
                var texture = Textures.Find(State.Source.Sex, page);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
                Require(shader && shader.isSupported, "original converted core diagnostic shader");
                material = new Material(shader); material.SetTexture("_MainTex", texture.Main); material.SetTexture("_DyeTex", texture.Dye);
                materials.Add(page, material); return material;
            }
            protected override Material GetMaterial(string name)
            {
                var skin = (ProfessionNativeSkin)State.Source.SkinCatalog.FindSkin(name);
                var pages = skin.Entries.Select(entry => entry.Attachment).OfType<ProfessionNativeRenderAttachment>().Select(attachment => attachment.Page).Distinct().ToArray();
                Require(pages.Length == 1, "original single-page core dye skin");
                return PageMaterial(pages[0]);
            }
            protected override void OnDestroy()
            {
                base.OnDestroy();
                foreach (var material in materials.Values) Object.DestroyImmediate(material);
                materials.Clear();
            }
        }
    }
}
