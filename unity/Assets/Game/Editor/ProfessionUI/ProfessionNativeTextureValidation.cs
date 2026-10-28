using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionNativeTextureValidation
    {
        [Serializable] private sealed class Fixture { public Expected[] textures; }
        [Serializable] private sealed class Mip { public int level, width, height, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV; public float m_MipBias; }
        [Serializable] private sealed class Expected
        { public string path; public int width, height, mip_count, texture_format; public Sampler settings; public Mip[] mips; }
        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "../../.."));
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(root, ".tools/profession-native-ui/871_156598/typed/native-textures-fixture.json")));
            Require(fixture.textures.Length == 4 && SystemInfo.supportsAsyncGPUReadback, "four native textures and GPU readback support");
            foreach (var e in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.path);
                Require(texture != null && texture.width == e.width && texture.height == e.height && texture.mipmapCount == e.mip_count && (int)texture.format == e.texture_format, e.path + " native dimensions/format/mips");
                Require((int)texture.filterMode == e.settings.m_FilterMode && texture.anisoLevel == e.settings.m_Aniso &&
                    (int)texture.wrapModeU == e.settings.m_WrapU && (int)texture.wrapModeV == e.settings.m_WrapV &&
                    Math.Abs(texture.mipMapBias - e.settings.m_MipBias) < 0.000001f, "original sampler fields");
                foreach (var mip in e.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    request.WaitForCompletion(); Require(!request.hasError, "GPU upload/readback");
                    var bytes = request.GetData<byte>().ToArray();
                    using (var hash = SHA256.Create())
                        Require(bytes.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256, e.path + " exact source mip bytes on GPU");
                }
            }
            Debug.Log("Profession native UI textures passed: all four original RGBA32/Alpha8 mip payloads match exact GPU readback; no PNG re-compression.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Profession native texture: " + message); }
    }
}
