using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    /// <summary>Validate installed native images against their actual conversion receipts.</summary>
    public static class OfficialCurrentNativeImageValidation
    {
        public sealed class Images
        {
            public readonly Dictionary<string, Sprite> ByPath = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            public readonly Dictionary<string, Sprite> ByAlias = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        }

        [Serializable] sealed class Proof
        {
            public string manifest;
            public Output[] outputs;
            public NativeSprite[] sprites;
            public NativeTexture[] textures;
            public Alias[] bindings, hud_aliases;
        }
        [Serializable] sealed class Output { public string path, sha256; }
        [Serializable] sealed class NativeSprite { public string path, guid, texture; public Expected expected; }
        [Serializable] sealed class Expected
        {
            public string path, name, guid;
            public Rectangle rect;
            public Vector2 pivot;
            public Vector4 border;
            public float ppu;
            public Vector2[] vertices, uv;
            public int[] triangles;
            public Shape[] physics_shapes;
        }
        [Serializable] sealed class Rectangle { public float x, y, width, height; }
        [Serializable] sealed class Shape { public Vector2[] points; }
        [Serializable] sealed class NativeTexture
        {
            public string path, guid, identity;
            public int format;
            public TextureFields original_fields;
            public Mip[] mips;
        }
        [Serializable] sealed class TextureFields
        {
            public int m_Width, m_Height;
            public bool m_IsReadable;
            public Sampler m_TextureSettings;
        }
        [Serializable] sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] sealed class Mip { public int level, width, height; }
        [Serializable] sealed class Alias { public string file; public Pointer pointer; }
        [Serializable] sealed class Pointer { public string guid; public int fileID, type; }

        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        public static string NpcReceipt => Path.Combine(RepositoryRoot, "assets/reference/npc-icons-source-manifest.json");
        public static string HudReceipt => Path.Combine(RepositoryRoot, "assets/reference/battle-hud/current-images-source-manifest.json");

        public static Images ValidateAll()
        {
            var result = Validate(NpcReceipt);
            Merge(result, Validate(HudReceipt));
            return result;
        }

        public static Images Validate(string receiptPath)
        {
            var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(receiptPath));
            Require(proof != null && proof.manifest == "911_156995" && proof.outputs != null
                && proof.sprites != null && proof.textures != null, "Current native image receipt is incomplete: " + receiptPath);
            foreach (var row in proof.outputs)
            {
                string path = UnityPath(row.path);
                using (var hash = SHA256.Create())
                    Require(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(PhysicalPath(path))))
                        .Replace("-", "").ToLowerInvariant() == row.sha256, "Current native image bytes changed: " + path);
            }
            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var row in proof.textures)
            {
                string path = UnityPath(row.path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var fields = row.original_fields;
                Require(texture != null && AssetDatabase.AssetPathToGUID(path) == row.guid
                    && texture.width == fields.m_Width && texture.height == fields.m_Height
                    && texture.mipmapCount == row.mips.Length && (int)texture.format == row.format
                    && texture.isReadable == fields.m_IsReadable, "Current native texture topology changed: " + path);
                var sampler = fields.m_TextureSettings;
                Require((int)texture.filterMode == sampler.m_FilterMode && texture.anisoLevel == sampler.m_Aniso
                    && (int)texture.wrapModeU == sampler.m_WrapU && (int)texture.wrapModeV == sampler.m_WrapV
                    && (int)texture.wrapModeW == sampler.m_WrapW, "Current native texture sampler changed: " + path);
                Near(texture.mipMapBias, sampler.m_MipBias, path + " mip bias");
                textures.Add(row.identity, texture);
            }
            var result = new Images();
            var byGuid = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var row in proof.sprites)
            {
                string path = UnityPath(row.path);
                var expected = row.expected;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprite != null && path == expected.path && sprite.name == expected.name
                    && row.guid == expected.guid && AssetDatabase.AssetPathToGUID(path) == row.guid
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id) && id == 21300000,
                    "Current native Sprite identity changed: " + path);
                Near(sprite.rect.x, expected.rect.x, path + " rect x"); Near(sprite.rect.y, expected.rect.y, path + " rect y");
                Near(sprite.rect.width, expected.rect.width, path + " rect width"); Near(sprite.rect.height, expected.rect.height, path + " rect height");
                Point(sprite.pivot, expected.pivot, path + " pivot"); Near(sprite.pixelsPerUnit, expected.ppu, path + " PPU");
                for (int axis = 0; axis < 4; axis++) Near(sprite.border[axis], expected.border[axis], path + " border");
                Require(sprite.vertices.Length == expected.vertices.Length && sprite.uv.Length == expected.uv.Length,
                    "Current native Sprite geometry size changed: " + path);
                for (int i = 0; i < expected.vertices.Length; i++)
                { Point(sprite.vertices[i], expected.vertices[i], path + " vertex"); Point(sprite.uv[i], expected.uv[i], path + " UV"); }
                Require(sprite.triangles.Select(value => (int)value).SequenceEqual(expected.triangles), "Current native Sprite triangles changed: " + path);
                Require(sprite.GetPhysicsShapeCount() == expected.physics_shapes.Length, "Current native Sprite physics shape count changed: " + path);
                for (int i = 0; i < expected.physics_shapes.Length; i++)
                {
                    var points = new List<Vector2>(); sprite.GetPhysicsShape(i, points);
                    Require(points.Count == expected.physics_shapes[i].points.Length, "Current native Sprite physics shape changed: " + path);
                    for (int p = 0; p < points.Count; p++) Point(points[p], expected.physics_shapes[i].points[p], path + " physics point");
                }
                Require(textures.TryGetValue(row.texture, out var texture) && sprite.texture == texture,
                    "Current native Sprite atlas binding changed: " + path);
                result.ByPath.Add(path, sprite); byGuid.Add(row.guid, sprite);
            }
            var catalog = OfficialBattleHudCatalog.LoadDefault();
            foreach (var row in (proof.bindings ?? Array.Empty<Alias>()).Concat(proof.hud_aliases ?? Array.Empty<Alias>()))
            {
                if (string.IsNullOrEmpty(row.file)) continue; // NPC address bindings are not HUD filename aliases.
                Require(row.pointer != null && row.pointer.fileID == 21300000 && row.pointer.type == 2
                    && byGuid.TryGetValue(row.pointer.guid, out var sprite) && catalog.FindSprite(row.file) == sprite,
                    "Current native HUD alias changed: " + row.file);
                result.ByAlias.Add(row.file, catalog.FindSprite(row.file));
            }
            return result;
        }

        static void Merge(Images target, Images source)
        {
            foreach (var row in source.ByPath) target.ByPath.Add(row.Key, row.Value);
            foreach (var row in source.ByAlias) target.ByAlias.Add(row.Key, row.Value);
        }
        static string UnityPath(string path)
        {
            Require(path != null && path.StartsWith("unity/Assets/", StringComparison.Ordinal), "Native image output is outside Unity Assets: " + path);
            string relative = path.Substring("unity/".Length);
            _ = PhysicalPath(relative);
            return relative;
        }
        static string PhysicalPath(string path)
        {
            string full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));
            Require(full.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "Native image path escapes Assets: " + path);
            return full;
        }
        static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
        static void Near(float actual, float expected, string reason) => Require(Mathf.Abs(actual - expected) <= .000001f, "Current native image " + reason);
        static void Point(Vector2 actual, Vector2 expected, string reason) { Near(actual.x, expected.x, reason + " x"); Near(actual.y, expected.y, reason + " y"); }
    }
}
