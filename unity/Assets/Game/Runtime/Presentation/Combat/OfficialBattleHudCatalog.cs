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
        public BattleHudNode[] nodes;
        public BattleHudDamageFloatBinding damageUi;
        public BattleBossHpBarBinding bossHpBar;

        public BattleHudNode BuildRoot()
        {
            if (nodes == null || nodes.Length == 0)
            {
                if (root == null) throw new FormatException("Official battle HUD prefab has no nodes: " + key);
                return root;
            }

            var children = new List<int>[nodes.Length];
            int rootIndex = -1;
            for (int index = 0; index < nodes.Length; index++)
            {
                BattleHudNode node = nodes[index];
                if (node == null) throw new FormatException("Official battle HUD node is null: " + key);
                node.children = null;
                int parentIndex = node.parentIndex;
                if (parentIndex == -1)
                {
                    if (rootIndex >= 0) throw new FormatException("Official battle HUD prefab has multiple roots: " + key);
                    rootIndex = index;
                }
                else
                {
                    if (parentIndex < 0 || parentIndex >= index)
                        throw new FormatException("Official battle HUD parent index is invalid: " + key);
                    if (children[parentIndex] == null) children[parentIndex] = new List<int>();
                    children[parentIndex].Add(index);
                }
            }

            if (rootIndex != 0) throw new FormatException("Official battle HUD root node is invalid: " + key);
            for (int index = 0; index < nodes.Length; index++)
            {
                List<int> childIndices = children[index];
                if (childIndices == null) continue;
                nodes[index].children = new BattleHudNode[childIndices.Count];
                for (int child = 0; child < childIndices.Count; child++)
                    nodes[index].children[child] = nodes[childIndices[child]];
            }

            root = nodes[rootIndex];
            return root;
        }
    }

    [Serializable] public sealed class BattleBossHpBarBinding
    {
        public string[] hpSpriteFiles;
        public string shieldSpriteFile, emptyBarSpriteFile;
        public BattleHudColor[] shieldColors;
        public float moveSpeed;
    }

    [Serializable] public sealed class BattleHudColor
    {
        public float[] rgba;
    }

    [Serializable] public sealed class BattleHudDamageFloatBinding
    {
        public int type;
        public float showTime;
        public string numberPath, tipPath, breakResiliencePath, trueDamagePath;
        public float height;
        public float[] sceneOffset;
    }

    [Serializable] public sealed class BattleHudStatusIconCatalogData
    {
        public string manifest;
        public int statusEntityCount;
        public BattleHudStatusIconBinding[] entries;
    }

    [Serializable] public sealed class BattleHudStatusIconBinding
    {
        public int statusId;
        public string iconFile, backgroundFile;
        public bool showStackCount;
    }

    [Serializable] public sealed class BattleHudNode
    {
        public string name, path, localization_key;
        public int parentIndex = -1;
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
        public bool animation_play_automatically;
        public string default_animation;
        public BattleHudAnimation[] animations;
        public BattleHudNode[] children;
        public BattleHudImage image;
        public BattleHudText text;
        public BattleHudScroll scroll;
    }

    [Serializable] public sealed class BattleHudAnimation
    {
        public string name, owner;
        public float duration;
        public bool loop;
        public BattleHudAnimationTrack[] tracks;
    }

    [Serializable] public sealed class BattleHudAnimationTrack
    {
        public string path, property;
        public BattleHudAnimationKey[] keys;
    }

    [Serializable] public sealed class BattleHudAnimationKey
    {
        public float time, value, in_tangent, out_tangent;
    }

    [Serializable] public sealed class BattleUnitBarRuntimeData
    {
        public string manifest, sourceSha256;
        public BattleUnitBarBinding[] entities;
        public BattleUnitBarFaction[] factions;
    }

    [Serializable] public sealed class BattleUnitBarBinding
    {
        public int entityId;
        public float duration;
        public BattleUnitBarPart[] parts;
        public BattleUnitStatusLayout statusLayout;
    }

    [Serializable] public sealed class BattleUnitBarPart
    {
        public string name, role, spriteFile;
        public float[] position, scale, size, color;
        public int fillMode, fillOrigin, order;
    }

    [Serializable] public sealed class BattleUnitStatusLayout
    {
        public float[] position, scale, cellSize;
        public int columns, rows;
    }

    [Serializable] public sealed class BattleUnitBarFaction
    {
        public string name;
        public float[] hpColor, hpAddColor, hpSubColor;
    }

    [Serializable] public sealed class BattleStatusIconData
    {
        public string manifest, entryTableSha256, battleRulesSha256, spriteManifestSha256, stageStatusRulesSha256;
        public int statusEntityCount;
        public BattleStatusDisplay[] entries;
    }

    [Serializable] public sealed class BattleStatusDisplay
    {
        public int statusId;
        public string iconFile, backgroundFile, nameText, descriptionText;
        public bool showStackCount, showSpriteAboveUnit, showRoundReplaceStack,
            showCustomCount, hasRoundStartComponent;
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
        private const string StatusIconCatalogSha256 = "ac23e85cfcf855ec4c997f62b28e91ed1ee580f182bcf393f3783cfe57a8a6f4";
        private const string StatusIconEntryTableSha256 = "97550f16026c2e17ed7d41a6a598c2386c1e6db0728cdbd9f7ef426d629b27bb";
        private const string StatusIconBattleRulesSha256 = "9e1f1cf7332760f55ad27b39966f5a97bb8b227d0d8bff8c98a5c856dafd6803";
        private const string StatusIconSpriteManifestSha256 = "01962eb1dbfbebe32f4b161fdb57775827de9de6c3aa151b220052b79f42b485";
        private const string StatusIconStageRulesSha256 = "f8c0f515d2f44c5d0847d5422b49d8823578d55de69f59f2d10ec5db83091256";
        [SerializeField] private TextAsset runtimePanels;
        [SerializeField] private TextAsset originalPanels;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private BattleHudSpriteBinding[] sprites = Array.Empty<BattleHudSpriteBinding>();
        [SerializeField] private TMPro.TMP_FontAsset font;
        private BattleHudCatalogData data;
        private BattleUnitBarRuntimeData unitBarData;
        private BattleStatusIconData statusIconData;
        private Dictionary<int, BattleUnitBarBinding> unitBarsByEntity;
        private Dictionary<int, BattleStatusDisplay> statusDisplaysById;

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
                foreach (BattleHudPrefab prefab in data.prefabs)
                    if (prefab == null || prefab.BuildRoot() == null)
                        throw new FormatException("Official battle HUD prefab is invalid.");
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
            if (string.IsNullOrEmpty(file)) return null;
            const string sourceSpritePrefix = "res://assets/official/battle-hud/sprites/";
            if (file.StartsWith(sourceSpritePrefix, StringComparison.Ordinal))
                file = file.Substring(sourceSpritePrefix.Length);
            bool matchByName = file.IndexOf('/') < 0 && file.IndexOf('\\') < 0;
            string fileSuffix = "/" + file;
            Sprite nameMatch = null;
            foreach (BattleHudSpriteBinding binding in sprites)
            {
                if (binding == null) continue;
                if (binding.file == file) return binding.sprite;
                if (!matchByName || !binding.file.EndsWith(fileSuffix, StringComparison.Ordinal)) continue;
                if (nameMatch != null && nameMatch != binding.sprite)
                    throw new InvalidOperationException("Official HUD sprite filename is ambiguous: " + file);
                nameMatch = binding.sprite;
            }
            return nameMatch;
        }

        public bool TryGetUnitBar(int entityId, out BattleUnitBarBinding binding)
        {
            EnsureBattleRuntimeData();
            return unitBarsByEntity.TryGetValue(entityId, out binding);
        }

        public BattleUnitBarFaction UnitBarFaction(bool player)
        {
            EnsureBattleRuntimeData();
            string name = player ? "CharacterMeEffect" : "DefaultEffect";
            foreach (BattleUnitBarFaction faction in unitBarData.factions)
                if (faction != null && faction.name == name) return faction;
            throw new InvalidOperationException("Official battle unit-bar faction is missing: " + name);
        }

        public bool TryGetStatusDisplay(int statusId, out Sprite icon, out Sprite background,
            out bool showSpriteAboveUnit, out bool showStackCount, out bool showRoundReplaceStack,
            out bool showCustomCount, out bool hasRoundStartComponent)
        {
            EnsureBattleRuntimeData();
            bool found = statusDisplaysById.TryGetValue(statusId, out BattleStatusDisplay display);
            icon = found ? FindSprite(display.iconFile) : null;
            background = found ? FindSprite(display.backgroundFile) : null;
            showSpriteAboveUnit = found && display.showSpriteAboveUnit;
            showStackCount = found && display.showStackCount;
            showRoundReplaceStack = found && display.showRoundReplaceStack;
            showCustomCount = found && display.showCustomCount;
            hasRoundStartComponent = found && display.hasRoundStartComponent;
            return found;
        }

        public bool TryGetStatusSprites(int statusId, out Sprite icon, out Sprite background,
            out bool showStackCount)
        {
            return TryGetStatusDisplay(statusId, out icon, out background, out _, out showStackCount,
                out _, out _, out _);
        }

        public bool TryGetStatusDetails(int statusId, out string name, out string description)
        {
            EnsureBattleRuntimeData();
            if (statusDisplaysById.TryGetValue(statusId, out BattleStatusDisplay display)
                && !string.IsNullOrEmpty(display.nameText)
                && !string.IsNullOrEmpty(display.descriptionText))
            {
                name = display.nameText;
                description = display.descriptionText;
                return true;
            }
            name = null;
            description = null;
            return false;
        }

        private void EnsureBattleRuntimeData()
        {
            if (unitBarData != null && statusIconData != null) return;
            TextAsset unitBarsText = Resources.Load<TextAsset>("Combat/unit-bars-runtime");
            TextAsset unitBarsManifestText = Resources.Load<TextAsset>("Combat/unit-bars-runtime-source-manifest");
            TextAsset statusIconsText = Resources.Load<TextAsset>("Combat/battle-status-icons");
            if (unitBarsText == null || unitBarsManifestText == null || statusIconsText == null)
                throw new InvalidOperationException("Official battle unit-bar or status display data is not installed.");
            if (Sha256(Encoding.UTF8.GetBytes(statusIconsText.text)) != StatusIconCatalogSha256)
                throw new InvalidOperationException("Official battle status icon catalog hash does not match.");

            BattleHudRuntimeSourceManifest source = JsonUtility.FromJson<BattleHudRuntimeSourceManifest>(unitBarsManifestText.text);
            if (source == null || source.manifest != "854_156383"
                || source.source_path != "Assets/Official/Combat/HUD/unit-bars.json"
                || source.output_path != "Assets/Game/Resources/Combat/unit-bars-runtime.json"
                || source.output_sha256 != Sha256(Encoding.UTF8.GetBytes(unitBarsText.text)))
                throw new InvalidOperationException("Official battle unit-bar source hashes do not match.");

            BattleUnitBarRuntimeData bars = JsonUtility.FromJson<BattleUnitBarRuntimeData>(unitBarsText.text);
            BattleStatusIconData statuses = JsonUtility.FromJson<BattleStatusIconData>(statusIconsText.text);
            if (bars == null || bars.manifest != "854_156383"
                || bars.sourceSha256 != source.source_sha256 || bars.entities == null || bars.entities.Length != 503
                || bars.factions == null || bars.factions.Length != 2
                || statuses == null || statuses.manifest != "854_156383"
                || statuses.statusEntityCount != 1595 || statuses.entries == null || statuses.entries.Length != 715
                || statuses.entryTableSha256 != StatusIconEntryTableSha256
                || statuses.battleRulesSha256 != StatusIconBattleRulesSha256
                || statuses.spriteManifestSha256 != StatusIconSpriteManifestSha256
                || statuses.stageStatusRulesSha256 != StatusIconStageRulesSha256)
                throw new FormatException("Official battle unit-bar or status display data is incomplete.");

            var barsByEntity = new Dictionary<int, BattleUnitBarBinding>(bars.entities.Length);
            foreach (BattleUnitBarBinding bar in bars.entities)
                if (bar == null || bar.entityId <= 0 || bar.parts == null || bar.parts.Length == 0
                    || !barsByEntity.TryAdd(bar.entityId, bar))
                    throw new FormatException("Official battle unit-bar entity data is invalid.");
            var displaysById = new Dictionary<int, BattleStatusDisplay>(statuses.entries.Length);
            foreach (BattleStatusDisplay display in statuses.entries)
                if (display == null || display.statusId <= 0 || !displaysById.TryAdd(display.statusId, display))
                    throw new FormatException("Official battle status display data is invalid.");

            unitBarData = bars;
            statusIconData = statuses;
            unitBarsByEntity = barsByEntity;
            statusDisplaysById = displaysById;
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
