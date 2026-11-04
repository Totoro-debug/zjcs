using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        [Serializable] private sealed class HighMip { public int level, width, height, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class HighSampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] private sealed class HighFields
        {
            public string m_Name; public int m_Width, m_Height, m_MipCount, m_ColorSpace; public bool m_IsReadable;
            public HighSampler m_TextureSettings;
        }
        [Serializable] private sealed class HighTexture { public string resource; public int target_format; public HighFields original_fields; public HighMip[] mips; }
        [Serializable] private sealed class HighAddress { public string Guid, Resource; }
        [Serializable] private sealed class HighMapping { public int index; public string placeholder, main, dye; }
        [Serializable] private sealed class HighCases { public HighTexture[] textures; public HighAddress[] addresses; public HighMapping[] mappings; public string[] skins; }

        public static void RunHighTextures()
        {
            Run(); SessionState.SetBool(Key + ".high-textures", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 1800);
        }

        private static void CheckHighTextures()
        {
            var work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<HighCases>(File.ReadAllText(Path.Combine(work, "weapon-high-texture-cases.json")));
            var resources = Resources.Load<ProfessionNativeWeaponTextureResources>("Combat/Weapons/texture-resources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            Require(resources && resources.Loader && resources.Atlas && resources.ManagerConfig, "complete selected weapon High native graph");
            Require(resources.Loader.placeholderMap.Length == fixture.mappings.Length && fixture.mappings.Length == 398
                && resources.Atlas.MaterialCount == 398 && fixture.addresses.Length == 398 && fixture.textures.Length == 398,
                "complete original weapon High inventory");
            Require(resources.TextureGuids.OrderBy(g => g).SequenceEqual(fixture.addresses.Select(a => a.Guid).OrderBy(g => g)),
                "all original High GUIDs are explicitly bound");
            var loader = resources.Loader;
            Require(loader.LevelOfResolution == LevelOfResolution.High && loader.maxPlaceholderSize == 32
                && loader.unloadAfterSecondsUnused == 60 && loader.PlaceholderPath == "Atlas/Placeholder"
                && loader.TargetTexturePath == "Atlas/High" && loader.DyeTexturePath == "" && !loader.skeletonDataAsset,
                "selected original High loader settings");
            var pages = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var row in fixture.mappings)
            {
                var mapping = loader.placeholderMap[row.index]; var material = resources.Atlas.MaterialAt(row.index);
                Require(mapping.placeholderTexture && mapping.placeholderTexture.name == row.placeholder
                    && mapping.targetTextureReference.AssetReference.Guid == row.main
                    && mapping.dyeTextureReference.AssetReference.Guid == row.dye && material.mainTexture == mapping.placeholderTexture,
                    "original full High/placeholder/material order: " + row.index);
                Require(material.shader && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader),
                    "selected original material shader compiles for D3D: " + material.name);
                pages.Add(row.placeholder.Substring(0, row.placeholder.Length - "_placeholder".Length), material);
            }
            using var manager = new AppearanceAssetManager(resources.ManagerConfig, _ => new Subscription());
            var firstOwner = new OfflineProfessionWeaponTextureResources(config, manager, resources);
            var secondOwner = new OfflineProfessionWeaponTextureResources(config, manager, resources);
            bool rejected = false;
            try { firstOwner.LoadAssetSyncByGuid<Material>(fixture.addresses[0].Guid); }
            catch (NotSupportedException) { rejected = true; }
            Require(rejected && firstOwner.LoadedResourceTextureCount == 0, "incompatible request rejects before loading any resource");
            rejected = false;
            try { firstOwner.LoadAssetSyncByGuid<Texture2D>("missing-original-guid"); }
            catch (NotSupportedException) { rejected = true; }
            Require(rejected && firstOwner.LoadedResourceTextureCount == 0, "unbound GUID cannot silently substitute a texture");
            int mipCount = 0; long bytes = 0;
            foreach (var expected in fixture.textures)
            {
                var address = fixture.addresses.First(a => a.Resource == expected.resource);
                Require(resources.TextureResource(address.Guid) == expected.resource, "explicit lazy resource address");
                var first = firstOwner.LoadAssetSyncByGuid<Texture2D>(address.Guid);
                var duplicate = firstOwner.LoadAssetSyncByGuid<Texture2D>(address.Guid);
                var second = secondOwner.LoadAssetSyncByGuid<Texture2D>(address.Guid);
                try
                {
                    var texture = second.Result; var f = expected.original_fields; var s = f.m_TextureSettings;
                    Require(first.Result == texture && duplicate.Result == texture && texture.name == f.m_Name
                        && texture.width == f.m_Width && texture.height == f.m_Height && texture.mipmapCount == f.m_MipCount
                        && texture.format == (TextureFormat)expected.target_format && texture.isReadable == f.m_IsReadable
                        && texture.filterMode == (FilterMode)s.m_FilterMode && texture.anisoLevel == s.m_Aniso
                        && texture.mipMapBias == s.m_MipBias && texture.wrapModeU == (TextureWrapMode)s.m_WrapU
                        && texture.wrapModeV == (TextureWrapMode)s.m_WrapV && texture.wrapModeW == (TextureWrapMode)s.m_WrapW
                        && (expected.target_format != 4 || UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) == (f.m_ColorSpace == 1)),
                        "all original texture flags, dimensions, mip count and samplers: " + f.m_Name);
                    int callbacks = 0; first.Completed += h => { Require(h.Result == texture, "synchronous High completion retains this request"); callbacks++; };
                    first.WaitForAsyncComplete(); first.Release(); first.Release(); duplicate.Release();
                    Require(callbacks == 1 && !first.IsValid && !first.Result && second.IsValid && second.Result == texture
                        && firstOwner.LoadedResourceTextureCount == 0 && secondOwner.LoadedResourceTextureCount == 1,
                        "independent/double request releases preserve the other live owner");
                    foreach (var mip in expected.mips)
                    {
                        var readback = AsyncGPUReadback.Request(texture, mip.level); readback.WaitForCompletion();
                        Require(!readback.hasError, "actual original High GPU readback");
                        var data = readback.GetData<byte>().ToArray();
                        using var sha = SHA256.Create();
                        string digest = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                        Require(data.Length == mip.rgba_size && digest == mip.rgba_sha256, "exact decoded original High mip bytes: " + f.m_Name);
                        mipCount++; bytes += data.Length;
                    }
                }
                finally { first.Release(); duplicate.Release(); second.Release(); }
                Require(firstOwner.LoadedResourceTextureCount == 0 && secondOwner.LoadedResourceTextureCount == 0,
                    "all requests release this High page");
            }
            var previous = ProfessionAppearanceTextureBindings.Current;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            ProfessionAppearanceTextureBindings.Current = firstOwner;
            try
            {
                var weapon = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
                Require(fixture.skins.Length == 390, "all current manifest scene weapon skins");
                foreach (string skin in fixture.skins)
                {
                    var skeleton = new ProfessionNativeAppearanceSkeleton(weapon.Model.Data);
                    var originalSkin = skeleton.Data.FindSkin(skin);
                    Require(originalSkin != null, "source-identical full original weapon skin: " + skin);
                    skeleton.SetSkin(originalSkin); skeleton.SetToSetupPose(); skeleton.UpdateCache(); skeleton.Pose.UpdateWorld();
                    // Explicit setup geometry checks the complete High art binding.
                    // This probe does not claim animation/physics or prefab effects.
                    using var batches = new ProfessionNativeMeshBatches(skeleton.SlotCount);
                    batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), pmaVertexColors: true, allowConcaveClipping: true);
                    Require(batches.Batches.Count > 0 && batches.Batches.All(b => b.Blend == 0), "original visible normal-blend weapon geometry: " + skin);
                    var source = batches.Batches.Select(b => pages[b.Page]).ToArray(); var shared = new Material[source.Length];
                    IAppearanceAssetManager assets = manager;
                    assets.GetLoaderSharedMaterials(loader, source, ref shared);
                    try
                    {
                        Require(shared.All(m => m && m.mainTexture && !m.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)),
                            "actual original manager supplies High art for every native weapon page: " + skin);
                        CaptureHighWeapon(batches, shared, skin, work);
                    }
                    finally { assets.ReturnLoaderSharedMaterials(loader, source, true); }
                    Require(firstOwner.LoadedResourceTextureCount == 0 && source.All(m => m.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)),
                        "original manager returns lazy page requests and retains source placeholders: " + skin);
                }
            }
            finally { ProfessionAppearanceTextureBindings.Current = previous; ShaderUtil.allowAsyncCompilation = previousAsync; }
            Debug.Log("Complete selected weapon High passed: 398 original mappings/GUIDs, " + fixture.textures.Length + " lazy native textures, "
                + mipCount + " exact GPU mip hashes (" + bytes + " bytes), dual-owner/refcount/type guards and original manager release; 390 original setup-geometry weapon captures. Weapon animation/physics, full particle/sprite prefabs and production factory remain separate.");
        }

        private static void CaptureHighWeapon(ProfessionNativeMeshBatches batches, Material[] materials, string skin, string work)
        {
            var bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches.Skip(1)) bounds.Encapsulate(batch.Mesh.bounds);
            float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
            Require(extent > 0 && float.IsFinite(extent), "finite original weapon geometry bounds");
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active; Texture2D capture = null;
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    for (int i = 0; i < batches.Batches.Count; i++)
                    {
                        Require(materials[i].passCount == 2, "original weapon scene/reflection subshader selected: "
                            + materials[i].passCount + "/pipeline=" + Shader.globalRenderPipeline);
                        commands.DrawMesh(batches.Batches[i].Mesh, fit, materials[i], 0, 0);
                    }
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; capture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); capture.Apply();
                File.WriteAllBytes(Path.Combine(work, "high-weapon-" + skin + ".png"), capture.EncodeToPNG());
                int coverage = capture.GetPixels32().Count(p => p.a > 32);
                Require(coverage > 1000, "actual High weapon GPU coverage: " + skin + " pixels=" + coverage
                    + " bounds=" + bounds + " materials=" + string.Join(",", materials.Select(m => m.name + "/" + m.shader.name
                        + "/passes=" + m.passCount + "/tag=" + m.shader.FindPassTagValue(0, new ShaderTagId("LightMode")).name
                        + "/upper=" + m.shader.FindPassTagValue(0, new ShaderTagId("LIGHTMODE")).name)));
            }
            finally { RenderTexture.active = previous; if (capture) Object.DestroyImmediate(capture); RenderTexture.ReleaseTemporary(target); }
        }
    }
}
