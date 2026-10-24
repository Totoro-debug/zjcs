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
        public float[] border;
        public int mesh_type;
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
    public sealed class HomeSkinRuntimeData
    {
        public string version;
        public HomeSkinEntity[] entities;
    }

    [Serializable]
    public sealed class HomeSkinEntity
    {
        public int skin_item_id;
        public int environment_skin_id;
        public string name;
        public int type;
        public float[] slot;
        public HomeSceneLayer[] layers;
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
        public HomeFruitTreeScene fruit_tree;
        public HomeTextPair[] strings;
    }

    [Serializable]
    public sealed class HomeFruitTreeScene
    {
        public float[] tree_position;
        public float tree_scale;
        public HomeTreeAnimation[] animations;
        public HomeSceneLayer fruit_effect;
        public HomeFruitTreeSlot[] fruit_slots;
        public HomeSceneLayer[] layers;
        public HomeFruitTreeState[] states;
        public HomeFruitTreeStateAnimation[] state_animations;
    }

    [Serializable]
    public sealed class HomeFruitTreeSlot
    {
        public string path;
        public float[] position;
        public float[] scale;
    }

    [Serializable]
    public sealed class HomeFruitTreeState
    {
        public string key;
        public string[] paths;
    }

    [Serializable]
    public sealed class HomeFruitTreeStateAnimation
    {
        public int state;
        public string name;
    }

    [Serializable]
    public sealed class HomeTreeAnimation
    {
        public string name;
        public float duration;
        public float fps;
        public float scale;
        public HomeTreeFrame[] frames;
    }

    [Serializable]
    public sealed class HomeTreeFrame
    {
        public HomeTreeMesh[] meshes;
    }

    [Serializable]
    public sealed class HomeTreeMesh
    {
        public string page;
        public int order;
        public float[] vertices;
        public float[] uv;
        public int[] triangles;
        public float[] color;
        public int blend;
    }

    [Serializable]
    public sealed class HomeUiImage
    {
        public HomeSpriteRef sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
        public bool component_flags;
        public bool enabled = true;
        public bool raycast_target;
        public bool maskable = true;
        public bool fill_center = true;
        public int fill_method;
        public float fill_amount = 1f;
        public bool fill_clockwise = true;
        public int fill_origin;
        public bool use_sprite_mesh;
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
        public bool mask_component_flags, mask_enabled, mask_show_graphic;
        public bool has_tab_group, tab_group_allow_switch_off;
        public bool no_drawing_raycast, raycast_target;
        public float[] raycast_padding;
        public bool rectmask2d;
        public HomeUiLayout layout;
        public HomeUiGrid grid;
        public int[] size_fitter;
        public HomeUiLayoutElement layout_element;
        public HomeUiAspect aspect_fitter;
        public float alpha;
        public bool button;
        public HomeUiScroll scroll;
        public HomeUiAnimation[] animations;
        public HomeUiNode[] children;
    }

    [Serializable]
    public sealed class HomeUiAnimation
    {
        public string name;
        public float duration;
        public bool loop;
        public HomeUiAnimationTrack[] tracks;
    }

    [Serializable]
    public sealed class HomeUiAnimationTrack
    {
        public string path;
        public string property;
        public HomeUiAnimationKey[] keys;
    }

    [Serializable]
    public sealed class HomeUiAnimationKey
    {
        public float time;
        public float value;
        public float in_slope;
        public float out_slope;
        public bool in_step;
        public bool out_step;
    }

    [Serializable]
    public sealed class HomeUiPanel
    {
        public string key;
        public HomeUiNode root;
    }

    [Serializable]
    public sealed class HomeUiPanelExtensionData
    {
        public string version;
        public HomeUiFlatPanel[] panels;
        public HomeDynamicSprite[] dynamic_sprites;
        public HomeTextPair[] texts;
    }

    [Serializable]
    public sealed class HomeUiFlatPanel
    {
        public string key;
        public HomeUiFlatNode root;
        public HomeUiFlatNode[] nodes;
    }

    [Serializable]
    public sealed class HomeUiFlatNode
    {
        public string parent_path;
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
        public bool mask_component_flags, mask_enabled, mask_show_graphic;
        public bool has_tab_group, tab_group_allow_switch_off;
        public bool no_drawing_raycast, raycast_target;
        public float[] raycast_padding;
        public bool rectmask2d;
        public HomeUiLayout layout;
        public HomeUiGrid grid;
        public int[] size_fitter;
        public HomeUiLayoutElement layout_element;
        public HomeUiAspect aspect_fitter;
        public float alpha;
        public bool button;
        public HomeUiScroll scroll;
        public HomeUiAnimation[] animations;

        public HomeUiNode ToNode() => new HomeUiNode
        {
            name = name,
            path = path,
            active = active,
            anchor_min = anchor_min,
            anchor_max = anchor_max,
            anchored_position = anchored_position,
            size_delta = size_delta,
            pivot = pivot,
            scale = scale,
            rotation = rotation,
            scripts = scripts,
            image = image,
            text = text,
            localization_key = localization_key,
            mask = mask,
            mask_component_flags = mask_component_flags,
            has_tab_group = has_tab_group,
            no_drawing_raycast = no_drawing_raycast,
            raycast_target = raycast_target,
            raycast_padding = raycast_padding,
            tab_group_allow_switch_off = tab_group_allow_switch_off,
            mask_enabled = mask_enabled,
            mask_show_graphic = mask_show_graphic,
            rectmask2d = rectmask2d,
            layout = layout,
            grid = grid,
            size_fitter = size_fitter,
            layout_element = layout_element,
            aspect_fitter = aspect_fitter,
            alpha = alpha,
            button = button,
            scroll = scroll,
            animations = animations,
            children = Array.Empty<HomeUiNode>(),
        };
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
        public HomeUiFlatPanel[] panels;
        public HomeTextPair[] texts;
        public HomeDynamicSprite[] dynamic_sprites;
        public HomeTextPair[] furniture_names;
        public HomeTextPair[] furniture_type_names;
        public HomeTextPair[] strings;
        public HomeAstrologicalTagArt[] astrological_tag_art;
    }

    [Serializable]
    public sealed class HomeAstrologicalTagArt
    {
        public int property_type;
        public HomeSpriteRef icon;
        public float[] color;
    }

    [Serializable]
    public sealed class HomeStateGameObjectConfig
    {
        public int state;
        public string[] game_objects;
    }

    [Serializable]
    public sealed class HomeStateSpriteConfig
    {
        public int state;
        public string sprite_file;
    }

    [Serializable]
    public sealed class HomeStateImageConfig
    {
        public string path;
        public HomeStateSpriteConfig[] states;
    }

    [Serializable]
    public sealed class HomeTreasureGachaMachineConfig
    {
        public string manifest;
        public HomeStateGameObjectConfig[] map_game_object_states;
        public HomeStateImageConfig[] map_sprite_states;
        public HomeStateImageConfig[] element_sprite_states;
        public HomeStateGameObjectConfig[] machine_show_game_object_states;
        public HomeGachaScaleFitterConfig aspect_ratio_scale_fitter;
    }

    [Serializable]
    public sealed class HomeGachaScaleFitterConfig
    {
        public string owner_path;
        public string target_path;
        public HomeScaleCurveConfig first_curve;
        public HomeScaleCurveConfig second_curve;
    }

    [Serializable]
    public sealed class HomeScaleCurveConfig
    {
        public int pre_wrap_mode;
        public int post_wrap_mode;
        public HomeScaleCurveKey[] keys;
    }

    [Serializable]
    public sealed class HomeScaleCurveKey
    {
        public float time;
        public float value;
        public float in_slope;
        public float out_slope;
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
        private readonly Dictionary<int, HomeAstrologicalTagArt> astrologicalTagArt =
            new Dictionary<int, HomeAstrologicalTagArt>();
        private readonly List<string> additionalSpriteFolders = new List<string>();
        private TMP_FontAsset font;
        private HomeTreasureGachaMachineConfig treasureGachaMachine;

        public string Folder { get; }
        public HomeSceneData Scene { get; }
        public HomeUiData Ui { get; }
        public HomeSkinRuntimeData HomeSkins { get; }
        public HomeTreasureGachaMachineConfig TreasureGachaMachine => treasureGachaMachine;

        private HomeAssetCatalog(string folder, HomeSceneData scene, HomeUiData ui,
            HomeSkinRuntimeData homeSkins)
        {
            Folder = folder;
            Scene = scene;
            Ui = ui;
            HomeSkins = homeSkins;
            foreach (HomeUiFlatPanel panel in ui.panels ?? Array.Empty<HomeUiFlatPanel>())
                if (panel != null && !string.IsNullOrEmpty(panel.key))
                    panels[panel.key] = RestoreFlatPanelNodes(panel);
            foreach (HomeTextPair text in ui.texts ?? Array.Empty<HomeTextPair>())
                if (text != null && !string.IsNullOrEmpty(text.key)) texts[text.key] = text.value ?? string.Empty;
            foreach (HomeDynamicSprite sprite in ui.dynamic_sprites ?? Array.Empty<HomeDynamicSprite>())
                if (sprite != null && !string.IsNullOrEmpty(sprite.key)) dynamicSprites[sprite.key] = sprite.file;
            foreach (HomeTextPair name in ui.furniture_names ?? Array.Empty<HomeTextPair>())
                if (name != null) furnitureNames[name.key] = name.value ?? string.Empty;
            foreach (HomeTextPair name in ui.furniture_type_names ?? Array.Empty<HomeTextPair>())
                if (name != null) furnitureTypeNames[name.key] = name.value ?? string.Empty;
            foreach (HomeAstrologicalTagArt setting in ui.astrological_tag_art ?? Array.Empty<HomeAstrologicalTagArt>())
                if (setting != null) astrologicalTagArt[setting.property_type] = setting;
        }

        public static HomeAssetCatalog LoadDefault()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            HomeSceneData scene = Read<HomeSceneData>(Path.Combine(folder, "scene.json"));
            HomeUiData ui = Read<HomeUiData>(Path.Combine(folder, "ui.json"));
            HomeSkinRuntimeData homeSkins = Read<HomeSkinRuntimeData>(Path.Combine(folder, "home-skins.json"));
            if (scene == null || scene.camera == null || scene.background == null || scene.background.Length == 0)
                throw new InvalidDataException("The official home scene payload is missing.");
            if (ui == null || ui.panels == null || ui.panels.Length == 0)
                throw new InvalidDataException("The official home UI payload is missing.");
            if (homeSkins == null || homeSkins.version != "854_156383"
                || homeSkins.entities == null || homeSkins.entities.Length == 0)
                throw new InvalidDataException("The official home skin payload is missing or invalid.");
            return new HomeAssetCatalog(folder, scene, ui, homeSkins);
        }

        private static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Home payload is missing.", path);
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }

        public bool HasPanel(string key) => key != null && panels.ContainsKey(key);

        public HomeUiPanel Panel(string key) => panels.TryGetValue(key, out HomeUiPanel panel) ? panel : null;

        public HomeAstrologicalTagArt AstrologicalTagArt(int propertyType) =>
            astrologicalTagArt.TryGetValue(propertyType, out HomeAstrologicalTagArt setting) ? setting : null;

        public void LoadPanelExtension(string path)
        {
            HomeUiPanelExtensionData extension = Read<HomeUiPanelExtensionData>(path);
            if (extension == null || extension.panels == null || extension.panels.Length == 0)
                throw new InvalidDataException("The official home panel extension is missing or empty.");
            foreach (HomeUiFlatPanel flatPanel in extension.panels)
            {
                if (flatPanel == null || string.IsNullOrEmpty(flatPanel.key) || flatPanel.root == null)
                    throw new InvalidDataException("The official home panel extension contains an invalid panel.");
                HomeUiPanel panel = RestoreFlatPanelNodes(flatPanel);
                panels[panel.key] = panel;
            }
            string moduleFolder = Path.GetDirectoryName(path);
            if (string.Equals(Path.GetFileName(moduleFolder), "TreasureGacha", StringComparison.Ordinal))
            {
                LoadTreasureGachaMachine(Path.Combine(moduleFolder, "machine-states.json"));
            }
            if (!string.IsNullOrEmpty(moduleFolder)) additionalSpriteFolders.Add(Path.Combine(moduleFolder, "sprites"));
            foreach (HomeDynamicSprite sprite in extension.dynamic_sprites ?? Array.Empty<HomeDynamicSprite>())
                if (sprite != null && !string.IsNullOrEmpty(sprite.key)) dynamicSprites[sprite.key] = sprite.file;
            foreach (HomeTextPair text in extension.texts ?? Array.Empty<HomeTextPair>())
                if (text != null && !string.IsNullOrEmpty(text.key)) texts[text.key] = text.value ?? string.Empty;
        }

        public void LoadTreasureGachaMachine(string path)
        {
            HomeTreasureGachaMachineConfig machine = Read<HomeTreasureGachaMachineConfig>(path);
            if (machine == null || machine.manifest != "854_156383"
                || machine.map_game_object_states == null || machine.map_game_object_states.Length == 0
                || machine.map_sprite_states == null || machine.map_sprite_states.Length == 0
                || machine.element_sprite_states == null || machine.element_sprite_states.Length == 0
                || machine.machine_show_game_object_states == null
                || machine.machine_show_game_object_states.Length == 0
                || machine.aspect_ratio_scale_fitter == null
                || string.IsNullOrEmpty(machine.aspect_ratio_scale_fitter.owner_path)
                || string.IsNullOrEmpty(machine.aspect_ratio_scale_fitter.target_path)
                || !ValidScaleCurve(machine.aspect_ratio_scale_fitter.first_curve)
                || !ValidScaleCurve(machine.aspect_ratio_scale_fitter.second_curve))
                throw new InvalidDataException("The official treasure gacha machine state payload is missing or invalid.");
            treasureGachaMachine = machine;
        }

        private static HomeUiPanel RestoreFlatPanelNodes(HomeUiFlatPanel flatPanel)
        {
            if (flatPanel == null || flatPanel.root == null)
                throw new InvalidDataException("The official home panel has no flattened root.");
            HomeUiNode root = flatPanel.root.ToNode();
            if (string.IsNullOrEmpty(root.path))
                throw new InvalidDataException("The official home panel root path is missing: " + flatPanel.key);
            var nodes = new Dictionary<string, HomeUiNode>(StringComparer.Ordinal)
            {
                [root.path] = root,
            };
            var children = new Dictionary<HomeUiNode, List<HomeUiNode>>();
            children.Add(root, new List<HomeUiNode>());
            foreach (HomeUiFlatNode record in flatPanel.nodes ?? Array.Empty<HomeUiFlatNode>())
            {
                if (record == null || string.IsNullOrEmpty(record.parent_path) || string.IsNullOrEmpty(record.path))
                    throw new InvalidDataException("The official home panel contains an invalid flattened node: " + flatPanel.key);
                if (nodes.ContainsKey(record.path))
                    throw new InvalidDataException("The official home panel contains a duplicate node path: " + record.path);
                HomeUiNode node = record.ToNode();
                nodes.Add(node.path, node);
                children.Add(node, new List<HomeUiNode>());
            }

            foreach (HomeUiFlatNode record in flatPanel.nodes ?? Array.Empty<HomeUiFlatNode>())
            {
                if (!nodes.TryGetValue(record.parent_path, out HomeUiNode parent))
                    throw new InvalidDataException("The official home panel node has a missing parent: " + record.parent_path);
                children[parent].Add(nodes[record.path]);
            }
            foreach (KeyValuePair<HomeUiNode, List<HomeUiNode>> entry in children)
            {
                entry.Key.children = entry.Value.ToArray();
            }
            return new HomeUiPanel { key = flatPanel.key, root = root };
        }

        private static bool ValidScaleCurve(HomeScaleCurveConfig curve) =>
            curve != null && curve.keys != null && curve.keys.Length >= 2;

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

        public Sprite FindSprite(HomeSpriteRef reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.file)) return null;
            string key = "ref:" + JsonUtility.ToJson(reference);
            if (sprites.TryGetValue(key, out Sprite sprite)) return sprite;
            Texture2D texture = FindTexture(reference.file);
            if (texture == null) return null;
            Vector2 pivot = reference.pivot != null && reference.pivot.Length >= 2
                ? new Vector2(reference.pivot[0], reference.pivot[1])
                : new Vector2(0.5f, 0.5f);
            if (pivot.x > 1f || pivot.y > 1f)
                pivot = new Vector2(pivot.x / texture.width, pivot.y / texture.height);
            Vector4 border = reference.border != null && reference.border.Length >= 4
                ? new Vector4(reference.border[0], reference.border[1], reference.border[2], reference.border[3])
                : Vector4.zero;
            float pixelsPerUnit = reference.ppu > 0f ? reference.ppu : 100f;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                pixelsPerUnit, 0, (SpriteMeshType)Mathf.Clamp(reference.mesh_type, 0, 1), border);
            sprite.name = reference.file;
            sprites[key] = sprite;
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
            foreach (Sprite sprite in sprites.Values) DestroyLoaded(sprite);
            foreach (Texture2D texture in textures.Values) DestroyLoaded(texture);
            sprites.Clear();
            textures.Clear();
        }

        private static void DestroyLoaded(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
