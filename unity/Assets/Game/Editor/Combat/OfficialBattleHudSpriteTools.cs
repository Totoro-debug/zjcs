using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleHudSpriteTools
    {
        private const string SpriteRoot = "Assets/Official/Combat/HUD/Sprites/";
        private const string ReceiptPath = "../assets/reference/battle-hud/sprite-import-source-manifest.json";

        [MenuItem("Zjcs/Combat/Restore Official HUD Sprite Metadata")]
        public static void Restore()
        {
            var sources = Sources();
            int changed = 0;
            foreach (KeyValuePair<string, BattleHudNode> pair in sources)
            {
                string path = SpriteRoot + pair.Key;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidDataException("Official HUD texture importer is missing: " + path);
                BattleHudNode node = pair.Value;
                Vector4 border = Border(node);
                Vector2 pivot = Pivot(node);
                bool different = importer.spriteBorder != border || importer.spritePivot != pivot
                    || importer.spritePixelsPerUnit != node.image_sprite_ppu;
                if (!different) continue;
                importer.spriteBorder = border;
                importer.spritePivot = pivot;
                importer.spritePixelsPerUnit = node.image_sprite_ppu;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }
            Validate();
            var outputs = sources.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new SpriteSource
            {
                path = "unity/" + SpriteRoot + pair.Key, png_sha256 = Sha(File.ReadAllBytes(SpriteRoot + pair.Key)),
                meta_sha256 = Sha(File.ReadAllBytes(SpriteRoot + pair.Key + ".meta")),
                prefab_node = pair.Value.path, border = pair.Value.image_sprite_border,
                pivot = pair.Value.image_sprite_pivot, ppu = pair.Value.image_sprite_ppu
            }).ToArray();
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(new SourceReceipt
            {
                manifest = "854_156383", runtime_prefab_sha256 = Sha(File.ReadAllBytes("Assets/Official/Combat/HUD/panels-runtime.json")),
                prefab_source_manifest_sha256 = Sha(File.ReadAllBytes("../assets/reference/battle-hud/source-manifest.json")),
                importer_sha256 = Sha(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialBattleHudSpriteTools.cs")),
                sprite_count = outputs.Length, sprites = outputs
            }, true) + "\n");
            Debug.Log("Official battle HUD Sprite metadata restored: " + changed + " affected importers; " + sources.Count + " referenced sprites validated.");
        }

        public static void Validate()
        {
            var catalog = OfficialBattleHudCatalog.LoadDefault();
            foreach (KeyValuePair<string, BattleHudNode> pair in Sources())
            {
                Sprite sprite = catalog.FindSprite(pair.Key);
                BattleHudNode node = pair.Value;
                if (sprite == null || sprite.rect.width != node.image_sprite_width || sprite.rect.height != node.image_sprite_height
                    || sprite.border != Border(node) || Vector2.Distance(sprite.pivot, new Vector2(node.image_sprite_pivot[0], node.image_sprite_pivot[1])) > .001f
                    || sprite.pixelsPerUnit != node.image_sprite_ppu)
                    throw new InvalidDataException("Official HUD Sprite dimensions, border, pivot or PPU changed: " + pair.Key);
            }
        }

        private static SortedDictionary<string, BattleHudNode> Sources()
        {
            var sources = new SortedDictionary<string, BattleHudNode>(StringComparer.Ordinal);
            foreach (BattleHudPrefab prefab in OfficialBattleHudCatalog.LoadDefault().Load().prefabs)
                foreach (BattleHudNode node in prefab.nodes)
                {
                    if (!node.has_image_sprite) continue;
                    string key = Path.GetFileName(node.image_sprite_file);
                    if (sources.TryGetValue(key, out BattleHudNode previous))
                    {
                        if (Border(previous) != Border(node) || Pivot(previous) != Pivot(node)
                            || previous.image_sprite_width != node.image_sprite_width || previous.image_sprite_height != node.image_sprite_height
                            || previous.image_sprite_ppu != node.image_sprite_ppu)
                            throw new InvalidDataException("Conflicting original HUD Sprite metadata: " + key);
                    }
                    else sources.Add(key, node);
                }
            return sources;
        }

        private static Vector4 Border(BattleHudNode node) => new Vector4(node.image_sprite_border[0],
            node.image_sprite_border[1], node.image_sprite_border[2], node.image_sprite_border[3]);
        private static Vector2 Pivot(BattleHudNode node) => new Vector2(node.image_sprite_pivot[0] / node.image_sprite_width,
            node.image_sprite_pivot[1] / node.image_sprite_height);
        private static string Sha(byte[] raw)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
        }
        [Serializable] private sealed class SourceReceipt
        {
            public string manifest, runtime_prefab_sha256, prefab_source_manifest_sha256, importer_sha256;
            public int sprite_count;
            public SpriteSource[] sprites;
        }
        [Serializable] private sealed class SpriteSource
        {
            public string path, png_sha256, meta_sha256, prefab_node;
            public float[] border, pivot;
            public float ppu;
        }
    }
}
