using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    [Serializable]
    public sealed class OfficialMap10UiPayload
    {
        public string manifest;
        public int mapClassId;
        public string sourceUiSha256;
        public string sourceManifestSha256;
        public OfficialMap10UiPanel[] panels = Array.Empty<OfficialMap10UiPanel>();
        public OfficialMap10UiSpriteRecord[] sprites = Array.Empty<OfficialMap10UiSpriteRecord>();
    }

    [Serializable]
    public sealed class OfficialMap10UiPanel
    {
        public string key;
        public OfficialMap10UiNodeData[] nodes = Array.Empty<OfficialMap10UiNodeData>();
        public OfficialMap10StoryAnimation[] storyAnimations = Array.Empty<OfficialMap10StoryAnimation>();

        public BattleHudNode BuildRoot()
        {
            if (nodes == null || nodes.Length == 0)
                throw new InvalidDataException("Official Map10 UI panel has no nodes: " + key);
            var mapped = new BattleHudNode[nodes.Length];
            var children = new List<int>[nodes.Length];
            int rootIndex = -1;
            for (int i = 0; i < nodes.Length; i++)
            {
                OfficialMap10UiNodeData source = nodes[i];
                if (source == null) throw new InvalidDataException("Official Map10 UI node is null: " + key);
                mapped[i] = new BattleHudNode();
                foreach (System.Reflection.FieldInfo target in BattleNodeFields)
                {
                    if (target.Name == "children") continue;
                    System.Reflection.FieldInfo value = typeof(OfficialMap10UiNodeData).GetField(target.Name);
                    if (value != null) target.SetValue(mapped[i], value.GetValue(source));
                }

                if (source.parentIndex == -1)
                {
                    if (rootIndex >= 0) throw new InvalidDataException("Official Map10 UI panel has multiple roots: " + key);
                    rootIndex = i;
                }
                else
                {
                    if (source.parentIndex < 0 || source.parentIndex >= i)
                        throw new InvalidDataException("Official Map10 UI parent index is invalid: " + key);
                    if (children[source.parentIndex] == null) children[source.parentIndex] = new List<int>();
                    children[source.parentIndex].Add(i);
                }
            }

            if (rootIndex != 0) throw new InvalidDataException("Official Map10 UI root node is invalid: " + key);
            for (int i = 0; i < mapped.Length; i++)
            {
                List<int> childIndices = children[i];
                if (childIndices == null) continue;
                mapped[i].children = new BattleHudNode[childIndices.Count];
                for (int j = 0; j < childIndices.Count; j++) mapped[i].children[j] = mapped[childIndices[j]];
            }
            return mapped[rootIndex];
        }

        private static readonly System.Reflection.FieldInfo[] BattleNodeFields =
            typeof(BattleHudNode).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
    }

    [Serializable]
    public sealed class OfficialMap10UiNodeData
    {
        public int parentIndex;
        public string name, path, localization_key;
        public string[] scripts;
        public bool active, button, mask, rectmask2d;
        public bool has_image, has_text, has_scroll;
        public string text_value;
        public float[] text_color, text_margin, text_auto_size;
        public float text_font_size;
        public int text_horizontal, text_vertical, text_overflow;
        public bool text_word_wrap;
        public string image_sprite_file;
        public int image_sprite_width, image_sprite_height, image_type;
        public float[] image_sprite_border, image_sprite_pivot, image_color;
        public float image_sprite_ppu;
        public bool has_image_sprite, image_preserve_aspect;
        public bool image_graphic_flags, image_raycast_target, image_maskable;
        public bool mask_graphic_flags, mask_show_graphic;
        public bool scroll_horizontal, scroll_vertical, scroll_inertia;
        public int scroll_movement;
        public float scroll_sensitivity, scroll_elasticity, scroll_deceleration;
        public bool has_layout, has_size_fitter, has_layout_element, layout_reverse, layout_element_ignore;
        public float[] layout_padding, layout_spacing_list, layout_element_min, layout_element_preferred, layout_element_flexible;
        public float layout_spacing;
        public int layout_axis, layout_alignment, fitter_horizontal, fitter_vertical;
        public bool[] layout_expand, layout_control, layout_child_scale;
        public float[] anchor_min, anchor_max, anchored_position, size_delta, pivot, scale;
        public float rotation, alpha = 1f;
    }

    [Serializable]
    public sealed class OfficialMap10StoryAnimation
    {
        public string name;
        public OfficialMap10StoryAnimationStep[] steps = Array.Empty<OfficialMap10StoryAnimationStep>();
    }

    [Serializable]
    public sealed class OfficialMap10StoryAnimationStep
    {
        public string path;
        public string clip;
        public float delay;
        public float duration;
        public bool close;
        public OfficialMap10StoryActiveNode[] active = Array.Empty<OfficialMap10StoryActiveNode>();
        public string spine_path;
        public OfficialMap10SpineSequenceEntry[] spine_sequence = Array.Empty<OfficialMap10SpineSequenceEntry>();
    }

    [Serializable]
    public sealed class OfficialMap10StoryActiveNode
    {
        public string path;
        public bool value;
    }

    [Serializable]
    public sealed class OfficialMap10SpineSequenceEntry
    {
        public string AnimName;
        public int LoopTimes;
        public float ExitTime;
        public float NextMixDuration;
    }

    [Serializable]
    public sealed class OfficialMap10UiSpriteRecord
    {
        public string uri;
        public string file;
        public string unityAssetPath;
        public string sha256;
        public int width;
        public int height;
        public float[] border;
        public float[] pivot;
        public float pixelsPerUnit;
    }

    [Serializable]
    public sealed class OfficialMap10UiSourceManifest
    {
        public string manifest;
        public int mapClassId;
        public OfficialMap10UiSource source;
        public OfficialMap10UiOutput output;
    }

    [Serializable]
    public sealed class OfficialMap10UiSource
    {
        public string uiDataSha256;
        public string uiSourceManifestSha256;
    }

    [Serializable]
    public sealed class OfficialMap10UiOutput
    {
        public string path;
        public string sha256;
        public int panelCount;
        public int spriteCount;
    }

    [Serializable]
    public sealed class OfficialMap10UiSpriteBinding
    {
        public string uri;
        public string file;
        public Sprite sprite;
    }

    [CreateAssetMenu(menuName = "Zjcs/World/Official Map10 UI Catalog")]
    public sealed class OfficialMap10UiCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "World/Map10UiCatalog";
        [SerializeField] private TextAsset panelData;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private OfficialMap10UiSpriteBinding[] sprites = Array.Empty<OfficialMap10UiSpriteBinding>();
        private Dictionary<string, OfficialMap10UiPanel> panelByKey;
        private Dictionary<string, Sprite> spriteByUri;
        private Dictionary<string, Sprite> spriteByFile;
        private OfficialMap10UiPayload payload;

        public static OfficialMap10UiCatalog LoadDefault()
        {
            OfficialMap10UiCatalog catalog = Resources.Load<OfficialMap10UiCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official Map10 UI catalog is not installed.");
            catalog.ValidateBindings();
            OfficialBattleHudCatalog.LoadDefault();
            return catalog;
        }

        public IReadOnlyList<OfficialMap10UiPanel> Panels => Load().panels;

        public OfficialMap10UiPanel FindPanel(string key)
        {
            Load();
            return panelByKey.TryGetValue(key, out OfficialMap10UiPanel panel) ? panel : null;
        }

        public Sprite FindSprite(string fileOrUri)
        {
            if (string.IsNullOrEmpty(fileOrUri)) return null;
            Load();
            if (spriteByUri.TryGetValue(fileOrUri, out Sprite byUri)) return byUri;
            string file = Path.GetFileName(fileOrUri);
            if (spriteByFile.TryGetValue(file, out Sprite byName)) return byName;
            if (fileOrUri.StartsWith("UI/Dialogue/", StringComparison.Ordinal))
                return Resources.Load<Sprite>("Map10/Dialogue/" + Path.GetFileNameWithoutExtension(file));
            return null;
        }

        private OfficialMap10UiPayload Load()
        {
            ValidateBindings();
            if (payload == null)
            {
                OfficialMap10UiPayload parsed = JsonUtility.FromJson<OfficialMap10UiPayload>(panelData.text);
                OfficialMap10UiSourceManifest manifest = JsonUtility.FromJson<OfficialMap10UiSourceManifest>(sourceManifest.text);
                if (parsed == null || parsed.manifest != "854_156383" || parsed.mapClassId != 10
                    || parsed.panels == null || parsed.panels.Length != 14 || parsed.sprites == null
                    || sprites.Length != parsed.sprites.Length
                    || manifest == null || manifest.manifest != parsed.manifest || manifest.mapClassId != 10
                    || manifest.output == null || manifest.output.path != "map10-ui.json"
                    || manifest.source == null
                    || !string.Equals(parsed.sourceUiSha256, manifest.source.uiDataSha256, StringComparison.OrdinalIgnoreCase)
                    || manifest.output.panelCount != parsed.panels.Length
                    || manifest.output.spriteCount != parsed.sprites.Length
                    || !string.Equals(manifest.output.sha256, Hash(Encoding.UTF8.GetBytes(panelData.text)), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(parsed.sourceManifestSha256, manifest.source.uiSourceManifestSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Official Map10 UI data or source hashes are invalid.");

                panelByKey = new Dictionary<string, OfficialMap10UiPanel>(StringComparer.Ordinal);
                foreach (OfficialMap10UiPanel panel in parsed.panels)
                    if (panel == null || string.IsNullOrEmpty(panel.key) || panel.nodes == null || panel.nodes.Length == 0
                        || !panelByKey.TryAdd(panel.key, panel))
                        throw new InvalidDataException("Official Map10 UI panel bindings are invalid.");

                spriteByUri = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                spriteByFile = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (OfficialMap10UiSpriteRecord record in parsed.sprites)
                {
                    OfficialMap10UiSpriteBinding binding = FindBinding(record);
                    if (record == null || string.IsNullOrEmpty(record.uri) || binding == null || binding.sprite == null
                        || !string.Equals(binding.file, record.file, StringComparison.Ordinal))
                        throw new InvalidDataException("Official Map10 UI sprite binding is missing: " + (record == null ? "<null>" : record.file));
                    spriteByUri.Add(record.uri, binding.sprite);
                    spriteByFile.Add(record.file, binding.sprite);
                }
                payload = parsed;
            }
            return payload;
        }

        private OfficialMap10UiSpriteBinding FindBinding(OfficialMap10UiSpriteRecord record)
        {
            foreach (OfficialMap10UiSpriteBinding binding in sprites)
                if (binding != null && record != null && string.Equals(binding.uri, record.uri, StringComparison.Ordinal))
                    return binding;
            return null;
        }

        private void ValidateBindings()
        {
            if (panelData == null || sourceManifest == null || sprites == null || sprites.Length == 0)
                throw new InvalidOperationException("Official Map10 UI data, source manifest, or sprite bindings are missing.");
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
