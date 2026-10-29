using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Offline.Equipment
{
    [CreateAssetMenu(menuName = "Zjcs/Equipment/Official Equipment Asset Catalog")]
    public sealed class OfficialEquipmentAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Equipment/OfficialEquipmentAssetCatalog";
        private const string ExpectedManifest = "854_156383";
        private const string ExpectedRuntimePath = "Assets/Official/Equipment/Runtime/equipment-runtime-rules.json";

        [SerializeField] private TextAsset runtimeRules;
        [SerializeField] private TextAsset runtimeSourceManifest;

        [Serializable]
        private sealed class RuntimeManifest
        {
            public string manifest;
            public RuntimeOutput output;
            public List<RuntimeInput> inputs;
            public List<string> tables;
        }

        [Serializable]
        private sealed class RuntimeOutput
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class RuntimeInput
        {
            public string path;
            public string sha256;
        }

        public static OfficialEquipmentAssetCatalog LoadDefault()
        {
            OfficialEquipmentAssetCatalog catalog = Resources.Load<OfficialEquipmentAssetCatalog>(DefaultResourcePath);
            if (catalog == null)
                throw new InvalidOperationException("Official equipment catalog is not installed at Resources/" + DefaultResourcePath + ".asset");
            return catalog;
        }

        public OfficialEquipmentCatalog Load()
        {
            if (runtimeRules == null || runtimeSourceManifest == null)
                throw new InvalidOperationException("Official equipment runtime rules and source manifest are not bound.");
            RuntimeManifest manifest = JsonUtility.FromJson<RuntimeManifest>(runtimeSourceManifest.text);
            if (manifest == null || manifest.manifest != ExpectedManifest || manifest.output == null
                || manifest.output.path != ExpectedRuntimePath || manifest.output.bytes != runtimeRules.bytes.Length
                || string.IsNullOrEmpty(manifest.output.sha256) || manifest.inputs == null || manifest.inputs.Count != 3)
                throw new InvalidOperationException("Official equipment runtime source manifest is invalid.");
            if (!string.Equals(Sha256(runtimeRules.bytes), manifest.output.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Official equipment runtime rules differ from the source manifest.");

            OfficialEquipmentCatalog catalog = OfficialEquipmentCatalog.Load(runtimeRules.text);
            RequireRows(catalog, "item", 1);
            RequireRows(catalog, "equip", 1);
            RequireRows(catalog, "equip_type", 1);
            RequireRows(catalog, "equip_upgrade_material", 1);
            RequireRows(catalog, "equip_inherit_cost", 1);
            RequireRows(catalog, "equip_inlay_site", 1);
            RequireRows(catalog, "gem", 1);
            RequireRows(catalog, "equip_piece_merge", 1);
            RequireRows(catalog, "system_level_limit", 1);
            RequireRows(catalog, "system_level_gap_limit", 1);
            return catalog;
        }

        private static void RequireRows(OfficialEquipmentCatalog catalog, string table, int minimum)
        {
            if (catalog.Rows(table).Count < minimum)
                throw new InvalidOperationException("Official equipment table is empty: " + table);
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
    }
}
