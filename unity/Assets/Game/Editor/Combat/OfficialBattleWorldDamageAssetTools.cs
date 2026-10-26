using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleWorldDamageAssetTools
    {
        private const string Root = "Assets/Official/Combat/Numbers/Resources/Combat/Numbers/";
        [Serializable] private sealed class Payload { public string manifest; public BitmapFont[] fonts; }
        [Serializable] private sealed class BitmapFont
        {
            public string key, name, shader;
            public float size, lineSpacing, ascent, descent;
            public Glyph[] glyphs;
            public Scalar[] floats;
            public Tint[] colors;
            public float[] textureScale, textureOffset;
        }
        [Serializable] private sealed class Glyph
        {
            public int index;
            public Rectangle uv, vert;
            public float advance;
            public bool flipped;
        }
        [Serializable] private sealed class Rectangle { public float x,y,width,height; public Rect Value => new Rect(x,y,width,height); }
        [Serializable] private sealed class Scalar { public string name; public float value; }
        [Serializable] private sealed class Tint { public string name; public float[] rgba; }

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var data = JsonUtility.FromJson<Payload>(File.ReadAllText(Root + "world-damage.json"));
            if (data.manifest != "854_156383") throw new InvalidDataException("Unpinned original world damage payload.");
            foreach (var entry in data.fonts)
            {
                if (entry.shader != "UI/GameDamage") throw new InvalidDataException("Unexpected original bitmap font shader.");
                string texturePath = Root + entry.key + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
                string materialPath = Root + entry.key + ".mat";
                var shader = Shader.Find("Zjcs/Opening/DamageBitmap");
                if (shader == null) throw new InvalidDataException("The Windows UI/GameDamage shader translation is missing.");
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                material.shader = shader;
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                material.mainTextureScale = new Vector2(entry.textureScale[0],entry.textureScale[1]);
                material.mainTextureOffset = new Vector2(entry.textureOffset[0],entry.textureOffset[1]);
                foreach (var value in entry.floats) material.SetFloat(value.name,value.value);
                foreach (var value in entry.colors) material.SetColor(value.name,new Color(value.rgba[0],value.rgba[1],value.rgba[2],value.rgba[3]));
                EditorUtility.SetDirty(material);
                string fontPath = Root + entry.key + ".fontsettings";
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null) { font = new Font(entry.name); AssetDatabase.CreateAsset(font,fontPath); }
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
                    var glyph=entry.glyphs[i];
#pragma warning disable 0618
                    glyphs[i]=new CharacterInfo { index=glyph.index,uv=glyph.uv.Value,vert=glyph.vert.Value,
                        advance=Mathf.RoundToInt(glyph.advance),flipped=glyph.flipped,size=Mathf.RoundToInt(entry.size) };
#pragma warning restore 0618
                }
                font.characterInfo=glyphs;
                EditorUtility.SetDirty(font);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Original configured world damage fonts imported: " + data.fonts.Length + ".");
        }
    }
}
