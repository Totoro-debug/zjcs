using System;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportGraphicsAssetTools
    {
        private const string Root = "Assets/Official/Combat/BattleReport/Graphics/";

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data = JsonUtility.FromJson<BattleElementFeedbackData>(File.ReadAllText(Root + "report-graphics.json"));
            if (data.manifest != "854_156383") throw new InvalidDataException("Unpinned report graphics source.");
            foreach (var entry in data.textures)
            {
                if (entry.width == 0 && entry.height == 0) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(Root + entry.key + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = entry.sRGB;
                importer.isReadable = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.filterMode = (FilterMode)entry.filter;
                importer.wrapModeU = (TextureWrapMode)entry.wrapU;
                importer.wrapModeV = (TextureWrapMode)entry.wrapV;
                importer.anisoLevel = entry.aniso;
                importer.mipMapBias = entry.bias;
                importer.SaveAndReimport();
            }
            foreach (var entry in data.materials)
            {
                var shader = Shader.Find(entry.shader);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidDataException("Original report shader failed: " + entry.shader);
                string path = Root + entry.key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.name = entry.name;
                material.renderQueue = entry.queue;
                material.shaderKeywords = entry.keywords;
                foreach (var scalar in entry.floats) material.SetFloat(scalar.name, scalar.value);
                foreach (var color in entry.colors)
                    material.SetColor(color.name, new Color(color.rgba[0], color.rgba[1], color.rgba[2], color.rgba[3]));
                foreach (var texture in entry.textures)
                {
                    material.SetTexture(texture.name, string.IsNullOrEmpty(texture.texture) ? null
                        : AssetDatabase.LoadAssetAtPath<Texture2D>(Root + texture.texture + ".png"));
                    material.SetTextureScale(texture.name, new Vector2(texture.scale[0], texture.scale[1]));
                    material.SetTextureOffset(texture.name, new Vector2(texture.offset[0], texture.offset[1]));
                }
                EditorUtility.SetDirty(material);
            }
            foreach (var entry in data.fonts)
            {
                using (var hash = SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(entry.sourcePath))).Replace("-", "").ToLowerInvariant() != entry.sourceSha256)
                        throw new InvalidDataException("The original report source font changed.");
                string path = Root + entry.key + ".asset";
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(entry.sourcePath),
                        entry.pointSize, entry.padding, (GlyphRenderMode)entry.renderMode, entry.width, entry.height,
                        (AtlasPopulationMode)entry.population, entry.multiAtlas);
                    var temporaryMaterial = font.material;
                    AssetDatabase.CreateAsset(font, path);
                    AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                    UnityEngine.Object.DestroyImmediate(temporaryMaterial);
                }
                font.name = entry.name;
                JsonUtility.FromJsonOverwrite(entry.fontJson, font);
                var atlas = font.atlasTextures[0];
                atlas.Reinitialize(entry.width, entry.height, TextureFormat.Alpha8, false);
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + entry.atlasTextures[0] + ".png");
                if (source != null) atlas.SetPixels32(source.GetPixels32());
                else atlas.SetPixels32(new Color32[entry.width * entry.height]);
                // MPLUS's original atlas is 0×0 with no glyphs. TMP allocates the configured empty atlas.
                atlas.Apply(false, false);
                font.material = AssetDatabase.LoadAssetAtPath<Material>(Root + entry.material + ".mat");
                font.material.mainTexture = atlas;
                font.atlasPopulationMode = (AtlasPopulationMode)entry.population;
                font.isMultiAtlasTexturesEnabled = entry.multiAtlas;
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = entry.clearOnBuild;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.ReadFontAssetDefinition();
                if (!font.TryAddCharacters(entry.warmCharacters, out string missing))
                    throw new InvalidDataException("Original source font lacks report characters: " + missing);
                EditorUtility.SetDirty(font);
                EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(font.material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Original report graphics dependencies imported: " + data.textures.Length + " texture records, "
                + data.fonts.Length + " fonts, " + data.materials.Length + " materials.");
        }
    }
}
