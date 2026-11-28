using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSkillQualityAcceptance
    {
        [Serializable] sealed class Source { public string manifest; public SourceSprite[] sprites; }
        [Serializable] sealed class Identity { public string path; }
        [Serializable] sealed class RectData { public float x, y, width, height; }
        [Serializable] sealed class Expected
        {
            public RectData rect; public Vector2 pivot; public float ppu; public Vector4 border;
            public Vector2[] vertices, uv; public int[] triangles;
        }
        [Serializable] sealed class AtlasData { public RectData textureRect; public Vector2 textureRectOffset; }
        [Serializable] sealed class Atlas { public AtlasData render_data; }
        [Serializable] sealed class Mip { public int level, rgba_size; public string rgba_sha256; }
        [Serializable] sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV; public float m_MipBias; }
        [Serializable] sealed class TextureFields { public int m_Width, m_Height, m_MipCount; public Sampler m_TextureSettings; }
        [Serializable] sealed class TextureSource { public TextureFields original_fields; public Mip[] mips; }
        [Serializable] sealed class SourceSprite { public int quality; public Identity sprite; public string texture_path; public Expected expected; public Atlas atlas; public TextureSource texture; }

        [MenuItem("Zjcs/Combat/Validate Original Skill Qualities")]
        public static void Run()
        {
            OfficialBattleSkillQualityCatalog catalog = OfficialBattleSkillQualityCatalog.Load();
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("qualities");
            Require(entries.arraySize == 6, "Six original quality Sprites were not bound.");
            string root = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/battle-hud/skill-quality-source-manifest.json")));
            Require(source.manifest == "911_156995" && source.sprites.Length == entries.arraySize, "Current original quality source is required.");
            var checkedTextures = new System.Collections.Generic.HashSet<Texture2D>();
            var originalBindings = new System.Collections.Generic.List<BattleSkillQualityBinding>();
            var imageObject = new GameObject("SkillQualityGeometryFixture", typeof(RectTransform), typeof(Image));
            try
            {
                var image = imageObject.GetComponent<Image>();
                image.rectTransform.sizeDelta = new Vector2(184, 184);
                var geometry = imageObject.AddComponent<OfficialBattleSkillQualityGeometry>();
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    var binding = new BattleSkillQualityBinding
                    {
                        quality = entry.FindPropertyRelative("quality").intValue,
                        sprite = (Sprite)entry.FindPropertyRelative("sprite").objectReferenceValue,
                        sourceSize = entry.FindPropertyRelative("sourceSize").vector2Value,
                        trimOffset = entry.FindPropertyRelative("trimOffset").vector2Value,
                        trimSize = entry.FindPropertyRelative("trimSize").vector2Value,
                        uvMin = entry.FindPropertyRelative("uvMin").vector2Value,
                        uvMax = entry.FindPropertyRelative("uvMax").vector2Value
                    };
                    Require(binding.quality == index + 1 && binding.sprite == catalog.FindQuality(index + 1),
                        "Original quality ordering differs.");
                    Require(binding.sourceSize == new Vector2(184, 184) && binding.sprite.pixelsPerUnit == 100,
                        "The original untrimmed size or PPU was lost.");
                    var original = source.sprites.Single(row => row.quality == binding.quality);
                    var expected = original.expected;
                    Require(binding.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(original.sprite.path.Substring("unity/".Length))
                        && binding.sprite.texture == AssetDatabase.LoadAssetAtPath<Texture2D>(original.texture_path.Substring("unity/".Length))
                        && binding.sprite.rect == new Rect(expected.rect.x, expected.rect.y, expected.rect.width, expected.rect.height)
                        && binding.sprite.pivot == expected.pivot && binding.sprite.border == expected.border
                        && binding.sprite.pixelsPerUnit == expected.ppu
                        && binding.sprite.triangles.Select(value => (int)value).SequenceEqual(expected.triangles)
                        && binding.sprite.vertices.Length == expected.vertices.Length && binding.sprite.uv.Length == expected.uv.Length,
                        "The current original native Sprite binding or mesh differs.");
                    for (int i = 0; i < expected.vertices.Length; i++)
                        Require(Vector2.Distance(binding.sprite.vertices[i], expected.vertices[i]) < .000001f
                            && Vector2.Distance(binding.sprite.uv[i], expected.uv[i]) < .000001f, "Original Sprite mesh or UV differs.");
                    var packed = original.atlas.render_data;
                    var fields = original.texture.original_fields;
                    var lower = new Vector2(packed.textureRect.x / fields.m_Width, packed.textureRect.y / fields.m_Height);
                    var upper = lower + new Vector2(packed.textureRect.width / fields.m_Width, packed.textureRect.height / fields.m_Height);
                    Require(binding.sourceSize == new Vector2(expected.rect.width, expected.rect.height)
                        && binding.trimOffset == packed.textureRectOffset && binding.trimSize == new Vector2(packed.textureRect.width, packed.textureRect.height)
                        && Vector2.Distance(binding.uvMin, lower) < .000001f && Vector2.Distance(binding.uvMax, upper) < .000001f,
                        "Simple Image geometry differs from the current original packed atlas rectangle.");
                    if (checkedTextures.Add(binding.sprite.texture)) VerifyTexture(binding.sprite.texture, original.texture);
                    image.sprite = binding.sprite;
                    geometry.Bind(binding);
                    originalBindings.Add(binding);
                    using (var vertices = new VertexHelper())
                    {
                        UIVertex vertex = UIVertex.simpleVert;
                        vertex.color = new Color32(31, 73, 127, 211);
                        vertices.AddVert(vertex);
                        geometry.ModifyMesh(vertices);
                        Require(vertices.currentVertCount == 4 && vertices.currentIndexCount == 6,
                            "The original Simple Image quad differs.");
                        vertices.PopulateUIVertex(ref vertex, 0);
                        Require(Vector2.Distance(vertex.position, new Vector2(-92, -92) + binding.trimOffset) < .001f
                            && Vector2.Distance(vertex.uv0, binding.uvMin) < .00001f
                            && vertex.color.Equals(new Color32(31, 73, 127, 211)),
                            "Original trim, fractional UV or graphic color was lost.");
                        vertices.PopulateUIVertex(ref vertex, 2);
                        Require(Vector2.Distance(vertex.position, new Vector2(-92, -92) + binding.trimOffset + binding.trimSize) < .001f
                            && Vector2.Distance(vertex.uv0, binding.uvMax) < .00001f,
                            "Original quality upper bounds differ: quality=" + binding.quality + " position=" + vertex.position.ToString("R")
                            + " uv=" + vertex.uv0.ToString("R") + " expected=" + binding.uvMax.ToString("R")
                            + " spriteRect=" + binding.sprite.rect + " texture=" + binding.sprite.texture.width
                            + "x" + binding.sprite.texture.height + " outer=" + UnityEngine.Sprites.DataUtility.GetOuterUV(binding.sprite).ToString("R"));
                        if (binding.quality == 2)
                            Require(Mathf.Abs(vertex.position.x - 90.92387f) < .001f
                                && Mathf.Abs(vertex.position.y - 91.92387f) < .001f,
                                "Current original Blue Sprite golden coordinates differ.");
                    }
                }
                foreach (var sample in new[] { (10000, 2), (10001, 3), (10003, 4), (14525, 5), (39501, 6) })
                {
                    catalog.Apply(image, sample.Item1);
                    Require(image.sprite == catalog.FindQuality(sample.Item2), "Official item quality changed: " + sample.Item1);
                }
                bool rejected = false;
                try { catalog.Find(16019); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Quality Rainbow was silently mapped to an unconfirmed substitute.");
                Capture(originalBindings.ToArray());
                Debug.Log("Current original skill qualities passed: six shared native Sprites, original mesh/UVs and complete GPU atlas bytes, five item-quality mappings, 184px padding/fractional UVs/color, and explicit unsupported Rainbow.");
            }
            finally { UnityEngine.Object.DestroyImmediate(imageObject); }
        }

        static void VerifyTexture(Texture2D texture, TextureSource source)
        {
            var fields = source.original_fields; var sampler = fields.m_TextureSettings;
            Require(texture.format == TextureFormat.RGBA32 && texture.width == fields.m_Width && texture.height == fields.m_Height
                && texture.mipmapCount == fields.m_MipCount && source.mips.Length == fields.m_MipCount
                && (int)texture.filterMode == sampler.m_FilterMode && texture.anisoLevel == sampler.m_Aniso
                && texture.mipMapBias == sampler.m_MipBias && (int)texture.wrapModeU == sampler.m_WrapU && (int)texture.wrapModeV == sampler.m_WrapV,
                "Original full atlas dimensions, mip count or sampler differs.");
            foreach (var mip in source.mips)
            {
                var request = AsyncGPUReadback.Request(texture, mip.level, TextureFormat.RGBA32); request.WaitForCompletion();
                Require(!request.hasError, "Quality atlas GPU readback failed.");
                byte[] raw = request.GetData<byte>().ToArray();
                using (var hash = SHA256.Create())
                    Require(raw.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256,
                        "Original complete quality atlas GPU pixels differ.");
            }
        }

        static void Capture(BattleSkillQualityBinding[] bindings)
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json"))) return;
            var cameraObject = new GameObject("MutedQualityAtlasCamera", typeof(Camera));
            var canvasObject = new GameObject("QualityAtlasFixture", typeof(RectTransform), typeof(Canvas));
            var target = new RenderTexture(768, 768, 24); target.Create(); Texture2D pixels = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .09f, .09f);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                for (int q = 1; q <= 6; q++)
                {
                    var image = new GameObject("OriginalQuality" + q, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(canvas.transform, false); image.rectTransform.sizeDelta = new Vector2(184, 184);
                    image.rectTransform.anchoredPosition = new Vector2(((q - 1) % 3 - 1) * 230, (q <= 3 ? 1 : -1) * 130);
                    image.sprite = bindings[q - 1].sprite;
                    image.gameObject.AddComponent<OfficialBattleSkillQualityGeometry>().Bind(bindings[q - 1]);
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
                try { pixels = new Texture2D(768, 768, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); pixels.Apply(); }
                finally { RenderTexture.active = previous; }
                Require(pixels.GetPixels32().Count(pixel => pixel.r > 70 || pixel.g > 70 || pixel.b > 70) > 1000, "Six quality frames did not render.");
                File.WriteAllBytes(Path.Combine(work, "current-battle-quality-frames.png"), pixels.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject); UnityEngine.Object.DestroyImmediate(cameraObject);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public static void VerifyActual(GameObject root, Zjcs.Combat.Official.FightBattleCheckpoint checkpoint)
        {
            OfficialBattleSkillQualityCatalog catalog = OfficialBattleSkillQualityCatalog.Load();
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            foreach (var skill in player.Skills)
            {
                WorldGameSkillIcon card = root.GetComponentsInChildren<WorldGameSkillIcon>(true)
                    .Single(value => value.SkillIndex == skill.Slot);
                Require(card.QualityImg.sprite == catalog.Find(skill.ItemId)
                    && card.QualityImg.GetComponent<OfficialBattleSkillQualityGeometry>() != null,
                    "The actual battle skill did not bind its original item quality and geometry.");
            }
            Debug.Log("Original skill quality Play Mode passed: actual skill item quality and original geometry are bound.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
