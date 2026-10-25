using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.CharacterCreation
{
    public static class OpeningDamageAssetTools
    {
        private const string Root = "Assets/Official/Opening/Damage/Resources/OpeningDamage/";
        [Serializable] private sealed class Payload { public string manifest; public BitmapFont[] fonts; }
        [Serializable] private sealed class BitmapFont
        {
            public string name;
            public float size, lineSpacing, ascent, descent;
            public Glyph[] glyphs;
        }
        [Serializable] private sealed class Glyph
        {
            public int index;
            public Rectangle uv, vert;
            public float advance;
            public bool flipped;
        }
        [Serializable] private sealed class Rectangle { public float x, y, width, height; public Rect Value => new Rect(x,y,width,height); }

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var payload = JsonUtility.FromJson<Payload>(File.ReadAllText(Root + "fonts.json"));
            if (payload.manifest != "854_156383") throw new InvalidDataException("Unpinned damage bitmap fonts.");
            foreach (var entry in payload.fonts)
            {
                string texturePath = Root + entry.name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                string materialPath = Root + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Zjcs/Opening/DamageBitmap"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader=Shader.Find("Zjcs/Opening/DamageBitmap");
                Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(texture==null) throw new InvalidDataException("Official damage atlas failed to import: "+texturePath);
                material.mainTexture = texture;
                EditorUtility.SetDirty(material);
                string fontPath = Root + entry.name + ".fontsettings";
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null) { font = new Font(entry.name); AssetDatabase.CreateAsset(font, fontPath); }
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_FontSize").floatValue = entry.size;
                serialized.FindProperty("m_LineSpacing").floatValue = entry.lineSpacing;
                serialized.FindProperty("m_Ascent").floatValue = entry.ascent;
                serialized.FindProperty("m_Descent").floatValue = entry.descent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                font.material = material;
                var glyphs = new CharacterInfo[entry.glyphs.Length];
                for (int i=0;i<glyphs.Length;i++)
                {
                    var glyph = entry.glyphs[i];
#pragma warning disable 0618
                    glyphs[i] = new CharacterInfo { index=glyph.index, uv=glyph.uv.Value, vert=glyph.vert.Value,
                        advance=Mathf.RoundToInt(glyph.advance), flipped=glyph.flipped, size=Mathf.RoundToInt(entry.size) };
#pragma warning restore 0618
                }
                font.characterInfo = glyphs;
                EditorUtility.SetDirty(font);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
