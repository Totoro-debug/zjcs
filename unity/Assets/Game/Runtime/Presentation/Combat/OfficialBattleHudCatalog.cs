using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleHudCatalogData
    {
        public string manifest;
        public BattleHudPrefab[] prefabs;
    }

    [Serializable] public sealed class BattleHudPrefab
    {
        public string key;
        public BattleHudNode root;
    }

    [Serializable] public sealed class BattleHudNode
    {
        public string name, path, localization_key;
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
        public BattleHudNode[] children;
        public BattleHudImage image;
        public BattleHudText text;
        public BattleHudScroll scroll;
    }

    [Serializable] public sealed class BattleHudImage
    {
        public BattleHudSprite sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
    }

    [Serializable] public sealed class BattleHudSprite
    {
        public string file;
        public int width, height;
        public float[] border;
        public float[] pivot;
        public float ppu;
    }

    [Serializable] public sealed class BattleHudText
    {
        public string value;
        public float[] color;
        public float font_size;
        public int horizontal, vertical, overflow;
        public bool word_wrap;
        public float[] margin, auto_size;
    }

    [Serializable] public sealed class BattleHudScroll
    {
        public bool horizontal, vertical, inertia;
        public int movement;
        public float sensitivity, elasticity, deceleration;
    }

    [Serializable] public sealed class BattleHudSpriteBinding
    {
        public string file;
        public Sprite sprite;
    }

    [Serializable] public sealed class BattleHudRuntimeSourceManifest
    {
        public string manifest, source_path, source_sha256, output_path, output_sha256;
    }

    [CreateAssetMenu(menuName = "Zjcs/Combat/Official Battle HUD Catalog")]
    public sealed class OfficialBattleHudCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Combat/OfficialBattleHudCatalog";
        [SerializeField] private TextAsset runtimePanels;
        [SerializeField] private TextAsset originalPanels;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private BattleHudSpriteBinding[] sprites = Array.Empty<BattleHudSpriteBinding>();
        [SerializeField] private TMPro.TMP_FontAsset font;
        private BattleHudCatalogData data;

        public TMPro.TMP_FontAsset Font => font;

        public static OfficialBattleHudCatalog LoadDefault()
        {
            OfficialBattleHudCatalog catalog = Resources.Load<OfficialBattleHudCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official battle HUD catalog is not installed.");
            catalog.ValidateBindings();
            catalog.Font.ReadFontAssetDefinition();
            return catalog;
        }

        public BattleHudCatalogData Load()
        {
            ValidateBindings();
            if (data == null)
            {
                BattleHudRuntimeSourceManifest source = JsonUtility.FromJson<BattleHudRuntimeSourceManifest>(sourceManifest.text);
                if (source == null || source.manifest != "854_156383"
                    || source.source_path != "Assets/Official/Combat/HUD/panels.json"
                    || source.output_path != "Assets/Official/Combat/HUD/panels-runtime.json"
                    || source.source_sha256 != Sha256(Encoding.UTF8.GetBytes(originalPanels.text))
                    || source.output_sha256 != Sha256(Encoding.UTF8.GetBytes(runtimePanels.text)))
                    throw new InvalidOperationException("Official battle HUD source hashes do not match.");
                data = JsonUtility.FromJson<BattleHudCatalogData>(runtimePanels.text);
                if (data == null || data.manifest != "854_156383" || data.prefabs == null || data.prefabs.Length != 30)
                    throw new FormatException("Official battle HUD data is incomplete or from an unsupported manifest.");
            }
            return data;
        }

        public BattleHudPrefab FindPrefab(string key)
        {
            foreach (BattleHudPrefab prefab in Load().prefabs)
                if (prefab.key == key) return prefab;
            return null;
        }

        public Sprite FindSprite(string file)
        {
            foreach (BattleHudSpriteBinding binding in sprites)
                if (binding != null && binding.file == file) return binding.sprite;
            return null;
        }

        private void ValidateBindings()
        {
            if (runtimePanels == null || originalPanels == null || sourceManifest == null || font == null
                || sprites == null || sprites.Length != 701)
                throw new InvalidOperationException("Official battle HUD sources, sprites, or FZY4K font are not bound.");
            foreach (BattleHudSpriteBinding binding in sprites)
                if (binding == null || string.IsNullOrEmpty(binding.file) || binding.sprite == null)
                    throw new InvalidOperationException("An official battle HUD sprite binding is missing.");
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var value = new StringBuilder(64);
                foreach (byte item in sha.ComputeHash(bytes)) value.Append(item.ToString("x2"));
                return value.ToString();
            }
        }
    }
}
