using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class ClothingTextures
        { public int total_pages; public ClothingPage[] pages; public ClothingTexture[] textures; }
        [Serializable] private sealed class ClothingPage { public string sex, page, main_guid, dye_guid; }
        [Serializable] private sealed class ClothingMip { public int level, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class ClothingSampler
        { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] private sealed class ClothingTexture
        {
            public string path, guid;
            public int width, height, mip_count, texture_format;
            public ClothingSampler settings;
            public ClothingMip[] mips;
        }

        private static void CheckOriginalClothingTextures(string folder)
        {
            var fixture = JsonUtility.FromJson<ClothingTextures>(File.ReadAllText(Path.Combine(folder, "local-clothing-textures.json")));
            var catalog = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            Require(fixture.pages.Length == 4 && fixture.textures.Length == 6 && catalog.Count == fixture.total_pages,
                "four original local-clothing bindings retain all existing catalog entries (pages=" + fixture.pages.Length
                + ", textures=" + fixture.textures.Length + ", catalog=" + catalog.Count + ", expected=" + fixture.total_pages + ")");
            Require(SystemInfo.supportsAsyncGPUReadback, "original clothing actual GPU readback");
            long bytes = 0; int levels = 0;
            foreach (var expected in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(expected.path);
                Require(texture && AssetDatabase.AssetPathToGUID(expected.path) == expected.guid, "original clothing native texture identity");
                Require(texture.width == expected.width && texture.height == expected.height && texture.mipmapCount == expected.mip_count
                    && (int)texture.format == expected.texture_format, "original clothing dimensions, mips and format");
                var sampler = expected.settings;
                Require((int)texture.filterMode == sampler.m_FilterMode && texture.anisoLevel == sampler.m_Aniso
                    && (int)texture.wrapModeU == sampler.m_WrapU && (int)texture.wrapModeV == sampler.m_WrapV
                    && (int)texture.wrapModeW == sampler.m_WrapW && Mathf.Abs(texture.mipMapBias - sampler.m_MipBias) < .000001f,
                    "original clothing sampler settings");
                foreach (var mip in expected.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level); request.WaitForCompletion();
                    Require(!request.hasError, "original clothing GPU upload/readback");
                    var pixels = request.GetData<byte>().ToArray();
                    using (var hash = SHA256.Create())
                        Require(pixels.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(pixels)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256,
                            "original clothing exact decoded GPU mip bytes: " + expected.path);
                    bytes += pixels.LongLength; levels++;
                }
            }
            foreach (var expected in fixture.pages)
            {
                var page = catalog.Find(expected.sex, expected.page);
                Require(page.Main && page.Dye && page.Placeholder && page.MainGuid == expected.main_guid && page.DyeGuid == expected.dye_guid,
                    "original high-resolution clothing loader GUIDs and complete page references");
                Require(catalog.ByGuid(expected.main_guid) == page.Main && catalog.ByGuid(expected.dye_guid) == page.Dye,
                    "original clothing local GUID lookup");
            }
            Debug.Log("[Original local clothing textures] " + fixture.pages.Length + " bindings, " + fixture.textures.Length
                + " textures, " + levels + " exact GPU mips, " + bytes + " decoded bytes.");
        }
    }
}
