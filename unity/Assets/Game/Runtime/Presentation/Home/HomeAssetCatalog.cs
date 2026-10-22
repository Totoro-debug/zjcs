using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    [Serializable]
    public sealed class HomeSpriteRef
    {
        public string file;
        public string name;
        public int width;
        public int height;
        public float[] pivot;
        public float ppu;
        public float[] rect_size;
        public float[] trim_offset;
        public float[] trim_size;

    }

    [Serializable]
    public sealed class HomeMaterialRef
    {
        public string name;
        public string shader;
        public HomeColorMap colors;
    }

    [Serializable]
    public sealed class HomeColorMap
    {
        public float[] _Color;
        public float[] _Color1;
        public float[] _Color2;
        public float[] _Flip;
        public float[] _RendererColor;
    }

    [Serializable]
    public sealed class HomeSceneLayer
    {
        public string path;
        public HomeSpriteRef sprite;
        public float[] vertices;
        public float[] color;
        public int sorting_layer;
        public int sorting_order;
        public float depth;
        public HomeMaterialRef[] materials;
    }

    [Serializable]
    public sealed class HomeFurnitureScene
    {
        public int type;
        public string name;
        public float[] slot;
        public HomeSceneLayer[] layers;
    }

    [Serializable]
    public sealed class HomeQuad
    {
        public string path;
        public float[] vertices;
    }

    [Serializable]
    public sealed class HomeOutdoorSlot
    {
        public int id;
        public string name;
        public float[] position;
    }

    [Serializable]
    public sealed class HomeCameraData
    {
        public float orthographic_size;
        public float[] position;
        public float[] border_center;
        public float[] border_extents;
        public float drag_sensitivity;
    }

    [Serializable]
    public sealed class HomeTextPair
    {
        public string key;
        public string value;
        public float[] color;
    }

    [Serializable]
    public sealed class HomeSceneData
    {
        public string version;
        public HomeCameraData camera;
        public HomeSceneLayer[] background;
        public HomeFurnitureScene[] furniture;
        public HomeQuad[] click_regions;
        public HomeOutdoorSlot[] outdoor_slots;
        public HomeTextPair[] strings;
    }

    [Serializable]
    public sealed class HomeUiImage
    {
        public HomeSpriteRef sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
    }

    [Serializable]
    public sealed class HomeUiTextStyle
    {
        public string value;
        public float[] color;
        public float font_size;
        public int horizontal;
        public int vertical;
    }

    [Serializable]
    public sealed class HomeUiLayout
    {
        public int axis;
        public int[] padding;
        public int alignment;
        public float spacing;
        public bool[] expand;
        public bool[] control;
        public bool[] child_scale;
        public bool reverse;
    }

    [Serializable]
    public sealed class HomeUiGrid
    {
        public int[] padding;
        public int alignment;
        public int corner;
        public int axis;
        public float[] cell;
        public float[] spacing;
        public int constraint;
        public int count;
    }

    [Serializable]
    public sealed class HomeUiLayoutElement
    {
        public bool ignore;
        public float[] min;
        public float[] preferred;
        public float[] flexible;
    }

    [Serializable]
    public sealed class HomeUiAspect
    {
        public int mode;
        public float ratio;
    }

    [Serializable]
    public sealed class HomeUiScroll
    {
        public bool horizontal;
        public bool vertical;
        public bool inertia;
        public int movement;
        public float sensitivity;
        public float elasticity;
        public float deceleration;
    }

    [Serializable]
    public sealed class HomeUiNode
    {
        public string name;
        public string path;
        public bool active;
        public float[] anchor_min;
        public float[] anchor_max;
        public float[] anchored_position;
        public float[] size_delta;
        public float[] pivot;
        public float[] scale;
        public float rotation;
        public string[] scripts;
        public HomeUiImage image;
        public HomeUiTextStyle text;
        public string localization_key;
        public bool mask;
        public bool rectmask2d;
        public HomeUiLayout layout;
        public HomeUiGrid grid;
        public int[] size_fitter;
        public HomeUiLayoutElement layout_element;
        public HomeUiAspect aspect_fitter;
        public float alpha;
        public bool button;
        public HomeUiScroll scroll;
        public HomeUiNode[] children;
    }

    [Serializable]
    public sealed class HomeUiPanel
    {
        public string key;
        public HomeUiNode root;
    }

    [Serializable]
    public sealed class HomeDynamicSprite
    {
        public string key;
        public string file;
    }

    [Serializable]
    public sealed class HomeUiData
    {
        public string version;
        public HomeUiPanel[] panels;
        public HomeTextPair[] texts;
        public HomeDynamicSprite[] dynamic_sprites;
        public HomeTextPair[] furniture_names;
        public HomeTextPair[] furniture_type_names;
        public HomeTextPair[] strings;
    }

    /// <summary>
    /// Loads the staged official home payloads and turns PNG files under
    /// ``StreamingAssets/Home/sprites`` into sprites on demand.
    /// </summary>
    public sealed class HomeAssetCatalog
    {
        public const string FolderName = "Home";

        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> dynamicSprites = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HomeUiPanel> panels = new Dictionary<string, HomeUiPanel>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> furnitureNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> furnitureTypeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> additionalSpriteFolders = new List<string>();
        private TMP_FontAsset font;

        public string Folder { get; }
        public HomeSceneData Scene { get; }
        public HomeUiData Ui { get; }

        private HomeAssetCatalog(string folder, HomeSceneData scene, HomeUiData ui)
        {
            Folder = folder;
            Scene = scene;
            Ui = ui;
            foreach (HomeUiPanel panel in ui.panels ?? Array.Empty<HomeUiPanel>())
                if (panel != null && !string.IsNullOrEmpty(panel.key)) panels[panel.key] = panel;
            foreach (HomeTextPair text in ui.texts ?? Array.Empty<HomeTextPair>())
                if (text != null && !string.IsNullOrEmpty(text.key)) texts[text.key] = text.value ?? string.Empty;
            foreach (HomeDynamicSprite sprite in ui.dynamic_sprites ?? Array.Empty<HomeDynamicSprite>())
                if (sprite != null && !string.IsNullOrEmpty(sprite.key)) dynamicSprites[sprite.key] = sprite.file;
            foreach (HomeTextPair name in ui.furniture_names ?? Array.Empty<HomeTextPair>())
                if (name != null) furnitureNames[name.key] = name.value ?? string.Empty;
            foreach (HomeTextPair name in ui.furniture_type_names ?? Array.Empty<HomeTextPair>())
                if (name != null) furnitureTypeNames[name.key] = name.value ?? string.Empty;
        }

        public static HomeAssetCatalog LoadDefault()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            HomeSceneData scene = Read<HomeSceneData>(Path.Combine(folder, "scene.json"));
            HomeUiData ui = Read<HomeUiData>(Path.Combine(folder, "ui.json"));
            if (scene == null || scene.camera == null || scene.background == null || scene.background.Length == 0)
                throw new InvalidDataException("The official home scene payload is missing.");
            if (ui == null || ui.panels == null || ui.panels.Length == 0)
                throw new InvalidDataException("The official home UI payload is missing.");
            return new HomeAssetCatalog(folder, scene, ui);
        }

        private static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Home payload is missing.", path);
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }

        public bool HasPanel(string key) => key != null && panels.ContainsKey(key);

        public HomeUiPanel Panel(string key) => panels.TryGetValue(key, out HomeUiPanel panel) ? panel : null;

        public void LoadPanelExtension(string path)
        {
            HomeUiData extension = Read<HomeUiData>(path);
            if (extension == null || extension.panels == null || extension.panels.Length == 0)
                throw new InvalidDataException("The official home panel extension is missing or empty.");
            foreach (HomeUiPanel panel in extension.panels)
            {
                if (panel == null || string.IsNullOrEmpty(panel.key) || panel.root == null)
                    throw new InvalidDataException("The official home panel extension contains an invalid panel.");
                panels[panel.key] = panel;
            }
            string moduleFolder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(moduleFolder)) additionalSpriteFolders.Add(Path.Combine(moduleFolder, "sprites"));
            foreach (HomeDynamicSprite sprite in extension.dynamic_sprites ?? Array.Empty<HomeDynamicSprite>())
                if (sprite != null && !string.IsNullOrEmpty(sprite.key)) dynamicSprites[sprite.key] = sprite.file;
            foreach (HomeTextPair text in extension.texts ?? Array.Empty<HomeTextPair>())
                if (text != null && !string.IsNullOrEmpty(text.key)) texts[text.key] = text.value ?? string.Empty;
        }

        public string LocalizedText(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
            return texts.TryGetValue(key, out string value) ? value : fallback ?? key;
        }

        public string FurnitureName(int type) =>
            furnitureNames.TryGetValue(type.ToString(), out string value) ? value : "Furniture" + type;

        public string FurnitureTypeName(int type) =>
            furnitureTypeNames.TryGetValue(type.ToString(), out string value) ? value : string.Empty;

        public Sprite FindSprite(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (sprites.TryGetValue(file, out Sprite sprite)) return sprite;
            Texture2D texture = FindTexture(file);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = file;
            sprites[file] = sprite;
            return sprite;
        }

        public Sprite FindDynamicSprite(string key)
        {
            return key != null && dynamicSprites.TryGetValue(key, out string file) ? FindSprite(file) : null;
        }

        public Texture2D FindTexture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (textures.TryGetValue(file, out Texture2D cached)) return cached;
            string path = Path.Combine(Folder, "sprites", file);
            if (!File.Exists(path))
                path = additionalSpriteFolders.Select(folder => Path.Combine(folder, file)).FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.name = file;
            texture.wrapMode = TextureWrapMode.Clamp;
            textures[file] = texture;
            return texture;
        }

        public TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic");
                if (font == null) font = TMP_Settings.defaultFontAsset;
                if (font == null) throw new InvalidOperationException("The official Chinese TMP font asset is missing.");
                Debug.Log("[Home] TMP font=" + font.name + " material=" + (font.material != null)
                          + " atlas=" + (font.atlasTexture != null));
                return font;
            }
        }

        public void Release()
        {
            foreach (Sprite sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (Texture2D texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            sprites.Clear();
            textures.Clear();
        }
    }
}
