using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleHudMaterialTools
    {
        private const string Root = "Assets/Official/Combat/HudMaterials";
        private const string Source = Root + "/Resources/Combat/battle-hud-materials.json";
        private const string Catalog = Root + "/Resources/Combat/OfficialBattleHudMaterialAssets.asset";
        [Serializable] private sealed class Output { public string path, sha256; }
        [Serializable] private sealed class Receipt { public string manifest, input_sha256, importer_sha256; public Output[] outputs; }
        private static BattleHudMaterialSource Read() => JsonUtility.FromJson<BattleHudMaterialSource>(File.ReadAllText(Source));

        [MenuItem("Zjcs/Combat/Restore Main HUD Image Materials")]
        public static void Restore()
        {
            AssetDatabase.Refresh();
            BattleHudMaterialSource data = Read();
            foreach (BattleHudMaterialTexture texture in data.textures)
            {
                var importer = AssetImporter.GetAtPath(texture.path) as TextureImporter;
                if (importer == null) throw new InvalidDataException("Original HUD texture did not import: " + texture.path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = texture.sRGB;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 8192;
                importer.filterMode = (FilterMode)texture.filter;
                importer.wrapModeU = (TextureWrapMode)texture.wrapU;
                importer.wrapModeV = (TextureWrapMode)texture.wrapV;
                importer.anisoLevel = texture.aniso;
                importer.mipMapBias = texture.bias;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory(Root + "/Native");
            foreach (BattleTimelineMaterial source in data.materials)
            {
                BattleHudMaterialShader shader = data.shaders.Single(s => s.key == source.shader);
                var native = AssetDatabase.LoadAssetAtPath<Shader>(shader.path);
                if (native == null || ShaderUtil.ShaderHasError(native))
                    throw new InvalidDataException("Original HUD shader conversion did not compile: " + shader.path + "\n"
                        + (native == null ? "Missing Shader asset" : string.Join("\n", ShaderUtil.GetShaderMessages(native)
                            .Select(m => m.severity + " " + m.file + ":" + m.line + " " + m.message))));
                var material = new Material(native) { name = source.name, renderQueue = source.queue };
                foreach (var value in source.floats) if (material.HasProperty(value.name)) material.SetFloat(value.name, value.value);
                foreach (var value in source.colors) if (material.HasProperty(value.name))
                    material.SetColor(value.name, new Color(value.value[0], value.value[1], value.value[2], value.value[3]));
                foreach (var value in source.textures)
                {
                    if (!material.HasProperty(value.name)) continue;
                    Texture2D texture = string.IsNullOrEmpty(value.texture) ? null
                        : AssetDatabase.LoadAssetAtPath<Texture2D>(data.textures.Single(t => t.key == value.texture).path);
                    material.SetTexture(value.name, texture);
                    material.SetTextureScale(value.name, new Vector2(value.scale[0], value.scale[1]));
                    material.SetTextureOffset(value.name, new Vector2(value.offset[0], value.offset[1]));
                }
                material.shaderKeywords = source.keywords;
                Save(material, Root + "/Native/" + source.name + ".mat");
            }
            var catalog = ScriptableObject.CreateInstance<OfficialBattleHudMaterialAssets>();
            catalog.SourceSha256 = Sha(File.ReadAllBytes(Source));
            catalog.bindings = data.bindings.Select(b => new BattleHudMaterialBinding
            {
                path = b.path, material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Native/"
                    + data.materials.Single(m => m.key == b.material).name + ".mat")
            }).ToArray();
            Save(catalog, Catalog);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Validate();
            string receipt = Path.GetFullPath("../assets/reference/battle-hud/materials-native-source-manifest.json");
            File.WriteAllText(receipt, JsonUtility.ToJson(new Receipt
            {
                manifest = data.manifest, input_sha256 = Sha(File.ReadAllBytes(Source)),
                importer_sha256 = Sha(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialBattleHudMaterialTools.cs")),
                outputs = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => new Output { path = "unity/" + p.Replace('\\', '/'), sha256 = Sha(File.ReadAllBytes(p)) }).ToArray()
            }, true) + "\n");
            Debug.Log("Original battle main HUD materials restored: sequence, additive and capsule border flow.");
        }

        [MenuItem("Zjcs/Combat/Validate Main HUD Image Materials")]
        public static void Validate()
        {
            BattleHudMaterialSource data = Read();
            OfficialBattleHudMaterialAssets catalog = OfficialBattleHudMaterialAssets.Load();
            if (catalog.SourceSha256 != Sha(File.ReadAllBytes(Source)) || catalog.bindings.Length != data.bindings.Length)
                throw new InvalidDataException("Battle HUD material source bindings changed.");
            var variants = new ShaderVariantCollection();
            foreach (BattleHudMaterialShader shader in data.shaders)
            {
                Shader native = AssetDatabase.LoadAssetAtPath<Shader>(shader.path);
                foreach (string[] keywords in new[] { Array.Empty<string>(), new[] { "UNITY_UI_CLIP_RECT" },
                    new[] { "UNITY_UI_ALPHACLIP" }, new[] { "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP" } })
                    variants.Add(new ShaderVariantCollection.ShaderVariant(native, UnityEngine.Rendering.PassType.Normal, keywords));
            }
            variants.WarmUp();
            UnityEngine.Object.DestroyImmediate(variants);
            foreach (BattleHudMaterialTexture texture in data.textures)
            {
                Texture2D native = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.path);
                var importer = AssetImporter.GetAtPath(texture.path) as TextureImporter;
                if (native == null || importer == null || native.width != texture.width || native.height != texture.height
                    || Sha(File.ReadAllBytes(texture.path)) != texture.sha256 || importer.sRGBTexture != texture.sRGB
                    || importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || native.filterMode != (FilterMode)texture.filter || native.wrapModeU != (TextureWrapMode)texture.wrapU
                    || native.wrapModeV != (TextureWrapMode)texture.wrapV || native.anisoLevel != texture.aniso
                    || !Mathf.Approximately(native.mipMapBias, texture.bias))
                    throw new InvalidDataException("Original HUD texture or sampling changed: " + texture.path);
            }
            foreach (BattleHudMaterialReference binding in data.bindings)
            {
                BattleTimelineMaterial source = data.materials.Single(m => m.key == binding.material);
                BattleHudMaterialShader shader = data.shaders.Single(s => s.key == source.shader);
                Material material = catalog.Find(binding.path);
                if (material == null || material.shader != AssetDatabase.LoadAssetAtPath<Shader>(shader.path)
                    || ShaderUtil.ShaderHasError(material.shader) || material.renderQueue != (source.queue < 0 ? material.shader.renderQueue : source.queue)
                    || !material.shaderKeywords.OrderBy(k => k).SequenceEqual(source.keywords.OrderBy(k => k)))
                    throw new InvalidDataException("Original HUD material or shader did not bind: " + binding.path);
                foreach (var value in source.floats)
                    if (material.HasProperty(value.name) && !Mathf.Approximately(material.GetFloat(value.name), value.value))
                        throw new InvalidDataException("Original HUD material float changed: " + source.name + "/" + value.name);
                foreach (var value in source.colors)
                    if (material.HasProperty(value.name) && material.GetColor(value.name) != new Color(value.value[0], value.value[1], value.value[2], value.value[3]))
                        throw new InvalidDataException("Original HUD material color changed: " + source.name + "/" + value.name);
                foreach (var value in source.textures)
                {
                    if (!material.HasProperty(value.name)) continue;
                    string expected = string.IsNullOrEmpty(value.texture) ? "" : data.textures.Single(t => t.key == value.texture).path;
                    if (AssetDatabase.GetAssetPath(material.GetTexture(value.name)) != expected
                        || material.GetTextureScale(value.name) != new Vector2(value.scale[0], value.scale[1])
                        || material.GetTextureOffset(value.name) != new Vector2(value.offset[0], value.offset[1]))
                        throw new InvalidDataException("Original HUD texture binding changed: " + source.name);
                }
            }
            var parent = new GameObject("BattleHudMaterialValidation", typeof(RectTransform));
            try
            {
                var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault());
                renderer.Create("main", parent.transform);
                foreach (BattleHudMaterialReference binding in data.bindings)
                    if (renderer.Find(binding.path).GetComponent<Image>().material != catalog.Find(binding.path))
                        throw new InvalidDataException("Original Image material was omitted by HUD construction: " + binding.path);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
            Debug.Log("Battle HUD Image material validation passed: three original bindings, parameters, shaders and texture transforms.");
        }

        public static System.Collections.IEnumerator VerifyInPlayMode()
        {
            BattleHudMaterialSource data = Read();
            var images = UnityEngine.Object.FindObjectsOfType<Image>(true);
            Image sequence = null;
            foreach (BattleHudMaterialReference binding in data.bindings)
            {
                Material expected = OfficialBattleHudMaterialAssets.Load().Find(binding.path);
                Image image = images.SingleOrDefault(i => i.material == expected);
                if (image == null) throw new InvalidOperationException("Runtime Image did not retain its original material: " + binding.path);
                if (expected.shader.name.EndsWith("HudEffectSequeue", StringComparison.Ordinal)) sequence = image;
            }
            if (sequence == null || !sequence.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Sequence material probe requires the original visible skill-plan hint.");
            yield return new WaitForEndOfFrame();
            var corners = new Vector3[4];
            sequence.rectTransform.GetWorldCorners(corners);
            Texture2D first = ScreenCapture.CaptureScreenshotAsTexture();
            yield return new WaitForSecondsRealtime(.23f);
            yield return new WaitForEndOfFrame();
            Texture2D second = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(corners[0].x), 0, first.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(corners[0].y), 0, first.height - 1);
                int width = Mathf.Clamp(Mathf.CeilToInt(corners[2].x) - x, 1, first.width - x);
                int height = Mathf.Clamp(Mathf.CeilToInt(corners[2].y) - y, 1, first.height - y);
                Color[] before = first.GetPixels(x, y, width, height);
                Color[] after = second.GetPixels(x, y, width, height);
                int changed = before.Zip(after, (a, b) => Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b))
                    .Count(difference => difference > .1f);
                if (changed < 8) throw new InvalidOperationException("Original 4x4 recycle sequence did not animate on screen: " + changed);
                Debug.Log("Battle HUD Image material Play Mode passed: three original bindings; sequence changed " + changed + " screen pixels.");
            }
            finally { UnityEngine.Object.Destroy(first); UnityEngine.Object.Destroy(second); }
        }

        private static void Save(UnityEngine.Object asset, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null) AssetDatabase.CreateAsset(asset, path);
            else { EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(asset); }
        }
        private static string Sha(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
