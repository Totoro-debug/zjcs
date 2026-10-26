using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleElementFeedbackAssetTools
    {
        private const string Root = "Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback/";

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data = JsonUtility.FromJson<BattleElementFeedbackData>(File.ReadAllText(Root+"element-feedback.json"));
            if (data.manifest != "854_156383") throw new InvalidDataException("Unpinned element feedback source.");
            foreach (var entry in data.textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Root+entry.key+".png");
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
                string path = Root+entry.key+".mat";
                var shader = Shader.Find(entry.shader);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidDataException("Original world shader failed: "+entry.shader);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader = shader;
                material.name = entry.name;
                material.renderQueue = entry.queue;
                material.shaderKeywords = entry.keywords;
                foreach (var scalar in entry.floats) material.SetFloat(scalar.name,scalar.value);
                foreach (var color in entry.colors)
                    material.SetColor(color.name,new Color(color.rgba[0],color.rgba[1],color.rgba[2],color.rgba[3]));
                foreach (var texture in entry.textures)
                {
                    material.SetTexture(texture.name,string.IsNullOrEmpty(texture.texture) ? null
                        : AssetDatabase.LoadAssetAtPath<Texture2D>(Root+texture.texture+".png"));
                    material.SetTextureScale(texture.name,new Vector2(texture.scale[0],texture.scale[1]));
                    material.SetTextureOffset(texture.name,new Vector2(texture.offset[0],texture.offset[1]));
                }
                EditorUtility.SetDirty(material);
            }
            foreach (var entry in data.sprites)
            {
                string path = Root+entry.key+".asset";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var border = new Vector4(entry.border[0],entry.border[1],entry.border[2],entry.border[3]);
                if (sprite != null && sprite.border != border)
                {
                    AssetDatabase.DeleteAsset(path);
                    sprite = null;
                }
                if (sprite == null)
                {
                    sprite = Sprite.Create(AssetDatabase.LoadAssetAtPath<Texture2D>(Root+entry.texture+".png"),
                        entry.rect.Value,entry.pivot.Value,entry.pixelsPerUnit,0,SpriteMeshType.FullRect,border);
                    sprite.name = entry.key;
                    AssetDatabase.CreateAsset(sprite,path);
                }
                sprite.OverrideGeometry(entry.vertices.Select(value => value.Value).ToArray(),entry.triangles);
                EditorUtility.SetDirty(sprite);
            }
            foreach (var entry in data.fonts)
            {
                using (var hash = SHA256.Create())
                    if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(entry.sourcePath))).Replace("-", "").ToLowerInvariant()!=entry.sourceSha256)
                        throw new InvalidDataException("The original streaming source font changed.");
                string path = Root+entry.key+".asset";
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(entry.sourcePath),
                        entry.pointSize,entry.padding,(GlyphRenderMode)entry.renderMode,entry.width,entry.height,
                        (AtlasPopulationMode)entry.population,entry.multiAtlas);
                    var temporaryMaterial = font.material;
                    AssetDatabase.CreateAsset(font,path);
                    AssetDatabase.AddObjectToAsset(font.atlasTextures[0],font);
                    UnityEngine.Object.DestroyImmediate(temporaryMaterial);
                }
                font.name = entry.name;
                JsonUtility.FromJsonOverwrite(entry.fontJson,font);
                var atlas = font.atlasTextures[0];
                atlas.Reinitialize(entry.width,entry.height,TextureFormat.Alpha8,false);
                atlas.SetPixels32(AssetDatabase.LoadAssetAtPath<Texture2D>(Root+entry.atlasTextures[0]+".png").GetPixels32());
                atlas.Apply(false,false);
                font.material = AssetDatabase.LoadAssetAtPath<Material>(Root+entry.material+".mat");
                font.material.mainTexture = atlas;
                font.atlasPopulationMode = (AtlasPopulationMode)entry.population;
                font.isMultiAtlasTexturesEnabled = entry.multiAtlas;
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = entry.clearOnBuild;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.ReadFontAssetDefinition();
                if (!font.TryAddCharacters(entry.warmCharacters,out string missing))
                    throw new InvalidDataException("Original source font lacks element feedback characters: "+missing);
                EditorUtility.SetDirty(font);
                EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(font.material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Original element feedback assets imported: "+data.bindings.Length+" bindings, "+data.materials.Length+" materials and original dynamic font.");
        }
    }
}
