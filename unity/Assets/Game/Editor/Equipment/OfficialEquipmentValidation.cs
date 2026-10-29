using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Editor.Equipment
{
    public static class OfficialEquipmentValidation
    {
        private const string RuntimeRulesPath = "Assets/Official/Equipment/Runtime/equipment-runtime-rules.json";
        private const string RuntimeManifestPath = "Assets/Official/Equipment/Runtime/source-manifest.json";
        private const string CatalogPath = "Assets/Game/Resources/Equipment/OfficialEquipmentAssetCatalog.asset";
        private const string UiCatalogPath = "Assets/Official/Equipment/UI/equipment-ui.json";
        private const string UiManifestPath = "Assets/Official/Equipment/UI/source-manifest.json";
        private static readonly string[] RequiredUiPanels =
        {
            "equipment", "equip_detail", "equip_compare", "item_equip", "item_gem", "equip_row",
            "gem_merge_row", "equip_promote", "equip_promote_cell", "equip_promote_bar",
            "equip_promote_bar_row", "equip_promote_prop", "equip_gem_row", "equip_inherit",
            "equip_inherit_content", "equip_inherit_rule", "equip_select", "equip_select_row",
            "equip_resonance", "equip_resonance_content", "equip_resonance_row", "equip_gem_slot",
            "equip_select_detail", "equip_inherit_prop", "equip_inherit_select_prop", "gem_detail",
            "equip_term", "equip_piece_detail", "equip_choose", "equip_choose_row", "equip_bless_tip",
            "equip_suit_tip", "equip_piece_confirm", "equip_term_skill_detail", "equip_choose_icon",
            "equip_level_tip", "equip_piece_owned_hint", "equip_level_tip_row", "equip_detail_gem"
        };

        [Serializable]
        private sealed class UiManifest
        {
            public string manifest;
            public UiOutput output;
            public List<string> panels;
            public List<UiSprite> sprites;
        }

        [Serializable]
        private sealed class UiOutput
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class UiSprite
        {
            public string path;
            public string sha256;
        }

        [MenuItem("Zjcs/Equipment/Validate Official Catalog")]
        public static void Validate()
        {
            TextAsset rules = AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimeRulesPath);
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimeManifestPath);
            if (rules == null || manifest == null)
                throw new InvalidOperationException("Restore the verified equipment payload with scripts/prepare_unity_equipment_catalog.py.");

            EnsureFolder("Assets/Game/Resources");
            EnsureFolder("Assets/Game/Resources/Equipment");
            OfficialEquipmentAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialEquipmentAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialEquipmentAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            SerializedProperty rulesField = serialized.FindProperty("runtimeRules");
            SerializedProperty manifestField = serialized.FindProperty("runtimeSourceManifest");
            if (rulesField == null || manifestField == null)
                throw new InvalidOperationException("Official equipment asset catalog fields are missing.");
            rulesField.objectReferenceValue = rules;
            manifestField.objectReferenceValue = manifest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            OfficialEquipmentCatalog loaded = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            if (loaded.Find("equip", 20000) == null || loaded.Find("item", 20000) == null
                || loaded.PositionForItem(20000) != 10 || !loaded.CanWear(20000, 1)
                || loaded.PropertyValue(1000, 3, 1) <= 0)
                throw new InvalidOperationException("Official equipment catalog failed its reference data checks.");
            OfflineEquipmentInstance generated = OfficialEquipmentGenerator.Generate(loaded, 20000, 1,
                new EquipmentRollContext { DropLevel = 1, PlayerLevel = 1, SubRank = 0, SourceOperate = 0 },
                () => 0.5f);
            if (generated.ItemId != 20000 || generated.InstanceId != 1 || generated.ItemLevel != 3
                || generated.SecondaryProperty != 4)
                throw new InvalidOperationException("Official equipment generation failed its pinned reference check.");
            int sprites = ValidateOriginalUiPayload();
            Debug.Log("Official equipment data validated: " + loaded.Rows("equip").Count
                + " equipment, " + loaded.Rows("gem").Count + " gems, " + RequiredUiPanels.Length
                + " official UI panel trees, " + sprites + " original sprites, and pinned 854_156383 rules.");
        }

        private static int ValidateOriginalUiPayload()
        {
            TextAsset ui = AssetDatabase.LoadAssetAtPath<TextAsset>(UiCatalogPath);
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(UiManifestPath);
            if (ui == null || source == null)
                throw new InvalidOperationException("Restore the official equipment UI payload with scripts/prepare_unity_equipment_catalog.py.");

            UiManifest manifest = JsonUtility.FromJson<UiManifest>(source.text);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.output == null
                || manifest.output.path != UiCatalogPath || manifest.output.bytes != ui.bytes.Length
                || manifest.panels == null || !manifest.panels.SequenceEqual(RequiredUiPanels)
                || manifest.sprites == null || manifest.sprites.Count == 0
                || !string.Equals(Sha256(ui.bytes), manifest.output.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Official equipment UI source manifest or payload hash is invalid.");

            Dictionary<string, object> panels = OfficialEquipmentJson.Parse(ui.text) as Dictionary<string, object>;
            if (panels == null || panels.Count != RequiredUiPanels.Length)
                throw new InvalidOperationException("Official equipment UI panel catalog has an unexpected shape.");
            foreach (string name in RequiredUiPanels)
            {
                if (!(panels.TryGetValue(name, out object value) && value is Dictionary<string, object> panel
                    && panel.TryGetValue("source", out object sourceValue) && sourceValue is Dictionary<string, object>
                    && panel.TryGetValue("root", out object rootValue) && rootValue is Dictionary<string, object>))
                    throw new InvalidOperationException("Official equipment UI panel is incomplete: " + name);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (UiSprite sprite in manifest.sprites)
            {
                if (sprite == null || string.IsNullOrEmpty(sprite.path)
                    || !sprite.path.StartsWith("Assets/Official/Equipment/UI/Sprites/", StringComparison.Ordinal)
                    || sprite.path.Contains(".."))
                    throw new InvalidOperationException("Official equipment UI sprite path is invalid.");
                string absolute = Path.Combine(projectRoot, sprite.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute) || !string.Equals(Sha256(File.ReadAllBytes(absolute)), sprite.sha256, StringComparison.Ordinal)
                    || AssetDatabase.LoadAssetAtPath<Sprite>(sprite.path) == null)
                    throw new InvalidOperationException("Official equipment UI sprite failed its import or source hash check: " + sprite.path);
            }
            return manifest.sprites.Count;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                StringBuilder value = new StringBuilder(64);
                foreach (byte item in hash.ComputeHash(bytes)) value.Append(item.ToString("x2"));
                return value.ToString();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
