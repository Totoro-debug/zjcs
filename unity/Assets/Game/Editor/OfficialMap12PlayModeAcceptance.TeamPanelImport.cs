using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        [Serializable] private sealed class TeamPanelGeometryData
        { public TeamPanelGeometry[] sprites; public TeamPanelTexture[] textures; }
        [Serializable] private sealed class TeamPanelGeometry
        {
            public string path, guid, name;
            public TeamTargetRect rect;
            public Vector2 pivot;
            public float ppu;
            public Vector4 border;
            public Vector2[] vertices, uv;
            public int[] triangles;
        }
        [Serializable] private sealed class TeamPanelTexture
        { public string path; public int format; public TeamPanelTextureFields original_fields; public TeamPanelMip[] mips; }
        [Serializable] private sealed class TeamPanelTextureFields
        {
            public int m_Width, m_Height, m_MipCount, m_ColorSpace;
            public bool m_IsReadable;
            public TeamPanelSampler m_TextureSettings;
        }
        [Serializable] private sealed class TeamPanelSampler
        { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] private sealed class TeamPanelMip
        { public int level, rgba_size; public string rgba_sha256; }

        public static void VerifyTeamPanelNativeImport()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                VerifyTeamPanelNativeAssets();
                Debug.Log("Original TeamPanel native import passed full atlas GPU mip hashes, sampling, logical Sprite fields, vertices, indices, UV and original prefab script bindings.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void VerifyTeamPanelNativeAssets()
        {
            var data = JsonUtility.FromJson<TeamPanelGeometryData>(File.ReadAllText(
                "Assets/Official/World/Map/12/Source/team-panel-sprite-geometry.json"));
            foreach (var row in data.sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(row.path);
                SteleRequire(sprite != null && AssetDatabase.AssetPathToGUID(row.path) == row.guid
                    && sprite.name == row.name && sprite.rect == new Rect(row.rect.x, row.rect.y, row.rect.width, row.rect.height)
                    && sprite.pixelsPerUnit == row.ppu && Vector2.Distance(sprite.pivot, row.pivot) < 0.00001f
                    && Vector4.Distance(sprite.border, row.border) < 0.00001f,
                    "Original TeamPanel Sprite logical fields differ: " + row.path);
                SteleRequire(sprite.vertices.Length == row.vertices.Length
                    && sprite.vertices.Zip(row.vertices, Vector2.Distance).All(d => d < 0.00001f)
                    && sprite.triangles.Select(index => (int)index).SequenceEqual(row.triangles)
                    && sprite.uv.Length == row.uv.Length && sprite.uv.Zip(row.uv, Vector2.Distance).All(d => d < 0.00001f),
                    "Original TeamPanel Sprite geometry/atlas UV differs: " + row.path);
            }
            using (var hash = SHA256.Create())
            foreach (var row in data.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(row.path);
                var original = row.original_fields;
                var sampler = original.m_TextureSettings;
                SteleRequire(texture != null && texture.width == original.m_Width && texture.height == original.m_Height
                    && texture.mipmapCount == original.m_MipCount && (int)texture.format == row.format
                    && texture.isReadable == original.m_IsReadable && (int)texture.filterMode == sampler.m_FilterMode
                    && texture.anisoLevel == sampler.m_Aniso && texture.mipMapBias == sampler.m_MipBias
                    && (int)texture.wrapModeU == sampler.m_WrapU && (int)texture.wrapModeV == sampler.m_WrapV
                    && (int)texture.wrapModeW == sampler.m_WrapW
                    && texture.isDataSRGB == (original.m_ColorSpace == 1),
                    "Original TeamPanel atlas dimensions/sampler/color space differ: " + row.path);
                foreach (var mip in row.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    request.WaitForCompletion();
                    SteleRequire(!request.hasError, "Original TeamPanel GPU atlas readback failed: " + row.path);
                    byte[] bytes = request.GetData<byte>().ToArray();
                    string digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    SteleRequire(bytes.Length == mip.rgba_size && digest == mip.rgba_sha256,
                        "Original TeamPanel atlas mip pixels differ: " + row.path + " mip=" + mip.level + " actual=" + digest);
                }
            }
            foreach (string name in new[] { "TeamPanel", "TeamPanelItem", "TeamPanelDialogButtonGroup" })
            {
                var prefab = Resources.Load<GameObject>("Map12/Team/" + name);
                SteleRequire(prefab != null && prefab.GetComponentsInChildren<Transform>(true)
                    .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)) == 0,
                    "Original TeamPanel prefab contains missing scripts: " + name);
            }
        }
    }
}
