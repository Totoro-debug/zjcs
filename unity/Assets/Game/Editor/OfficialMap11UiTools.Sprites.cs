using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        public static void ValidateCommonSprite()
        {
            AssetDatabase.Refresh();
            OfficialMap11UiData data = JsonUtility.FromJson<OfficialMap11UiData>(
                File.ReadAllText(SourceRoot + "/map11-ui.json"));
            RestoreCommonSpriteAsset(data.commonSpriteAsset);
            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(ResourceRoot + "/Common.asset");
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetSimpleHashCode(asset.name), asset);
            var canvas = new GameObject("Map11SpriteValidation", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var textObject = new GameObject("OriginalSpriteTag", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(canvas.transform, false);
                TMP_Text text = textObject.GetComponent<TMP_Text>();
                text.rectTransform.sizeDelta = new Vector2(256, 128);
                text.font = Zjcs.Presentation.Combat.OfficialBattleHudCatalog.LoadDefault().Font;
                text.text = "<sprite=\"Common\" index=3>";
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate(true, true);
                if (!text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(value =>
                        value.elementType == TMP_TextElementType.Sprite && value.isVisible
                        && value.topRight.x > value.bottomLeft.x) || asset.spriteSheet == null
                    || asset.material.mainTexture != asset.spriteSheet)
                    throw new InvalidDataException("Official named Common sprite did not produce a textured sprite glyph: count="
                        + text.textInfo.spriteCount + ", parsed=" + text.GetParsedText() + ", atlas=" + asset.spriteSheet
                        + ", material=" + asset.material.mainTexture);
                Debug.Log("Map11 Common sprite validation passed: named tag, original glyph, atlas and material binding.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        private static void RestoreCommonSpriteAsset(OfficialMap11TmpSpriteAsset source)
        {
            if (source == null || source.name != "Common" || source.glyphs.Length != 7 || source.characters.Length != 7)
                throw new InvalidDataException("Official Common sprite asset is incomplete.");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source.texturePath))).Replace("-", "").ToLowerInvariant()
                    != source.textureSha256) throw new InvalidDataException("Official Common sprite atlas differs.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(source.texturePath);
            string previousSettings = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (EditorJsonUtility.ToJson(importer) != previousSettings) importer.SaveAndReimport();
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(source.texturePath);
            if (atlas == null || atlas.width != 400 || atlas.height != 200)
                throw new InvalidDataException("Official Common sprite atlas did not import as a 400x200 texture: "
                    + source.texturePath + ", " + (atlas == null ? "null" : atlas.width + "x" + atlas.height));
            string assetPath = ResourceRoot + "/Common.asset";
            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.name = source.name;
            asset.hashCode = source.hashCode;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = source.version;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.spriteSheet = atlas;
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            foreach (OfficialMap11TmpGlyph glyph in source.glyphs)
                asset.spriteGlyphTable.Add(new TMP_SpriteGlyph(glyph.index,
                    new GlyphMetrics(glyph.metrics[0], glyph.metrics[1], glyph.metrics[2], glyph.metrics[3], glyph.metrics[4]),
                    new GlyphRect(glyph.rect[0], glyph.rect[1], glyph.rect[2], glyph.rect[3]), glyph.scale, glyph.atlasIndex));
            foreach (OfficialMap11TmpCharacter character in source.characters)
            {
                var entry = new TMP_SpriteCharacter(character.unicode, asset,
                    asset.spriteGlyphTable.Single(value => value.index == character.glyphIndex))
                    { name = character.name, scale = character.scale };
                if (entry.hashCode != character.hashCode) throw new InvalidDataException("Common sprite name hash differs.");
                asset.spriteCharacterTable.Add(entry);
            }
            string materialPath = ResourceRoot + "/Common.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Sprite"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = asset.spriteSheet;
            material.SetColor("_Color", Color.white);
            material.SetVector("_ClipRect", new Vector4(-32767, -32767, 32767, 32767));
            material.SetFloat("_ColorMask", 15);
            material.SetFloat("_CullMode", 0);
            material.SetFloat("_Stencil", 0);
            material.SetFloat("_StencilComp", 8);
            material.SetFloat("_StencilOp", 0);
            material.SetFloat("_StencilReadMask", 255);
            material.SetFloat("_StencilWriteMask", 255);
            material.SetFloat("_UseUIAlphaClip", 0);
            asset.material = material;
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
