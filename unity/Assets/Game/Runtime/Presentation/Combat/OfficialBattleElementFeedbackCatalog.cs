using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleElementFeedbackData
    {
        public string manifest;
        public BattleElementFeedbackBinding[] bindings;
        public BattleElementFont[] fonts;
        public BattleElementMaterial[] materials;
        public BattleElementTexture[] textures;
        public BattleElementSprite[] sprites;
    }
    [Serializable] public sealed class BattleElementFeedbackBinding
    {
        public string rootName, elementPath, describePath, bgPath;
        public int effectType;
        public string[] effectPaths, hideObjects, hideRenderers;
        public BattleElementTextBinding[] texts;
        public BattleElementMaterialBinding[] materials;
        public BattleHudCustomActionText[] keys;
        public BattleElementTrigger[] triggers;
        public BattleElementBackground[] backgrounds;
    }
    [Serializable] public sealed class BattleElementTrigger { public string trigger, clip; }
    [Serializable] public sealed class BattleElementTextBinding
    {
        public string path, font, material;
        public int style, weight, order;
        public float size, characterSpacing, wordSpacing, lineSpacing;
        public float[] margin;
        public BattleElementTextEffect effect;
    }
    [Serializable] public sealed class BattleElementTextEffect
    {
        public float dilate, width;
        public bool outline;
        public float[] color;
    }
    [Serializable] public sealed class BattleElementBackground
    {
        public string backgroundPath, textPath;
        public int align;
        public float paddingX, paddingY, minWidth, maxWidth, minHeight, maxHeight;
    }
    [Serializable] public sealed class BattleElementMaterialBinding
    {
        public string path, material, sprite;
        public int order, drawMode, tileMode;
        public float[] size;
    }
    [Serializable] public sealed class BattleElementFont
    {
        public string key, name, sourcePath, sourceSha256, material, fontJson, warmCharacters;
        public int width, height, padding, renderMode, population, pointSize;
        public bool multiAtlas, clearOnBuild;
        public string[] atlasTextures;
    }
    [Serializable] public sealed class BattleElementMaterial
    {
        public string key, name, shader;
        public int queue;
        public string[] keywords;
        public BattleElementScalar[] floats;
        public BattleElementColor[] colors;
        public BattleElementMaterialTexture[] textures;
    }
    [Serializable] public sealed class BattleElementScalar { public string name; public float value; }
    [Serializable] public sealed class BattleElementColor { public string name; public float[] rgba; }
    [Serializable] public sealed class BattleElementMaterialTexture { public string name, texture; public float[] scale, offset; }
    [Serializable] public sealed class BattleElementTexture
    {
        public string key;
        public int width, height, filter, wrapU, wrapV, aniso;
        public float bias;
        public bool sRGB;
    }
    [Serializable] public sealed class BattleElementSprite
    {
        public string key, texture;
        public BattleElementRectangle rect;
        public BattleElementPoint pivot;
        public float pixelsPerUnit;
        public float[] border;
        public BattleElementPoint[] vertices;
        public ushort[] triangles;
    }
    [Serializable] public sealed class BattleElementPoint { public float x,y; public Vector2 Value => new Vector2(x,y); }
    [Serializable] public sealed class BattleElementRectangle { public float x,y,width,height; public Rect Value => new Rect(x,y,width,height); }

    public static class OfficialBattleElementFeedbackCatalog
    {
        public const string ResourceRoot = "Combat/ElementFeedback/";
        private static BattleElementFeedbackData current;
        public static BattleElementFeedbackData Load()
        {
            if (current != null) return current;
            var asset = Resources.Load<TextAsset>(ResourceRoot+"element-feedback");
            if (asset == null) throw new InvalidOperationException("Original element feedback bindings are missing.");
            var data = JsonUtility.FromJson<BattleElementFeedbackData>(asset.text);
            if (data.manifest != "854_156383" || data.bindings == null || data.bindings.Length != 6)
                throw new FormatException("Unpinned original element feedback bindings.");
            return current = data;
        }
        public static BattleElementFeedbackBinding Find(int effectType)
        {
            foreach (var binding in Load().bindings)
                if (binding.effectType == effectType) return binding;
            return null;
        }
    }
}
