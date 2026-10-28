using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionRewardSpriteValidation
    {
        [Serializable] private sealed class Fixture { public Expected[] items; }
        [Serializable] private sealed class OriginalRect { public float x, y, width, height; }
        [Serializable] private sealed class Expected
        {
            public string name, path, texture_path, rgba_sha256;
            public OriginalRect rect;
            public Vector2 pivot;
            public Vector4 border;
            public Vector2[] vertices, uv;
            public int[] triangles;
            public float ppu;
            public int width, height, mip_count, filter, aniso, wrapU, wrapV;
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Original profession Sprite: " + message);
        }
        private static void Near(float actual, float expected, string message)
        {
            Require(Math.Abs(actual - expected) <= 0.000001f, message + " actual=" + actual + " expected=" + expected);
        }
        private static void Vector(Vector2 actual, Vector2 expected, string message)
        {
            Near(actual.x, expected.x, message + ".x");
            Near(actual.y, expected.y, message + ".y");
        }
        public static void Run()
        {
            string fixturePath = ProfessionValidationWorkspace.Fixture("reward-sprite-native-fixture.json");
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(fixturePath));
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(fixture.items.Length == 4, "four official reward items");
                Require(SystemInfo.supportsAsyncGPUReadback, "GPU texture readback support");
                foreach (Expected expected in fixture.items) Validate(expected);
                Debug.Log("Original profession reward Sprite validation passed: 4 native Sprite geometries, original pivot/rect/UV/index buffers and 4 exact RGBA GPU readbacks.");
            }
            finally
            {
                AssetDatabase.DeleteAsset("Assets/ProfessionDynamicResourcesProbe");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
        private static void Validate(Expected e)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(e.texture_path);
            Require(sprite != null && texture != null, e.name + " native import");
            Require(sprite.texture == texture, e.name + " original texture binding");
            Require(sprite.name == e.name, "original name");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId) && localId == 21300000, "native Sprite object ID");
            Near(sprite.rect.x, e.rect.x, "rect.x"); Near(sprite.rect.y, e.rect.y, "rect.y");
            Near(sprite.rect.width, e.rect.width, "rect.width"); Near(sprite.rect.height, e.rect.height, "rect.height");
            Vector(sprite.pivot, e.pivot, "pivot"); Near(sprite.pixelsPerUnit, e.ppu, "PPU");
            Near(sprite.border.x, e.border.x, "border.x"); Near(sprite.border.y, e.border.y, "border.y");
            Near(sprite.border.z, e.border.z, "border.z"); Near(sprite.border.w, e.border.w, "border.w");
            Vector2[] vertices = sprite.vertices, uv = sprite.uv;
            Require(vertices.Length == e.vertices.Length && uv.Length == e.uv.Length, "original mesh lengths");
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector(vertices[i], e.vertices[i], "vertex " + i);
                Vector(uv[i], e.uv[i], "UV " + i);
            }
            Require(sprite.triangles.Select(value => (int)value).SequenceEqual(e.triangles), "original index order");
            Require(texture.width == e.width && texture.height == e.height && texture.mipmapCount == e.mip_count &&
                texture.format == TextureFormat.RGBA32 && !texture.isReadable, "original dimensions/mips/readability and uncompressed Windows format");
            Require((int)texture.filterMode == e.filter && texture.anisoLevel == e.aniso &&
                (int)texture.wrapModeU == e.wrapU && (int)texture.wrapModeV == e.wrapV, "original sampler settings");
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            Require(!request.hasError, "native texture GPU upload/readback");
            byte[] pixels = request.GetData<byte>().ToArray();
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(pixels)).Replace("-", "").ToLowerInvariant();
                Require(actual == e.rgba_sha256, e.name + " exact decoded ASTC RGBA bytes after GPU upload");
            }
        }
    }
}
