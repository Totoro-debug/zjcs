using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneTextures()
        {
            Run(); SessionState.SetBool(Key + ".sceneTextures", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 1200);
        }

        private static Dictionary<string, object> SceneTextureObject(object value) => (Dictionary<string, object>)value;
        private static List<object> SceneTextureArray(object value) => (List<object>)value;
        private static int SceneTextureInt(object value) => Convert.ToInt32(value);
        private static float SceneTextureFloat(object value) => Convert.ToSingle(value);

        private static IEnumerator SceneTextureSteps()
        {
            var folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = SceneTextureObject(OfficialEquipmentJson.Parse(File.ReadAllText(Path.Combine(folder, "scene-textures-fixture.json"))));
            var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            Require(catalog && catalog.TextureCount == 977 && catalog.Skins.SkinTextureConfigList.Count == 764
                && catalog.Loader.placeholderMap.Length == 702, "complete current Low, skin and lazy texture bindings");
            Require((int)catalog.Loader.LevelOfResolution == 1 && catalog.Loader.atlasAsset == null && catalog.Loader.skeletonDataAsset == null
                && catalog.Loader.PlaceholderPath == "Atlas/Placeholder" && catalog.Loader.TargetTexturePath == "Atlas/Low"
                && catalog.Loader.DyeTexturePath == "ColorMap/Low", "original common Low loader fields");
            catalog.Loader.TryInit();
            Require(catalog.Loader.PlaceholderTextureIndexDict.Count == 702, "every original placeholder identity is unique");
            var loaderSerialized = new SerializedObject(catalog.Loader).FindProperty("placeholderMap");
            var mappings = SceneTextureArray(fixture["mappings"]);
            for (int i = 0; i < mappings.Count; i++)
            {
                var expected = SceneTextureObject(mappings[i]); var actual = loaderSerialized.GetArrayElementAtIndex(i);
                Require(actual.FindPropertyRelative("placeholderTexture").objectReferenceValue == AssetDatabase.LoadAssetAtPath<Texture>((string)expected["path"])
                    && actual.FindPropertyRelative("targetTextureReference.AssetReference.mAssetGuid").stringValue == (string)expected["main"]
                    && actual.FindPropertyRelative("dyeTextureReference.AssetReference.mAssetGuid").stringValue == (string)expected["dye"], "original complete ordered Low mapping and GUID proxies");
            }
            foreach (var model in catalog.Models)
            {
                Require(model.Asset == ProfessionNativeSceneCharacterAsset.LoadResource(model.Sex)
                    && model.Asset.Data.Manifest == ProfessionNativeSceneCharacterAsset.SourceManifest, "current original model identity");
                Require(model.Atlas.MaterialCount == (model.Sex == "male" ? 468 : 469) && model.Pages.Length == model.Atlas.MaterialCount, "complete ordered current atlas");
                for (int i = 0; i < model.Pages.Length; i++)
                    Require(model.Atlas.MaterialAt(i).mainTexture.name == model.Pages[i] + "_placeholder"
                        && catalog.MaterialForPage(model.Asset, model.Pages[i]) == model.Atlas.MaterialAt(i), "original ordered page and material identity");
            }
            var resources = new OfflineProfessionSceneTextureResources(catalog);
            var address = catalog.TextureAddresses[0];
            var first = resources.LoadAssetSyncByGuid<Texture>(address.Guid);
            var second = resources.LoadAssetSyncByGuid<Texture2D>(address.Guid);
            var otherScope = new OfflineProfessionSceneTextureResources(catalog);
            var otherOwner = otherScope.LoadAssetSyncByGuid<Texture2D>(address.Guid);
            Require(first.Result == second.Result && resources.LoadedTextureCount == 1, "independent requests share one actual current page");
            Require(otherOwner.Result == second.Result && otherScope.LoadedTextureCount == 1, "independent resource scopes share the same Unity resource identity");
            first.Release(); first.Release();
            Require(!first.IsValid && second.IsValid && resources.LoadedTextureCount == 1, "one owner's repeated release preserves another owner");
            resources.Dispose();
            bool rejected = false;
            try { resources.LoadAssetSyncByGuid<Texture>(address.Guid); } catch (ObjectDisposedException) { rejected = true; }
            Require(rejected && second.IsValid, "disposing a resource scope blocks new requests and preserves outstanding owners");
            second.Release();
            Require(resources.LoadedTextureCount == 0 && otherOwner.IsValid, "a scope's final release preserves another scope's live page");
            otherOwner.Release(); otherScope.Dispose();
            Require(otherScope.LoadedTextureCount == 0, "all scopes' final request releases its owned current page");

            using (resources = new OfflineProfessionSceneTextureResources(catalog))
            {
                int checkedTextures = 0, checkedMips = 0;
                var addresses = catalog.TextureAddresses.GroupBy(a => a.Resource).ToDictionary(g => g.Key, g => g.First().Guid);
                foreach (object item in SceneTextureArray(fixture["textures"]))
                {
                    var row = SceneTextureObject(item);
                    string path = (string)row["path"], resource = (string)row["resource"];
                    var original = SceneTextureObject(row["original_fields"]);
                    IProfessionUnsafeAssetHandle<Texture2D> handle = null;
                    var texture = resource.Length == 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                        : (handle = resources.LoadAssetSyncByGuid<Texture2D>(addresses[resource])).Result;
                    try
                    {
                        Require(texture && texture.name == (string)original["m_Name"]
                            && texture.width == SceneTextureInt(original["m_Width"]) && texture.height == SceneTextureInt(original["m_Height"])
                            && texture.mipmapCount == SceneTextureInt(original["m_MipCount"]) && (int)texture.format == SceneTextureInt(row["format"]), "actual current texture identity, size, mips and native format: " + path);
                        var sampler = SceneTextureObject(original["m_TextureSettings"]);
                        Require((int)texture.filterMode == SceneTextureInt(sampler["m_FilterMode"]) && texture.anisoLevel == SceneTextureInt(sampler["m_Aniso"])
                            && (int)texture.wrapModeU == SceneTextureInt(sampler["m_WrapU"]) && (int)texture.wrapModeV == SceneTextureInt(sampler["m_WrapV"])
                            && (int)texture.wrapModeW == SceneTextureInt(sampler["m_WrapW"])
                            && Mathf.Abs(texture.mipMapBias - SceneTextureFloat(sampler["m_MipBias"])) < .000001f
                            && texture.isReadable == (bool)original["m_IsReadable"]
                            && GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) == (SceneTextureInt(original["m_ColorSpace"]) == 1), "actual original current sampler, color space and readability: " + path);
                        foreach (var mipValue in SceneTextureArray(row["mips"]))
                        {
                            var mip = SceneTextureObject(mipValue);
                            var request = AsyncGPUReadback.Request(texture, SceneTextureInt(mip["level"]));
                            request.WaitForCompletion();
                            Require(!request.hasError, "actual native current texture GPU readback");
                            byte[] pixels = request.GetData<byte>().ToArray();
                            string hash;
                            using (var digest = SHA256.Create()) hash = BitConverter.ToString(digest.ComputeHash(pixels)).Replace("-", "").ToLowerInvariant();
                            Require(hash == (string)mip["rgba_sha256"], "actual current mip GPU bytes match original decoder: " + path);
                            checkedMips++;
                        }
                    }
                    finally { handle?.Release(); }
                    checkedTextures++;
                    if (checkedTextures % 75 == 0) { Debug.Log("[Current scene textures] " + checkedTextures + " actual original textures checked on GPU."); yield return null; }
                }
                Require(resources.LoadedTextureCount == 0, "per-page validation leaves no loaded current main or dye pages");
                int checkedMaterials = 0;
                foreach (var item in SceneTextureArray(fixture["materials"]))
                {
                    var row = SceneTextureObject(item); var original = SceneTextureObject(row["original_fields"]);
                    var material = AssetDatabase.LoadAssetAtPath<Material>((string)row["path"]);
                    Require(material && material.name == (string)original["m_Name"] && material.shader.name == "Zjcs/Official/ProfessionUI/SceneWeaponSkeletonDye2D"
                        && material.renderQueue == (SceneTextureInt(original["m_CustomRenderQueue"]) == -1 ? material.shader.renderQueue : SceneTextureInt(original["m_CustomRenderQueue"])), "actual current original material and world shader");
                    var keywords = SceneTextureArray(original["m_ValidKeywords"]).Concat(SceneTextureArray(original["m_InvalidKeywords"])).Cast<string>().OrderBy(s => s).ToArray();
                    Require(material.shaderKeywords.OrderBy(s => s).SequenceEqual(keywords), "all original current material keywords retained");
                    foreach (var bindingValue in SceneTextureArray(row["texture_bindings"]))
                    {
                        var binding = SceneTextureObject(bindingValue); string name = (string)binding["name"], path = (string)binding["path"];
                        var scale = SceneTextureObject(binding["scale"]); var offset = SceneTextureObject(binding["offset"]);
                        Require(material.GetTexture(name) == (path.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<Texture>(path))
                            && material.GetTextureScale(name) == new Vector2(SceneTextureFloat(scale["x"]), SceneTextureFloat(scale["y"]))
                            && material.GetTextureOffset(name) == new Vector2(SceneTextureFloat(offset["x"]), SceneTextureFloat(offset["y"])), "actual original material texture identity and transform");
                    }
                    var serialized = new SerializedObject(material);
                    var saved = SceneTextureObject(original["m_SavedProperties"]);
                    foreach (string kind in new[] { "m_Floats", "m_Colors", "m_Ints" })
                    {
                        var values = serialized.FindProperty("m_SavedProperties." + kind);
                        var expected = SceneTextureArray(saved[kind]);
                        Require(values.arraySize == expected.Count, "all original current material parameters retained");
                        for (int i = 0; i < expected.Count; i++)
                        {
                            var pair = SceneTextureArray(expected[i]); var actual = values.GetArrayElementAtIndex(i);
                            Require(actual.FindPropertyRelative("first").stringValue == (string)pair[0], "original material parameter order and name");
                            var value = actual.FindPropertyRelative("second");
                            if (kind == "m_Floats") Require(value.floatValue == SceneTextureFloat(pair[1]), "original material float value");
                            else if (kind == "m_Ints") Require(value.intValue == SceneTextureInt(pair[1]), "original material integer value");
                            else
                            {
                                var color = SceneTextureObject(pair[1]); var actualColor = value.colorValue;
                                Require(actualColor == new Color(SceneTextureFloat(color["r"]), SceneTextureFloat(color["g"]), SceneTextureFloat(color["b"]), SceneTextureFloat(color["a"])), "original material vector/color value");
                            }
                        }
                    }
                    checkedMaterials++;
                }
                Debug.Log("[Current scene resources] " + checkedTextures + " textures / " + checkedMips + " original mip GPU hashes, " + checkedMaterials + " original material identities, keywords and parameters; 702 Low mappings, 764 skin configurations and 977 lazy GUIDs.");
                var previous = ProfessionAppearanceTextureBindings.Current;
                ProfessionAppearanceTextureBindings.Current = resources;
                try
                {
                    foreach (var model in catalog.Models)
                    foreach (string group in new[] { "100", "200" })
                    {
                        var state = new ProfessionNativeAppearanceSkeleton(model.Asset.Data);
                        var skin = state.CreateSkin("Actual current body resource diagnostic");
                        foreach (string name in new[] { "default", "body_" + group + "_" + (model.Sex == "male" ? "M" : "F") + "_01", model.Sex == "male" ? "hair_G_01" : "hair_F_01" })
                        { var part = state.Data.FindSkin(name); Require(part != null, "actual current diagnostic source skin: " + name); skin.AddSkin(part); }
                        state.SetSkin(skin); state.SetToSetupPose();
                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, new ProfessionNativeAnimationChannels(state.Source, group + "/idle_01"), .4f, false);
                        var root = new GameObject("Actual current original Low body diagnostic");
                        var endpoint = root.AddComponent<SceneSeparatorEndpoint>();
                        var backend = endpoint.Backend = new ProfessionNativeSceneMeshSeparator(() => endpoint.Enables++);
                        var separator = DynamicSkeletonRenderSeparator.AddDynamicToSkeletonRenderer(endpoint, 0, 0, 0, 0);
                        var clones = new Dictionary<string, Material>();
                        var loaded = new List<int>();
                        try
                        {
                            using (var geometry = new ProfessionNativeMeshBatches(state.SlotCount))
                            {
                                geometry.Build(state.Pose, state.CreateDrawSlots(), true, true);
                                foreach (var batch in geometry.Batches)
                                    if (!clones.ContainsKey(batch.Page))
                                    {
                                        var material = new Material(catalog.MaterialForPage(model.Asset, batch.Page));
                                        int index = catalog.Loader.PlaceholderTextureIndexDict[material.mainTexture];
                                        Material replace = null;
                                        catalog.Loader.RequestLoadMaterialTextures(material, ref replace);
                                        catalog.Loader.AddLoadedTextureRef(index); loaded.Add(index);
                                        Require(material.mainTexture && !material.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal), "actual current Low page replaces source placeholder");
                                        clones.Add(batch.Page, material);
                                    }
                                backend.Render(geometry.Batches, Array.Empty<int>(), b => clones[b.Page]);
                                CaptureSeparator(separator, "current-" + model.Sex + "-" + group);
                            }
                        }
                        finally
                        {
                            backend.Dispose(); Object.Destroy(root);
                            foreach (var material in clones.Values) Object.Destroy(material);
                            foreach (int index in loaded) catalog.Loader.RemoveLoadedTextureRef(index);
                            catalog.Loader.TryUnloadUnusedTextures(true);
                        }
                        Require(resources.LoadedTextureCount == 0, "current Low body diagnostic releases main and dye requests");
                        yield return null;
                    }
                }
                finally { ProfessionAppearanceTextureBindings.Current = previous; }
            }
        }
    }
}
